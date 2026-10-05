using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Services.Contracts;
using Sillar.Modules.Services.Data;
using Sillar.Modules.Services.Endpoints;
using Sillar.Modules.Services.Services;
using Sillar.Shared.Data;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Services;
public sealed class ServicesModule : IModule, IModuleMigrations
{
    public const string ModuleCode = "services";
    private static readonly MigracionesDeContexto<ServicesDbContext> Migrations = new(
        connection => new ServicesDbContext(PersistenciaDeModulo.Opciones<ServicesDbContext>(connection, ServicesDbContext.Schema, ServicesDbContext.MigrationsHistoryTable)),
        ServicesDbContext.MigrationsHistoryTable);
    public string Code => ModuleCode;
    public string DisplayName => "Servicios — Vitrina";
    public string Description => "Vitrina pública y administración editorial de servicios permanentes.";
    public string Version => "1.0.0";
    public int DisplayOrder => 50;
    public string[] HardDependencies => ["core"];
    public string[] SoftDependencies => [];
    public string MigrationsHistoryTable => Migrations.MigrationsHistoryTable;
    public IReadOnlyList<string> KnownMigrations(string connectionString) => Migrations.KnownMigrations(connectionString);
    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken ct) => Migrations.AppliedMigrationsAsync(connectionString, ct);
    public Task ApplyMigrationsAsync(string connectionString, CancellationToken ct) => Migrations.ApplyMigrationsAsync(connectionString, ct);
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.");
        services.AddDbContext<ServicesDbContext>(options => PersistenciaDeModulo.Configurar(options, connection, ServicesDbContext.Schema, ServicesDbContext.MigrationsHistoryTable));
        services.AddScoped<ServiceShowcaseService>();
        services.AddScoped<IServiceShowcaseSnapshots>(provider => provider.GetRequiredService<ServiceShowcaseService>());
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapServiceEndpoints();
}
