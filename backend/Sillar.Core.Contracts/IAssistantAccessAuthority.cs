namespace Sillar.Core.Contracts;

/// <summary>
/// Candidate A-only contract. Identity is resolved by CORE, never supplied by HTTP.
/// Approval is reserved for a future ratified policy; A implementations fail closed.
/// </summary>
public interface IAssistantAccessAuthority
{
    Task<AssistantAccessDecision> EvaluateCurrentInstallationAsync(CancellationToken cancellationToken);
}

public enum AssistantAccessOutcome
{
    Denied,
    PendingDecision,
    Allowed
}

public enum AssistantAccessReason
{
    InstallationMissing,
    InstallationIncomplete,
    InstallationIdentityInvalid,
    LicenseExpired,
    LicenseUnrecognized,
    ModuleMissing,
    ModuleInactive,
    ModuleExpired,
    EntitlementPolicyUnratified,
    AuthorityUnavailable,
    AuthorizedByRatifiedPolicy
}

/// <param name="InstallationKey">Internal persisted identity, NOT a public identifier or credential.</param>
/// <param name="Source">Provenance of observed facts, not evidence of a finalized entitlement.</param>
public sealed record AssistantAccessDecision(
    AssistantAccessOutcome Outcome,
    AssistantAccessReason Reason,
    Guid? InstallationKey,
    string Source,
    DateTimeOffset ObservedAt)
{
    public bool IsAllowed => Outcome == AssistantAccessOutcome.Allowed;
}
