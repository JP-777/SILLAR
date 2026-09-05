using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;
using Sillar.Core.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Core.Services;

/// <summary>Cómo terminó un intento de instalación.</summary>
internal enum SetupOutcome
{
    /// <summary>Instalación completada.</summary>
    Completed,

    /// <summary>Ya estaba instalado. Las rutas de instalación dejan de existir.</summary>
    AlreadyInstalled,

    /// <summary>
    /// El esquema de CORE no está en la base. No es culpa de los datos enviados
    /// y no se arregla desde el asistente.
    /// </summary>
    MigrationsPending,

    /// <summary>Los datos enviados no sirven.</summary>
    Invalid
}

/// <summary>Resultado de la instalación.</summary>
internal sealed record SetupResult(SetupOutcome Outcome, string? Error = null, SetupResponse? Response = null);

/// <summary>En qué estado está la instalación.</summary>
/// <remarks>
/// Son <b>tres</b> y no dos. Que falten las migraciones no es un caso raro de
/// «falta instalar»: es una situación distinta, con otro responsable y otro
/// remedio. Tratarlas igual es lo que hacía que la primera pantalla de una
/// instalación nueva fuera un 500 con un <c>traceId</c>.
/// </remarks>
internal enum SetupState
{
    /// <summary>El esquema de CORE no está en la base. Faltan las migraciones.</summary>
    MigrationsPending,

    /// <summary>Las tablas están, pero nadie ha completado la instalación.</summary>
    SetupPending,

    /// <summary>Instalado y completo.</summary>
    Completed
}

/// <summary>Instalación inicial del sistema.</summary>
internal sealed class SetupService(
    CoreDbContext database,
    IPasswordHasher hasher,
    IAuditWriter audit,
    TimeProvider clock)
{
    /// <summary>En qué estado está la instalación.</summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué se atrapa <c>42P01</c> aquí y no se comprueba antes.</b> Una
    /// base recién creada no tiene el esquema <c>core</c>, así que esta consulta
    /// —la primera que hace el sistema— lanzaba <c>relation "core.installation"
    /// does not exist</c> y salía por el manejador genérico como un 500 en crudo.
    /// Y ocurría justo en <c>GET /api/setup/status</c>, que es la <b>única</b>
    /// ruta que el modo instalación monta: la primera pantalla de quien instala
    /// en una clienta.
    /// </para>
    /// <para>
    /// Preguntar antes por el esquema —un <c>SELECT</c> a <c>information_schema</c>
    /// en cada llamada— costaría una consulta de más siempre para cubrir un caso
    /// que ocurre una vez en la vida de la instalación. El error es la señal, y
    /// PostgreSQL la da con un código estable.
    /// </para>
    /// </remarks>
    public async Task<SetupState> GetStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installation = await database.Installations
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            return installation is null || !installation.IsSetupComplete
                ? SetupState.SetupPending
                : SetupState.Completed;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return SetupState.MigrationsPending;
        }
    }

    /// <summary>Crea la instalación y su primer <c>super_admin</c>.</summary>
    /// <remarks>
    /// Todo en una transacción: una instalación a medias —con negocio pero sin
    /// nadie que pueda entrar— dejaría el sistema inutilizable y sin forma de
    /// arreglarlo desde el propio sistema.
    /// </remarks>
    public async Task<SetupResult> CompleteAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return new SetupResult(SetupOutcome.Invalid, invalid);
        }

        var admin = request.Admin!;
        var now = clock.GetUtcNow();

        // Sin tablas no hay nada que instalar, y el asistente no puede crearlas.
        // Se comprueba antes de abrir la transacción: abrirla para descubrir que
        // la primera consulta revienta deja el mismo 500 en crudo que esto viene
        // a quitar, solo que un paso más tarde.
        if (await GetStateAsync(cancellationToken) is SetupState.MigrationsPending)
        {
            return new SetupResult(SetupOutcome.MigrationsPending);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        if (await database.Installations.AnyAsync(cancellationToken))
        {
            return new SetupResult(SetupOutcome.AlreadyInstalled);
        }

        database.Installations.Add(new Installation
        {
            Singleton = true,
            BusinessName = request.BusinessName!.Trim(),
            // Se genera aquí. Nunca llega del cliente: es la identidad de esta
            // instalación y quien la elige, la controla.
            InstallationKey = Guid.NewGuid(),
            ProductVersion = SillarProduct.Version,
            LicenseType = request.LicenseType!,
            IsSetupComplete = true
        });

        // **El nombre del negocio también al ajuste público.**
        //
        // Se pedía en la instalación y se guardaba solo en la fila de la
        // instalación, mientras el ajuste `business_name` —que es el que lee la
        // web pública— se quedaba en el `PENDIENTE_DEFINIR` del seed hasta que
        // alguien lo editara en Configuración. **El mismo dato en dos sitios, y
        // el que se quedaba atrás era justo el que ve el público**: un sitio
        // recién instalado salía sin nombre.
        //
        // Se pide una vez y sirve para las dos cosas. Configuración lo cambia
        // después, que es donde corresponde.
        var nombrePublico = await database.SiteSettings
            .FirstOrDefaultAsync(setting => setting.SettingKey == "business_name", cancellationToken);

        if (nombrePublico is not null)
        {
            nombrePublico.SettingValue = request.BusinessName!.Trim();
        }

        var user = new AdminUser
        {
            FullName = admin.FullName!.Trim(),
            Email = admin.Email!.Trim(),
            PasswordHash = hasher.Hash(admin.Password!),
            Role = AdminRole.SuperAdmin,
            IsActive = true
        };

        database.AdminUsers.Add(user);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSingletonViolation(exception))
        {
            // Dos peticiones simultáneas: la restricción uq_installation_singleton
            // deja pasar solo a una. La otra se entera aquí y responde como si la
            // ruta no existiera, que es lo que ocurre a partir de ahora.
            return new SetupResult(SetupOutcome.AlreadyInstalled);
        }

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Setup)
            {
                AdminUserId = user.AdminUserId,
                AdminUserEmail = user.Email,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "installation",
                Summary = $"Instalación completada para «{request.BusinessName!.Trim()}» con licencia {request.LicenseType}."
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new SetupResult(
            SetupOutcome.Completed,
            Response: new SetupResponse(request.BusinessName!.Trim(), user.AdminUserId, user.Email));
    }

    /// <summary>Comprueba los datos recibidos y devuelve el motivo del rechazo.</summary>
    private static string? Validate(SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessName) || request.BusinessName.Trim().Length > 150)
        {
            return "El nombre del negocio es obligatorio y no puede pasar de 150 caracteres.";
        }

        if (request.LicenseType is null || !LicenseType.All.Contains(request.LicenseType))
        {
            return $"El tipo de licencia debe ser uno de: {string.Join(", ", LicenseType.All)}.";
        }

        var admin = request.Admin;

        if (admin is null)
        {
            return "Hacen falta los datos del primer administrador.";
        }

        if (string.IsNullOrWhiteSpace(admin.FullName) || admin.FullName.Trim().Length > 150)
        {
            return "El nombre del administrador es obligatorio y no puede pasar de 150 caracteres.";
        }

        if (!EmailAddress.IsValid(admin.Email))
        {
            return "El correo del administrador no es válido.";
        }

        var password = PasswordPolicy.Check(admin.Password, admin.Email!, admin.FullName);

        return password.IsValid ? null : password.Error;
    }

    private static bool IsSingletonViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
