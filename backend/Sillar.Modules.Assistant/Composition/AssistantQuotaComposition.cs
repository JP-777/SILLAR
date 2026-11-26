using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Composition;

/// <summary>
/// Registro opt-in de componentes DURABLES de cuotas de Assistant.
/// Solo declara consumidores. No crea permisos ni identifica instalaciones:
/// CORE/Host debe registrar NpgsqlDataSource, ITrustedAssistantHostEvidence
/// e ITrustedAssistantQuotaPolicyWriter antes de resolver estos servicios.
/// No monta endpoints, no conecta IA, no migra la base y no activa el módulo.
/// </summary>
public static class AssistantQuotaComposition
{
    public static IServiceCollection AddAssistantQuotaInternals(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Obligatorio: el ámbito de instalación nace de la evidencia que firma
        // la composición Host, nunca de la solicitud del cliente o un stub local.
        services.AddScoped<HostAttestedAssistantInstallationScope>();
        services.AddScoped<ITrustedAssistantInstallationScope>(provider =>
            provider.GetRequiredService<HostAttestedAssistantInstallationScope>());

        // Son consumidores: su construcción falla si el Host no proporciona
        // el datasource o, para el CAS, el escritor de política confiable.
        // No registrar fallback para ninguna de esas autoridades.
        services.AddScoped<DurableAssistantQuotaLedger>();
        services.AddScoped<DurableAssistantQuotaPolicyStore>();
        return services;
    }
}
