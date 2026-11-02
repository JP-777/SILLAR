using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Opt-in de composición negativa M03–M11. <see cref="PaymentsModule"/> no lo llama: solo
/// una composición de Host aislada, con su propia barrera, decide invocarlo.
/// </summary>
/// <remarks>
/// <para>
/// Conecta tres puertos de M11 con sus implementaciones reales y nada más: la evaluación
/// sin efectos de M03 (que debe venir de <c>AddCandidateSalesReadOnlyEvaluation</c>), la
/// correlación candidata de M03 (que debe venir de <c>AddCandidateSalesOrderCorrelation</c>)
/// y el puerto de efectos negativo, que solo produce Rejected, DecisionRequired, Conflict
/// o Indeterminate.
/// </para>
/// <para>
/// No registra fuentes de declaración autenticada, verificadores de proveedor ni fuentes
/// de correlación autorizada: no hay productor real de ninguno, y fabricarlos sería crear
/// una autoridad. Por eso la correlación del inbox sigue siendo la barrera de FASE A.
/// </para>
/// </remarks>
public static class PaymentsNegativeM03Composition
{
    /// <summary>Registra el puente negativo M03–M11, scoped, sin tocar ningún otro registro.</summary>
    public static IServiceCollection AddPaymentsNegativeM03Bridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IM03CommercialReadOnlyEvaluationPort, M03CommercialReadOnlyEvaluationPortAdapter>();
        services.TryAddScoped<IM03OrderCorrelationCandidatePort, M03OrderCorrelationCandidatePortAdapter>();
        services.TryAddScoped<IProviderCommercialEffectPort, M03ReadOnlyNegativeEffectPortAdapter>();
        return services;
    }

    /// <summary>
    /// Retira la recepción de proveedor, que <see cref="PaymentsModule"/> registra pero no
    /// puede construirse: exige <c>IProviderEvidenceVerifier</c> y no existe ninguno.
    /// Autenticar a un proveedor no se simula. Devuelve los nombres retirados.
    /// </summary>
    public static IReadOnlyList<string> RemoveReceptionWithoutVerifier(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Type[] retirados = [typeof(IProviderReceptionService), typeof(ProviderReceptionOrchestrator)];
        foreach (var tipo in retirados)
        {
            services.RemoveAll(tipo);
        }

        return [.. retirados.Select(tipo => tipo.Name)];
    }
}
