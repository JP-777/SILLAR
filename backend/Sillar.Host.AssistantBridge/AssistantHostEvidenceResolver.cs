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

        if (observation.Verdict == AssistantAuthorityVerdict.PendingDecision)
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.PendingDecision);
        }

        if (observation.Verdict != AssistantAuthorityVerdict.Allowed)
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.AuthorityDenied);
        }

        if (observation.InstallationId is not { } installationId
            || installationId == Guid.Empty)
        {
            return AssistantHostEvidenceSnapshot.Denied(
                AssistantHostDenialReason.InstallationIdentityInvalid);
        }

        return AssistantHostEvidenceSnapshot.Allowed(installationId);
    }
}
