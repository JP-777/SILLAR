using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Numbering;
using Sillar.Modules.ServiceOrders.Services;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders;

/// <summary>M05b — recepción y estado durable de órdenes de servicios.</summary>
public sealed class ServiceOrdersModule : IModule, IModuleMigrations
{
    public const string ModuleCode = "service_orders";
    public const string ConnectionStringName = "Default";

    private static readonly MigracionesDeContexto<ServiceOrdersDbContext> Migrations = new(
        connection => new ServiceOrdersDbContext(
            PersistenciaDeModulo.Opciones<ServiceOrdersDbContext>(
                connection,
                ServiceOrdersDbContext.Schema,
                ServiceOrdersDbContext.MigrationsHistoryTable),
            new NodeIdentity(NodeIdentity.DefaultCode),
            TimeProvider.System),
        ServiceOrdersDbContext.MigrationsHistoryTable);

    public string Code => ModuleCode;
    public string DisplayName => "Servicios — Órdenes";
    public string Description => "Recepción, detalle congelado, asignación y estado durable de órdenes de servicio.";
    public string Version => "1.0.0";
    public int DisplayOrder => 55;
    public string[] HardDependencies => ["core", "services"];
    public string[] SoftDependencies => ["crm"];
    public string MigrationsHistoryTable => Migrations.MigrationsHistoryTable;
    public IReadOnlyList<string> KnownMigrations(string connectionString) => Migrations.KnownMigrations(connectionString);
    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migrations.AppliedMigrationsAsync(connectionString, cancellationToken);
    public Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migrations.ApplyMigrationsAsync(connectionString, cancellationToken);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión '{ConnectionStringName}'.");
        services.TryAddNodeIdentity(configuration);
        services.AddDbContext<ServiceOrdersDbContext>(options => PersistenciaDeModulo.Configurar(
            options,
            connection,
            ServiceOrdersDbContext.Schema,
            ServiceOrdersDbContext.MigrationsHistoryTable));
        services.AddScoped<ServiceOrderCodeAllocator>();
        services.AddScoped<IServiceOrderTransitions, ServiceOrderTransitionService>();
    }

    // Paso 2 publica persistencia y contratos. Los endpoints se incorporan en Paso 3.
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
