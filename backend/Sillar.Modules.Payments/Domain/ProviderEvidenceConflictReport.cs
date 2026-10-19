namespace Sillar.Modules.Payments.Domain;

/// <summary>Origen de una observación dentro del cotejo puro.</summary>
public enum ProviderEvidencePopulation
{
    Incoming,
    PreviouslyKnown
}

/// <summary>Conflictos deducibles sin elegir proveedor ni regla comercial.</summary>
public enum ProviderEvidenceConflictKind
{
    ReceptionMapsToMultipleSettlements,
    ReceptionHasContradictoryBodies,
    CrossProviderSettlementIdentityUnresolved
}

/// <summary>
/// Evidencia normalizada conservada para explicar un conflicto detectado.
/// </summary>
/// <remarks>
/// Sólo contiene identificadores, SHA-256 y el instante ya presentes en P1.
/// No contiene el cuerpo crudo ni acredita autenticación o persistencia.
/// </remarks>
public sealed record ProviderEvidenceConflictOccurrence(
    ProviderEvidencePopulation Population,
    string Provider,
    string ProviderEventId,
    string ExternalSettlementId,
    string BodySha256,
    DateTimeOffset VerifiedAt);

/// <summary>Explicación determinista de una identidad conflictiva.</summary>
/// <param name="Kind">Tipo cerrado de contradicción observada.</param>
/// <param name="Provider">
/// Proveedor para conflictos de recepción; nulo para una liquidación compartida
/// por proveedores distintos.
/// </param>
/// <param name="IdentityValue">
/// Identificador del evento para conflictos de recepción o identificador de la
/// liquidación para una colisión interproveedor.
/// </param>
/// <param name="Evidence">
/// Ocurrencias completas y ordenadas que fundamentan el conflicto.
/// </param>
public sealed record ProviderEvidenceConflictCase(
    ProviderEvidenceConflictKind Kind,
    string? Provider,
    string IdentityValue,
    IReadOnlyList<ProviderEvidenceConflictOccurrence> Evidence);

/// <summary>Reporte puro; no es un inbox ni una decisión de aplicación.</summary>
public sealed record ProviderEvidenceConflictReportResult(
    IReadOnlyList<ProviderEvidenceConflictCase> Cases,
    int IncomingOccurrencesExamined,
    int KnownOccurrencesExamined)
{
    public bool HasConflicts => Cases.Count > 0;
}

/// <summary>
/// Explica todos los conflictos observables entre el lote y la evidencia
/// conocida, conservando cada ocurrencia que fundamenta el diagnóstico.
/// </summary>
public static class ProviderEvidenceConflictReport
{
    public static ProviderEvidenceConflictReportResult Analyze(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);

        var arrivals = Materialize(
            incoming,
            ProviderEvidencePopulation.Incoming,
            nameof(incoming));
        var history = Materialize(
            known,
            ProviderEvidencePopulation.PreviouslyKnown,
            nameof(known));
        var all = arrivals.Concat(history).ToArray();
        var conflicts = new List<ProviderEvidenceConflictCase>();

        foreach (var reception in all.GroupBy(item => new ReceptionIdentity(
                     item.Provider,
                     item.ProviderEventId)))
        {
            var evidence = OrderEvidence(reception);

            if (reception
                .Select(item => item.ExternalSettlementId)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            {
                conflicts.Add(new ProviderEvidenceConflictCase(
                    ProviderEvidenceConflictKind.ReceptionMapsToMultipleSettlements,
                    reception.Key.Provider,
                    reception.Key.ProviderEventId,
                    evidence));
            }

            if (reception
                .Select(item => item.BodySha256)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            {
                conflicts.Add(new ProviderEvidenceConflictCase(
                    ProviderEvidenceConflictKind.ReceptionHasContradictoryBodies,
                    reception.Key.Provider,
                    reception.Key.ProviderEventId,
                    evidence));
            }
        }

        foreach (var settlement in all
                     .GroupBy(
                         item => item.ExternalSettlementId,
                         StringComparer.Ordinal)
                     .Where(group => group
                         .Select(item => item.Provider)
                         .Distinct(StringComparer.Ordinal)
                         .Skip(1)
                         .Any()))
        {
            conflicts.Add(new ProviderEvidenceConflictCase(
                ProviderEvidenceConflictKind.CrossProviderSettlementIdentityUnresolved,
                null,
                settlement.Key,
                OrderEvidence(settlement)));
        }

        var ordered = conflicts
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Provider ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.IdentityValue, StringComparer.Ordinal)
            .ToArray();

        return new ProviderEvidenceConflictReportResult(
            Array.AsReadOnly(ordered),
            arrivals.Length,
            history.Length);
    }

    private static ProviderEvidenceConflictOccurrence[] Materialize(
        IEnumerable<VerifiedExternalPaymentEvidence> source,
        ProviderEvidencePopulation population,
        string parameterName)
    {
        var materialized = source.ToArray();
        if (materialized.Any(item => item is null))
        {
            throw new ArgumentException(
                "La evidencia no admite entradas nulas.",
                parameterName);
        }

        return materialized
            .Select(item => new ProviderEvidenceConflictOccurrence(
                population,
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt))
            .ToArray();
    }

    private static IReadOnlyList<ProviderEvidenceConflictOccurrence>
        OrderEvidence(IEnumerable<ProviderEvidenceConflictOccurrence> evidence)
    {
        var ordered = evidence
            .OrderBy(item => item.Population)
            .ThenBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ProviderEventId, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ThenBy(item => item.BodySha256, StringComparer.Ordinal)
            .ThenBy(item => item.VerifiedAt)
            .ToArray();

        return Array.AsReadOnly(ordered);
    }

    private sealed record ReceptionIdentity(
        string Provider,
        string ProviderEventId);
}
