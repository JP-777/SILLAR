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

    /// <summary>Los datos enviados no sirven.</summary>
    Invalid
}

/// <summary>Resultado de la instalación.</summary>
internal sealed record SetupResult(SetupOutcome Outcome, string? Error = null, SetupResponse? Response = null);

/// <summary>Instalación inicial del sistema.</summary>
internal sealed class SetupService(
    CoreDbContext database,
    IPasswordHasher hasher,
    IAuditWriter audit,
    TimeProvider clock)
{
    /// <summary>Indica si queda instalación pendiente.</summary>
    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken)
    {
        var installation = await database.Installations
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return installation is null || !installation.IsSetupComplete;
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
