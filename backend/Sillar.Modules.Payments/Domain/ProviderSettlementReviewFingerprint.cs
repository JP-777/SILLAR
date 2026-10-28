using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Identificador reproducible del contenido descriptivo de una auditoría P8.
/// </summary>
/// <remarks>
/// <see cref="ContentSha256"/> no es el hash del cuerpo de un webhook, una
/// firma, una atestación, una recepción durable ni una autorización de pago.
/// Sólo permite comparar dos proyecciones puras construidas con este formato.
/// </remarks>
public sealed record ProviderReviewAuditFingerprint(
    string Format,
    string ContentSha256,
    int TotalGroups,
    int HeldGroups,
    int CrossProviderIdentityHolds,
    int EvidenceGroups,
    int KnownGroups,
    int SeparateDurableReviewGroups);

/// <summary>
/// Valida un resultado P8 completo y calcula una huella canónica independiente
/// del orden de las colecciones recibidas.
/// </summary>
public static class ProviderSettlementReviewFingerprint
{
    public const string CurrentFormat = "m11-provider-review-audit/v1";

    public static ProviderReviewAuditFingerprint Create(
        ProviderReviewAuditResult audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(audit.Rows);
        ArgumentNullException.ThrowIfNull(audit.ByProvider);

        var rows = audit.Rows.ToArray();
        ValidateRows(rows);

        var orderedRows = rows
            .OrderBy(row => row.Provider, StringComparer.Ordinal)
            .ThenBy(row => row.ExternalSettlementId, StringComparer.Ordinal)
            .ToArray();

        var held = rows.Count(row =>
            row.Severity == ProviderReviewAuditSeverity.Hold);
        var crossProviderHolds = rows.Count(row =>
            row.OriginalHoldReason ==
                ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved);
        var evidence = rows.Count(row =>
            row.Severity == ProviderReviewAuditSeverity.NeedsEvidence);
        var known = rows.Count(row =>
            row.Severity == ProviderReviewAuditSeverity.PreviouslyKnown);
        var separate = rows.Count(row =>
            row.Severity == ProviderReviewAuditSeverity.SeparateDurableReview);

        if (audit.TotalGroups != rows.Length
            || audit.HeldGroups != held
            || audit.CrossProviderIdentityHolds != crossProviderHolds
            || audit.EvidenceGroups != evidence
            || audit.KnownGroups != known
            || audit.SeparateDurableReviewGroups != separate
            || held + evidence + known + separate != rows.Length)
        {
            throw new ArgumentException(
                "Los totales P8 no coinciden con sus filas.",
                nameof(audit));
        }

        ValidateProviderSummaries(audit.ByProvider, orderedRows);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, CurrentFormat);
        AppendInt32(hash, orderedRows.Length);

        foreach (var row in orderedRows)
        {
            AppendString(hash, row.Provider);
            AppendString(hash, row.ExternalSettlementId);
            AppendInt32(hash, (int)row.Severity);
            AppendInt32(hash, (int)row.OriginalLane);
            AppendInt32(hash, (int)row.OriginalHoldReason);
            AppendInt32(hash, row.EventIdentities);
            AppendInt64(hash, row.IncomingOccurrences);
        }

        var contentSha256 = Convert.ToHexString(
                hash.GetHashAndReset())
            .ToLowerInvariant();

        return new ProviderReviewAuditFingerprint(
            CurrentFormat,
            contentSha256,
            rows.Length,
            held,
            crossProviderHolds,
            evidence,
            known,
            separate);
    }

    private static void ValidateRows(
        IReadOnlyCollection<ProviderReviewAuditRow> rows)
    {
        foreach (var row in rows)
        {
            if (row is null
                || string.IsNullOrWhiteSpace(row.Provider)
                || string.IsNullOrWhiteSpace(row.ExternalSettlementId)
                || row.EventIdentities <= 0
                || row.IncomingOccurrences <= 0
                || !Enum.IsDefined(row.Severity)
                || !Enum.IsDefined(row.OriginalLane)
                || !Enum.IsDefined(row.OriginalHoldReason))
            {
                throw new ArgumentException(
                    "P8 contiene una fila inválida.",
                    nameof(rows));
            }

            var expectedSeverity = row.OriginalLane switch
            {
                ProviderSettlementReviewLane.Hold =>
                    ProviderReviewAuditSeverity.Hold,
                ProviderSettlementReviewLane.RejectedEvidence
                    or ProviderSettlementReviewLane.AwaitIndependentEvidence =>
                    ProviderReviewAuditSeverity.NeedsEvidence,
                ProviderSettlementReviewLane.PreviouslyKnown =>
                    ProviderReviewAuditSeverity.PreviouslyKnown,
                ProviderSettlementReviewLane.RequiresSeparateDurableGate =>
                    ProviderReviewAuditSeverity.SeparateDurableReview,
                _ => throw new ArgumentException(
                    "P8 contiene un carril desconocido.",
                    nameof(rows))
            };

            if (row.Severity != expectedSeverity
                || (row.OriginalLane == ProviderSettlementReviewLane.Hold
                    && row.OriginalHoldReason ==
                        ProviderSettlementReviewHoldReason.None)
                || (row.OriginalLane != ProviderSettlementReviewLane.Hold
                    && row.OriginalHoldReason !=
                        ProviderSettlementReviewHoldReason.None))
            {
                throw new ArgumentException(
                    "La severidad P8 no coincide con su clasificación P7.",
                    nameof(rows));
            }
        }

        if (rows
            .GroupBy(row => (row.Provider, row.ExternalSettlementId))
            .Any(group => group.Skip(1).Any()))
        {
            throw new ArgumentException(
                "P8 contiene identidades compuestas duplicadas.",
                nameof(rows));
        }

        var collidedSettlements = rows
            .GroupBy(row => row.ExternalSettlementId, StringComparer.Ordinal)
            .Where(group => group
                .Select(row => row.Provider)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (rows.Any(row => collidedSettlements.Contains(
                    row.ExternalSettlementId)
                && (row.OriginalLane != ProviderSettlementReviewLane.Hold
                    || row.OriginalHoldReason !=
                        ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved)))
        {
            throw new ArgumentException(
                "P8 liberó una colisión interproveedor.",
                nameof(rows));
        }

        // Una colisión también puede involucrar evidencia histórica de otro
        // proveedor que no genera una segunda fila entrante en P8. Esta
        // proyección no puede reconstruir por sí sola esa fila histórica.
    }

    private static void ValidateProviderSummaries(
        IReadOnlyCollection<ProviderReviewAuditProviderSummary> summaries,
        IReadOnlyCollection<ProviderReviewAuditRow> rows)
    {
        var supplied = summaries.ToArray();
        if (supplied.Any(summary => summary is null
                || string.IsNullOrWhiteSpace(summary.Provider)
                || summary.TotalGroups <= 0
                || summary.HeldGroups < 0
                || summary.EvidenceGroups < 0
                || summary.KnownGroups < 0
                || summary.SeparateDurableReviewGroups < 0
                || summary.IncomingOccurrences <= 0)
            || supplied
                .Select(summary => summary.Provider)
                .Distinct(StringComparer.Ordinal)
                .Count() != supplied.Length)
        {
            throw new ArgumentException(
                "P8 contiene un resumen por proveedor inválido.",
                nameof(summaries));
        }

        var expected = rows
            .GroupBy(row => row.Provider, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new ProviderReviewAuditProviderSummary(
                    group.Key,
                    group.Count(),
                    group.Count(row =>
                        row.Severity == ProviderReviewAuditSeverity.Hold),
                    group.Count(row =>
                        row.Severity == ProviderReviewAuditSeverity.NeedsEvidence),
                    group.Count(row =>
                        row.Severity == ProviderReviewAuditSeverity.PreviouslyKnown),
                    group.Count(row => row.Severity ==
                        ProviderReviewAuditSeverity.SeparateDurableReview),
                    checked(group.Sum(row => row.IncomingOccurrences))),
                StringComparer.Ordinal);

        if (expected.Count != supplied.Length
            || supplied.Any(summary =>
                !expected.TryGetValue(summary.Provider, out var value)
                || value != summary))
        {
            throw new ArgumentException(
                "Los resúmenes P8 no coinciden con sus filas.",
                nameof(summaries));
        }
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
