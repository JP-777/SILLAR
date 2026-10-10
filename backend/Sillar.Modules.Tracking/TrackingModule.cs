using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Tracking.Application;
using Sillar.Modules.Tracking.Contracts;
using Sillar.Modules.Tracking.Data;
using Sillar.Modules.Tracking.Endpoints;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking;

public sealed class TrackingModule : IModule, IModuleMigrations
{
    public const string ModuleCode = "tracking";
    public const string ConnectionStringName = "Default";

    private static readonly MigracionesDeContexto<TrackingDbContext> Migrations = new(
        connection => new TrackingDbContext(
            PersistenciaDeModulo.Opciones<TrackingDbContext>(
                connection,
                TrackingDbContext.Schema,
                TrackingDbContext.MigrationsHistoryTable),
            new NodeIdentity(NodeIdentity.DefaultCode),
            TimeProvider.System),
        TrackingDbContext.MigrationsHistoryTable);

    public string Code => ModuleCode;
    public string DisplayName => "Seguimiento";
    public string Description => "Tablero, prioridad, plazos internos y notas de seguimiento de órdenes de servicio.";
    public string Version => "1.0.0";
    public int DisplayOrder => 60;

    public string[] HardDependencies => ["core", "service_orders"];
    public string[] SoftDependencies => [];

    public string MigrationsHistoryTable => Migrations.MigrationsHistoryTable;

    public IReadOnlyList<string> KnownMigrations(string connectionString)
        => Migrations.KnownMigrations(connectionString);

    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(
        string connectionString,
        CancellationToken cancellationToken)
        => Migrations.AppliedMigrationsAsync(connectionString, cancellationToken);

    public Task ApplyMigrationsAsync(
        string connectionString,
        CancellationToken cancellationToken)
        => Migrations.ApplyMigrationsAsync(connectionString, cancellationToken);

    public void RegisterServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.TryAddNodeIdentity(configuration);

        services.AddDbContext<TrackingDbContext>(options =>
            PersistenciaDeModulo.Configurar(
                options,
                connection,
                TrackingDbContext.Schema,
                TrackingDbContext.MigrationsHistoryTable));

        services.AddScoped<TrackingApplicationService>();
        services.AddScoped<ICustomerTrackingProgress, CustomerTrackingProgressService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTrackingEndpoints();
    }
}
