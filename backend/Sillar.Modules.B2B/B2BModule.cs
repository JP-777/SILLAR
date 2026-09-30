using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Endpoints;
using Sillar.Modules.B2B.Solicitudes;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.B2B;

/// <summary>M07 — Solicitudes B2B y Especiales.</summary>
/// <remarks>
/// <b>Dependencias duras de M01 y M04</b> (SPEC §3): toda solicitud exige
/// cuenta, y las líneas de cotización se atan a una presentación del
/// catálogo. Por eso sus claves foráneas hacia <c>catalog</c> y <c>crm</c> van
/// dentro de su migración, y desinstalar cualquiera de las dos con M07
/// instalado se rechaza (C6).
/// </remarks>
public sealed class B2BModule : IModule, IModuleMigrations
{
    public const string ModuleCode = "b2b";
    public const string ConnectionStringName = "Default";
    public string Code => ModuleCode;

    private static readonly MigracionesDeContexto<B2bDbContext> Migraciones = new(
        connectionString => new B2bDbContext(
            PersistenciaDeModulo.Opciones<B2bDbContext>(
                connectionString, B2bDbContext.Schema, B2bDbContext.MigrationsHistoryTable)),
        B2bDbContext.MigrationsHistoryTable);

    /// <inheritdoc />
    public string MigrationsHistoryTable => Migraciones.MigrationsHistoryTable;

    /// <inheritdoc />
    public IReadOnlyList<string> KnownMigrations(string connectionString)
        => Migraciones.KnownMigrations(connectionString);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migraciones.AppliedMigrationsAsync(connectionString, cancellationToken);

    /// <inheritdoc />
    public Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migraciones.ApplyMigrationsAsync(connectionString, cancellationToken);

    public string DisplayName => "Solicitudes B2B y Especiales";
    public string Description =>
        "Encargos personalizados y pedidos por volumen, con bandeja de estados y cotizaciones.";
    public string Version => "1.0.0";
    public int DisplayOrder => 70;
    public string[] HardDependencies => ["core", "catalog", "crm"];
    public string[] SoftDependencies => [];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.AddDbContext<B2bDbContext>(options => PersistenciaDeModulo.Configurar(
            options, connectionString, B2bDbContext.Schema, B2bDbContext.MigrationsHistoryTable));

        // El límite por cuenta: parámetro comercial, configurable sin tocar código.
        var maximo = configuration.GetValue("B2b:LimiteSolicitudes:Maximo", LimiteDeSolicitudes.PorDefecto.Maximo);
        var minutos = configuration.GetValue("B2b:LimiteSolicitudes:VentanaMinutos", (int)LimiteDeSolicitudes.PorDefecto.Ventana.TotalMinutes);
        services.AddSingleton(new LimitePorCuenta(new LimiteDeSolicitudes(maximo, TimeSpan.FromMinutes(minutos))));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SolicitudesService>();
    }

    /// <summary>Monta las rutas de cliente. Las de administración llegan en el siguiente tramo.</summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSolicitudesClienteEndpoints();
    }
}
