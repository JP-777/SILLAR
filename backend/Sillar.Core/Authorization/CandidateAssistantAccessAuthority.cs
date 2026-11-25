using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Authorization;

/// <summary>
/// Read-through, request-scoped candidate. Never consults the boot-time IModuleRegistry
/// as an authorization authority. Does not grant access in FASE A.
/// </summary>
internal sealed class CandidateAssistantAccessAuthority(CoreDbContext database, TimeProvider clock)
    : IAssistantAccessAuthority
{
    public async Task<AssistantAccessDecision> EvaluateCurrentInstallationAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        AssistantAccessDecision Deny(AssistantAccessReason reason, Guid? key = null) =>
            new(AssistantAccessOutcome.Denied, reason, key, "core.current_database", now);

        try
        {
            var installation = await database.Installations.AsNoTracking()
                .Select(i => new { i.InstallationKey, i.IsSetupComplete, i.LicenseType, i.LicensedUntil })
                .SingleOrDefaultAsync(cancellationToken);
            if (installation is null) return Deny(AssistantAccessReason.InstallationMissing);
            if (!installation.IsSetupComplete) return Deny(AssistantAccessReason.InstallationIncomplete);
            if (installation.InstallationKey == Guid.Empty)
                return Deny(AssistantAccessReason.InstallationIdentityInvalid);

            var key = installation.InstallationKey;
            if (!LicenseType.All.Contains(installation.LicenseType, StringComparer.Ordinal))
                return Deny(AssistantAccessReason.LicenseUnrecognized, key);
            if (installation.LicensedUntil is { } until && until <= now)
                return Deny(AssistantAccessReason.LicenseExpired, key);
            // Do NOT infer unlimited rights from perpetual or absence of expiration:
            // neither an entitlement policy nor a granular CAS policy was ratified.

            var module = await database.Modules.AsNoTracking()
                .Where(m => m.Code == "assistant")
                .Select(m => new
                {
                    Active = m.Activation != null && m.Activation.IsActive,
                    ExpiresAt = m.Activation != null ? m.Activation.ExpiresAt : null
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (module is null) return Deny(AssistantAccessReason.ModuleMissing, key);
            if (!module.Active) return Deny(AssistantAccessReason.ModuleInactive, key);
            if (module.ExpiresAt is { } expires && expires <= now)
                return Deny(AssistantAccessReason.ModuleExpired, key);

            return new AssistantAccessDecision(
                AssistantAccessOutcome.PendingDecision,
                AssistantAccessReason.EntitlementPolicyUnratified,
                key,
                "core.current_database;entitlement_policy=unratified", now);
        }
        catch (Exception)
        {
            // Includes provider failure and timeout; never convert missing proof to allow.
            return Deny(AssistantAccessReason.AuthorityUnavailable);
        }
    }
}

/// <summary>
/// Explicit opt-in only. Do not invoke in CoreModule/Host production registration
/// until cross-owner review and JP's later gate.
/// </summary>
public static class CandidateAssistantAuthorityRegistration
{
    public static IServiceCollection AddCandidateAssistantAuthority(this IServiceCollection services)
    {
        services.AddScoped<IAssistantAccessAuthority, CandidateAssistantAccessAuthority>();
        return services;
    }
}
