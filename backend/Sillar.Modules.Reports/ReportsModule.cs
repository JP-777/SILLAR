using Sillar.Modules.Reports.Data;
using Sillar.Shared.Data.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Reports.Endpoints;
using Sillar.Modules.Reports.Orders;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Reports;

/// <summary>M10 — Reportes; solo lectura, con historial local de instalación.</summary>
public sealed class ReportsModule : IModule, IModuleMigrations
{
    private static readonly MigracionesDeContexto<ReportsDbContext> Migrations = new(
        connection => new ReportsDbContext(PersistenciaDeModulo.Opciones<ReportsDbContext>(
            connection, ReportsDbContext.Schema, ReportsDbContext.MigrationsHistoryTable)),
        ReportsDbContext.MigrationsHistoryTable);
    public string MigrationsHistoryTable => Migrations.MigrationsHistoryTable;
    public IReadOnlyList<string> KnownMigrations(string connectionString) => Migrations.KnownMigrations(connectionString);
    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken ct)
        => Migrations.AppliedMigrationsAsync(connectionString, ct);
    public Task ApplyMigrationsAsync(string connectionString, CancellationToken ct)
        => Migrations.ApplyMigrationsAsync(connectionString, ct);

    /// <summary>Código público del módulo.</summary>
    public const string ModuleCode = "reporting";

    /// <inheritdoc />
    public string Code => ModuleCode;

    /// <inheritdoc />
    public string? Schema => ReportsDbContext.Schema;

    /// <inheritdoc />
    public string DisplayName => "Reportes";

    /// <inheritdoc />
    public string Description => "Indicadores agregados del negocio, sin exponer datos personales.";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public int DisplayOrder => 100;

    /// <inheritdoc />
    public string[] HardDependencies => ["core"];

    /// <inheritdoc />
    public string[] SoftDependencies => ["sales"];

    public IReadOnlyList<ModuleOperation> Operations =>
        [new("read", "Consultar reportes", "admin"), new("export", "Exportar indicadores", "admin")];

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        => services.AddOrderIndicatorsReport();

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrderIndicatorsEndpoints();
        endpoints.MapMonetaryIndicatorsEndpoints();
        endpoints.MapCoreSeamEndpoints();
        endpoints.MapOrderIndicatorsCsvExport();
    }
}
