using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Endpoints;
using Sillar.Core.Modularity;
using Sillar.Shared.Modularity;

namespace Sillar.Core;

/// <summary>
/// Módulo núcleo. Está siempre presente y siempre activo: si CORE no arranca,
/// no arranca nada.
/// </summary>
public sealed class CoreModule : IModule
{
    /// <summary>Código del módulo, para etiquetar auditoría y medios.</summary>
    public const string ModuleCode = ModuleGraph.CoreCode;

    /// <summary>Nombre de la cadena de conexión en la configuración.</summary>
    public const string ConnectionStringName = "Default";

    /// <inheritdoc />
    public string Code => ModuleCode;

    /// <inheritdoc />
    public string DisplayName => "Núcleo de plataforma";

    /// <inheritdoc />
    public string Description =>
        "Identidad de la instalación, catálogo de módulos y su activación, usuarios " +
        "administradores, autenticación, configuración del sitio, gestión de archivos y auditoría.";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public int DisplayOrder => 0;

    /// <inheritdoc />
    /// <remarks>CORE no depende de nadie. Todos dependen de él.</remarks>
    public string[] HardDependencies => [];

    /// <inheritdoc />
    public string[] SoftDependencies => [];

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'. " +
                "Se define en .env como ConnectionStrings__Default.");
        }

        services.AddCoreEssentials(configuration, connectionString);
        services.AddCoreAuthentication();

        // La foto de activaciones la deja el host en el contenedor antes de
        // llamar aquí; el registro es la lectura de esa foto que ven los demás
        // módulos a través del contrato.
        services.AddSingleton<IModuleRegistry, ModuleRegistry>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCapabilitiesEndpoints();
        endpoints.MapAuthEndpoints();
        endpoints.MapAdminUserEndpoints();
        endpoints.MapAdminModuleEndpoints();
        endpoints.MapSettingsEndpoints();
        endpoints.MapAuditEndpoints();
    }
}
