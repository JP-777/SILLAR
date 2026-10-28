using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Assistant.Composition;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Shared.Modularity;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.Assistant;

/// <summary>
/// M12 — Asistente y conocimiento editorial. Preparado para el descubrimiento modular.
/// Declara sus migraciones propias de assistant. No monta administración editorial ni integra el host.
/// </summary>
public sealed class AssistantModule : IModule, IModuleMigrations
{
    private static readonly MigracionesDeContexto<AssistantDbContext> Migraciones = new(
        cs => new AssistantDbContext(PersistenciaDeModulo.Opciones<AssistantDbContext>(
            cs, AssistantDbContext.Schema, AssistantDbContext.MigrationsHistoryTable)),
        AssistantDbContext.MigrationsHistoryTable);
    public string MigrationsHistoryTable => Migraciones.MigrationsHistoryTable;
    public IReadOnlyList<string> KnownMigrations(string connectionString) => Migraciones.KnownMigrations(connectionString);
    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken ct) => Migraciones.AppliedMigrationsAsync(connectionString, ct);
    public Task ApplyMigrationsAsync(string connectionString, CancellationToken ct) => Migraciones.ApplyMigrationsAsync(connectionString, ct);

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
    /// No monta rutas mientras CORE no pueda acreditar un Allowed legítimo y su
    /// vinculación con la identidad M12. ADR-004 exige ausencia de rutas sin licencia.
    /// FAQ y edición conservan handlers aislados para completar sus contratos.
    /// </summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        // La composición productiva de Host/CORE todavía no acredita entitlement.
    }
}
