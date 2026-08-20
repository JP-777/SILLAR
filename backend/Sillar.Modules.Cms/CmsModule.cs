using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Cms.Data;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Cms;

/// <summary>M02 — Contenido Web.</summary>
public sealed class CmsModule : IModule
{
    public const string ModuleCode = "cms";
    public const string ConnectionStringName = "Default";
    public string Code => ModuleCode;
    public string DisplayName => "Contenido Web";
    public string Description =>
        "Banners, promociones, productos destacados, trabajos y redes sociales para una web administrable.";
    public string Version => "1.0.0";
    public int DisplayOrder => 20;
    public string[] HardDependencies => ["core"];
    public string[] SoftDependencies => ["catalog"];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.AddDbContext<CmsDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                CmsDbContext.MigrationsHistoryTable,
                CmsDbContext.Schema)));
    }

    /// <remarks>El paso 2 solo entrega datos; M02 todavía no monta rutas.</remarks>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
