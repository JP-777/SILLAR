using Sillar.Core.Contracts;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Authorization;

/// <summary>
/// Una fila de hechos: la instalación y la activación de <c>assistant</c>, leídas
/// en una sola sentencia.
/// </summary>
internal sealed record AssistantFactsRow(
    Guid InstallationKey,
    bool IsSetupComplete,
    string LicenseType,
    DateTimeOffset? LicensedUntil,
    bool ModuleDeclared,
    bool ActivationPresent,
    bool ActivationIsActive,
    DateTimeOffset? ActivationExpiresAt);

/// <summary>
/// La regla de Fase A, sin base de datos ni reloj propios: recibe los hechos y el
/// instante, y como mucho llega a <see cref="AssistantAccessOutcome.PendingDecision"/>.
/// </summary>
/// <remarks>
/// Ante la duda, deniega. Una licencia temporal sin fecha de fin no se lee como
/// ilimitada, una perpetua con fecha de fin no se resuelve eligiendo una de las
/// dos lecturas, y en el instante exacto de un fin ya está vencida.
/// </remarks>
internal static class AssistantAccessEvaluator
{
    /// <summary>Procedencia de los hechos.</summary>
    public const string Source = "core.current_database";

    /// <summary>Procedencia de una decisión pendiente: los hechos, y que la política no existe.</summary>
    public const string PendingSource = Source + ";entitlement_policy=unratified";

    public static AssistantAccessDecision Evaluate(IReadOnlyList<AssistantFactsRow> rows, DateTimeOffset observedAt)
    {
        if (rows.Count == 0)
        {
            return Deny(AssistantAccessReason.InstallationMissing, observedAt);
        }

        // ck_installation_singleton lo impide en la base. Si aun así llegan dos,
        // no hay una identidad: no se elige una.
        if (rows.Count > 1)
        {
            return Deny(AssistantAccessReason.InstallationIdentityInvalid, observedAt);
        }

        var row = rows[0];
        var facts = new AssistantAccessFacts(
            row.LicenseType,
            row.LicensedUntil,
            row.ModuleDeclared,
            row.ActivationPresent,
            row.ActivationIsActive,
            row.ActivationExpiresAt);

        if (row.InstallationKey == Guid.Empty)
        {
            return Deny(AssistantAccessReason.InstallationIdentityInvalid, observedAt, null, facts);
        }

        var key = row.InstallationKey;

        if (!row.IsSetupComplete)
        {
            return Deny(AssistantAccessReason.InstallationIncomplete, observedAt, key, facts);
        }

        switch (row.LicenseType)
        {
            case LicenseType.Trial or LicenseType.Subscription:
                if (row.LicensedUntil is not { } until)
                {
                    return Deny(AssistantAccessReason.LicenseExpiryMissing, observedAt, key, facts);
                }

                if (observedAt >= until)
                {
                    return Deny(AssistantAccessReason.LicenseExpired, observedAt, key, facts);
                }

                break;

            case LicenseType.Perpetual:
                if (row.LicensedUntil is not null)
                {
                    return Deny(AssistantAccessReason.LicenseInconsistent, observedAt, key, facts);
                }

                break;

            default:
                return Deny(AssistantAccessReason.LicenseUnrecognized, observedAt, key, facts);
        }

        if (!row.ModuleDeclared)
        {
            return Deny(AssistantAccessReason.ModuleMissing, observedAt, key, facts);
        }

        if (!row.ActivationPresent || !row.ActivationIsActive)
        {
            return Deny(AssistantAccessReason.ModuleInactive, observedAt, key, facts);
        }

        if (row.ActivationExpiresAt is { } expires && observedAt >= expires)
        {
            return Deny(AssistantAccessReason.ModuleExpired, observedAt, key, facts);
        }

        // Hechos favorables. Fase A no concede: no hay política ratificada.
        return new AssistantAccessDecision(
            AssistantAccessOutcome.PendingDecision,
            AssistantAccessReason.EntitlementPolicyUnratified,
            key,
            PendingSource,
            observedAt,
            Earliest(row.LicensedUntil, row.ActivationExpiresAt),
            facts);
    }

    /// <summary>La autoridad no se pudo leer.</summary>
    public static AssistantAccessDecision Unavailable(DateTimeOffset observedAt)
        => Deny(AssistantAccessReason.AuthorityUnavailable, observedAt);

    private static AssistantAccessDecision Deny(
        AssistantAccessReason reason,
        DateTimeOffset observedAt,
        Guid? key = null,
        AssistantAccessFacts? facts = null)
        => new(AssistantAccessOutcome.Denied, reason, key, Source, observedAt, null, facts);

    private static DateTimeOffset? Earliest(DateTimeOffset? a, DateTimeOffset? b)
        => (a, b) switch
        {
            ({ } x, { } y) => x <= y ? x : y,
            ({ } x, null) => x,
            (null, { } y) => y,
            _ => null,
        };
}
