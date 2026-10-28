namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Resuelve una sola evaluación autoritativa asíncrona y publica una foto
/// inmutable en el ámbito actual.
/// </summary>
public sealed class AssistantHostEvidenceResolver(
    AssistantAuthorityProbe probe,
    AssistantHostEvidenceScope scope)
{
    private readonly object _gate = new();
    private Task<AssistantHostEvidenceSnapshot>? _resolution;

    public ValueTask<AssistantHostEvidenceSnapshot> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _resolution ??= ResolveOnceAsync(cancellationToken);
            return new ValueTask<AssistantHostEvidenceSnapshot>(_resolution);
        }
    }

    private async Task<AssistantHostEvidenceSnapshot> ResolveOnceAsync(
        CancellationToken cancellationToken)
    {
        AssistantHostEvidenceSnapshot snapshot;

        try
        {
            var observation = await probe(cancellationToken);
            snapshot = Project(observation);
        }
        catch (OperationCanceledException)
        {
            snapshot = AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.AuthorityCancelled);
        }
        catch (Exception)
        {
            snapshot = AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.AuthorityUnavailable);
        }

        scope.Publish(snapshot);
        return snapshot;
    }

    private static AssistantHostEvidenceSnapshot Project(
        AssistantAuthorityObservation? observation)
    {
        if (observation is null)
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.AuthorityUnavailable);
        }

        var trace = new AssistantAuthorityTrace(
            observation.Verdict,
            observation.InstallationIdentity,
            observation.Reason,
            observation.Source,
            observation.ObservedAt,
            observation.ValidUntil,
            observation.AuthorityInstallationKey);

        if (!ValidTrace(observation))
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.AuthorityUnavailable,
                trace);
        }

        if (observation.Verdict == AssistantAuthorityVerdict.PendingDecision)
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.PendingDecision,
                trace);
        }

        if (observation.Verdict != AssistantAuthorityVerdict.Allowed)
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.AuthorityDenied,
                trace);
        }

        if (observation.InstallationIdentity == AssistantInstallationIdentityEvidence.Missing)
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.InstallationIdentityInvalid,
                trace);
        }

        // CORE puede haber observado una clave interna, pero JP todavía no ha
        // atestado una identidad consumible por M12. Nunca se renombra esa clave
        // como InstallationId ni se produce evidencia positiva en FASE A.
        return AssistantHostEvidenceSnapshot.Denied(
            AssistantHostDenialReason.InstallationIdentityUnattested,
            trace);
    }

    private static bool ValidTrace(AssistantAuthorityObservation observation)
        => !string.IsNullOrWhiteSpace(observation.Reason)
           && !string.IsNullOrWhiteSpace(observation.Source)
           && observation.ObservedAt != default
           && (observation.ValidUntil is null || observation.ValidUntil > observation.ObservedAt)
           && (observation.InstallationIdentity == AssistantInstallationIdentityEvidence.Missing
               ? observation.AuthorityInstallationKey is null
               : observation.AuthorityInstallationKey is { } key && key != Guid.Empty);
}
