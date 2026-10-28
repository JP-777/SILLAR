using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Simulation;

/// <summary>Respuesta controlada de una dependencia simulada de evidencia.</summary>
public enum SimulatedProviderEvidenceOutcome
{
    Insufficient,
    Rejected,
    Attested
}

/// <summary>
/// Observación producida por un doble. No representa firma, autenticación ni
/// una respuesta obtenida de un proveedor real.
/// </summary>
public sealed record SimulatedProviderEvidenceObservation(
    SimulatedProviderEvidenceOutcome Outcome,
    string? AttestationReference);

/// <summary>
/// Costura exclusiva del arnés para inyectar evidencia simulada y reproducible.
/// </summary>
/// <remarks>
/// Una implementación real de verificación no debe implementar este contrato:
/// el tipo expresa deliberadamente simulación y no concede confianza.
/// </remarks>
public interface IProviderEvidenceObservationSimulator
{
    SimulatedProviderEvidenceObservation Observe(
        VerifiedExternalPaymentEvidence evidence);
}

/// <summary>Resultado completo y autoconsistente de una corrida simulada.</summary>
public sealed record ProviderSettlementSimulationResult(
    IReadOnlyList<ProviderEvidenceObservation> SimulatedObservations,
    ProviderSettlementAnalysisResult Analysis,
    ProviderSettlementAnalysisFingerprint Integrity,
    ProviderSettlementReviewCasebookResult Casebook);

/// <summary>
/// Ejecuta el núcleo puro de extremo a extremo con una dependencia de evidencia
/// simulada. No registra pagos, no persiste, no autentica y no llama a M03.
/// </summary>
public sealed class ProviderSettlementSimulationHarness
{
    private readonly IProviderEvidenceObservationSimulator _simulator;

    public ProviderSettlementSimulationHarness(
        IProviderEvidenceObservationSimulator simulator)
    {
        _simulator = simulator
            ?? throw new ArgumentNullException(nameof(simulator));
    }

    public ProviderSettlementSimulationResult Run(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);

        var arrivals = incoming.ToArray();
        var history = known.ToArray();

        if (arrivals.Any(item => item is null))
        {
            throw new ArgumentException(
                "La simulación no admite evidencia entrante nula.",
                nameof(incoming));
        }

        if (history.Any(item => item is null))
        {
            throw new ArgumentException(
                "La simulación no admite evidencia histórica nula.",
                nameof(known));
        }

        var observations = arrivals
            .Select(CreateObservation)
            .ToArray();
        var analysis = ProviderSettlementAnalysisPipeline.Analyze(
            arrivals,
            history,
            observations);
        var integrity = ProviderSettlementAnalysisIntegrity.Create(analysis);
        var casebook = ProviderSettlementReviewCasebook.Create(analysis);

        return new ProviderSettlementSimulationResult(
            Array.AsReadOnly(observations),
            analysis,
            integrity,
            casebook);
    }

    private ProviderEvidenceObservation CreateObservation(
        VerifiedExternalPaymentEvidence evidence)
    {
        var simulated = _simulator.Observe(evidence)
            ?? throw new InvalidOperationException(
                "La dependencia simulada devolvió una observación nula.");

        if (!Enum.IsDefined(simulated.Outcome))
        {
            throw new InvalidOperationException(
                "La dependencia simulada devolvió un resultado desconocido.");
        }

        if (simulated.Outcome == SimulatedProviderEvidenceOutcome.Attested)
        {
            if (string.IsNullOrWhiteSpace(simulated.AttestationReference))
            {
                throw new InvalidOperationException(
                    "Una atestación simulada exige una referencia no vacía.");
            }
        }
        else if (simulated.AttestationReference is not null)
        {
            throw new InvalidOperationException(
                "Una observación simulada no atestada no admite referencia.");
        }

        var verdict = simulated.Outcome switch
        {
            SimulatedProviderEvidenceOutcome.Insufficient =>
                ProviderEvidenceVerdict.Unverified,
            SimulatedProviderEvidenceOutcome.Rejected =>
                ProviderEvidenceVerdict.RejectedByVerifier,
            SimulatedProviderEvidenceOutcome.Attested =>
                ProviderEvidenceVerdict.AttestedByVerifier,
            _ => throw new InvalidOperationException(
                "La observación simulada no tiene proyección de evidencia.")
        };

        return new ProviderEvidenceObservation(
            PaymentIdempotencyKeys.From(evidence),
            verdict,
            simulated.AttestationReference);
    }
}
