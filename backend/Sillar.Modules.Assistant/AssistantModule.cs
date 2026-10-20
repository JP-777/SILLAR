using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Assistant.Composition;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Assistant;

/// <summary>
/// M12 — Asistente y conocimiento editorial. Preparado para el descubrimiento modular.
/// No registra migraciones, no monta la administración editorial y no se añade al host.
/// </summary>
public sealed class AssistantModule : IModule
{
    public const string ModuleCode = "assistant";
    public string Code => ModuleCode;
    public string? Schema => AssistantDbContext.Schema;
    public string DisplayName => "Asistente y conocimiento";
    public string Description =>
        "Preguntas frecuentes respaldadas por artículos publicados y administración editorial versionada.";
    public string Version => "0.1.0";
    public int DisplayOrder => 120;
    public string[] HardDependencies => ["core"];
    public string[] SoftDependencies => [];

    /// <summary>
    /// Registra componentes internos solo si el host ya aportó una fuente confiable de
    /// instalación, la política editorial concreta y una conexión configurada.
    /// No inventa ni acepta identidad mediante encabezados de una petición.
    /// </summary>
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("M12 requiere ConnectionStrings:Default del host.");

        if (!services.Any(x => x.ServiceType == typeof(ITrustedAssistantInstallationScope)))
            throw new InvalidOperationException(
                "M12 requiere un ITrustedAssistantInstallationScope proporcionado por el host.");

        var policies = services.Where(x => x.ServiceType == typeof(EditorialPersistencePolicy)).ToArray();
        if (policies.Length != 1 || policies[0].ImplementationInstance is not EditorialPersistencePolicy policy)
            throw new InvalidOperationException(
                "M12 requiere exactamente una política editorial explícita y confiable del host.");

        if (services.Any(x => x.ServiceType == typeof(AssistantDbContext)))
            throw new InvalidOperationException("M12 ya tiene un AssistantDbContext registrado.");

        services.AddAssistantInternalServices(
            options => options.UseNpgsql(connectionString, pg =>
                pg.MigrationsHistoryTable(AssistantDbContext.MigrationsHistoryTable, AssistantDbContext.Schema)),
            policy);
    }

    /// <summary>
    /// Por ahora solo expone FAQ de lectura. La edición HTTP permanece sin montar
    /// hasta incorporar auditoría confiable de las escrituras e identidad administrativa.
    /// </summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapAssistantFaqEndpoints();
    }
}
