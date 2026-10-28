using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sillar.Host.AssistantBridge;

/// <summary>Composición candidata opt-in; ninguna entrada productiva la invoca.</summary>
public static class AssistantHostServiceCollectionExtensions
{
    /// <summary>
    /// Registra solo la foto denegada, su adaptador síncrono y CAS deny-all.
    /// Es la composición segura cuando todavía no existe un productor CORE.
    /// </summary>
    public static IServiceCollection AddAssistantHostCandidates(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<AssistantHostEvidenceScope>();
        services.TryAddScoped<AssistantHostEvidenceAdapter>();
        services.TryAddScoped<DenyAllAssistantQuotaPolicyWriter>();
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
