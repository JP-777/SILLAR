using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Host.AssistantBridge;

/// <summary>Composición candidata opt-in; ninguna entrada productiva la invoca.</summary>
public static class AssistantHostServiceCollectionExtensions
{
    /// <summary>
    /// Registra la foto denegada y los contratos consumidores reales de M12.
    /// El ámbito de instalación continúa fail-closed y CAS siempre deniega.
    /// No registra política editorial, actor, autoridad revocable, commit gate,
    /// persistencia ni rutas.
    /// </summary>
    public static IServiceCollection AddAssistantHostCandidates(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<AssistantHostEvidenceScope>();
        services.TryAddScoped<AssistantHostEvidenceAdapter>();
        services.TryAddScoped<ITrustedAssistantHostEvidence>(provider =>
            provider.GetRequiredService<AssistantHostEvidenceAdapter>());
        services.TryAddScoped<HostAttestedAssistantInstallationScope>();
        services.TryAddScoped<ITrustedAssistantInstallationScope>(provider =>
            provider.GetRequiredService<HostAttestedAssistantInstallationScope>());
        services.TryAddScoped<DenyAllAssistantQuotaPolicyWriter>();
        services.TryAddScoped<ITrustedAssistantQuotaPolicyWriter>(provider =>
            provider.GetRequiredService<DenyAllAssistantQuotaPolicyWriter>());
        return services;
    }

    /// <summary>
    /// Añade una sonda autoritativa explícita y el resolvedor scoped. No existe
    /// sobrecarga que derive autoridad de configuración o de una petición HTTP.
    /// </summary>
    public static IServiceCollection AddAssistantHostAuthorityProbe(
        this IServiceCollection services,
        AssistantAuthorityProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        return services.AddAssistantHostAuthorityProbe(_ => probe);
    }

    /// <summary>
    /// Añade una fábrica scoped para poder adaptar una autoridad CORE scoped sin
    /// capturarla en el contenedor raíz.
    /// </summary>
    public static IServiceCollection AddAssistantHostAuthorityProbe(
        this IServiceCollection services,
        Func<IServiceProvider, AssistantAuthorityProbe> probeFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(probeFactory);

        services.AddAssistantHostCandidates();
        services.TryAddScoped(probeFactory);
        services.TryAddScoped<AssistantHostEvidenceResolver>();
        return services;
    }
}
