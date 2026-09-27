using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales;

/// <summary>M03 — Ventas Online.</summary>
public sealed class SalesModule : IModule, IModuleMigrations
{
    /// <summary>Código del módulo, igual que su schema.</summary>
    public const string ModuleCode = "sales";

    /// <summary>Nombre de la cadena de conexión.</summary>
    public const string ConnectionStringName = "Default";

    /// <inheritdoc />
    public string Code => ModuleCode;

    /// <summary>
    /// Sus migraciones, para que el instalador las aplique sin conocer este
    /// módulo. El contexto se construye aquí, con la misma configuración que en
    /// tiempo de ejecución; el nodo y el reloj no intervienen al migrar.
    /// </summary>
    private static readonly MigracionesDeContexto<SalesDbContext> Migraciones = new(
        connectionString => new SalesDbContext(
            PersistenciaDeModulo.Opciones<SalesDbContext>(
                connectionString, SalesDbContext.Schema, SalesDbContext.MigrationsHistoryTable),
            new NodeIdentity(NodeIdentity.DefaultCode),
            TimeProvider.System),
        SalesDbContext.MigrationsHistoryTable);

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

    /// <inheritdoc />
    public string DisplayName => "Ventas Online";

    /// <inheritdoc />
    public string Description =>
        "Carrito, pedidos y pago manual. Recojo en tienda: no hay entrega a domicilio.";

    /// <inheritdoc />
    public string Version => "1.0.0";

    // M01=10, M02=20, M04=40. M03 conserva el orden del catálogo modular.
    /// <inheritdoc />
    public int DisplayOrder => 30;

    /// <summary>
    /// Duras: CORE por plataforma, M01 porque no se vende lo que no está
    /// catalogado, y M04 porque comprar exige cuenta —lo que convirtió esa
    /// dependencia en dura el 21 de agosto de 2026—.
    /// </summary>
    public string[] HardDependencies => ["core", "catalog", "crm"];

    /// <summary>
    /// Blandas: M07, que recibe a quien pregunta por un producto «a consultar».
    /// Si no está, la acción no se pinta y <b>nada falla</b>: nunca se lanza una
    /// excepción porque falte una dependencia blanda.
    /// </summary>
    public string[] SoftDependencies => ["b2b"];

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.TryAddNodeIdentity(configuration);

        // Por el mismo sitio que el instalador: ver PersistenciaDeModulo.
        services.AddDbContext<SalesDbContext>(options => PersistenciaDeModulo.Configurar(
            options, connectionString, SalesDbContext.Schema, SalesDbContext.MigrationsHistoryTable));

        services.AddScoped<OrderCodeAllocator>();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Todavía sin endpoints: el paso 3 del ciclo llega después de DATOS. El
    /// módulo ya se descubre, migra y se desinstala.
    /// </remarks>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
