using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Authorization;

/// <summary>
/// Los hechos de una evaluación editorial, leídos en una sola sentencia: la sesión
/// con su cuenta, la instalación y la activación de <c>assistant</c>.
/// </summary>
internal sealed record EditorialFactsRow(
    Guid SessionId,
    int SessionAdminUserId,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? RevokedAt,
    bool AccountIsActive,
    string AccountRole,
    string AccountHomeNode,
    int InstallationCount,
    Guid? InstallationKey,
    bool? InstallationSetupComplete,
    string? LicenseType,
    DateTimeOffset? LicensedUntil,
    bool ModuleDeclared,
    bool ActivationPresent,
    bool ActivationIsActive,
    DateTimeOffset? ActivationExpiresAt);

/// <summary>
/// La regla editorial de Fase A, sin base ni reloj propios. Siempre deniega.
/// </summary>
internal static class AssistantEditorialEvaluator
{
    internal const int ArticleIdMaxLength = 60;

    public static EditorialAuthorityDecision Evaluate(
        EditorialAuthorityRequest request,
        int? claimedAdminUserId,
        EditorialFactsRow? facts,
        DateTimeOffset now)
    {
        EditorialAuthorityDecision Deny(EditorialDenialReason reason, EditorialActor? actor = null, EditorialInstallation? installation = null)
            => new(EditorialAuthorityOutcome.Denied, reason, actor, installation, now);

        if (facts is null)
        {
            return Deny(EditorialDenialReason.SessionUnknown);
        }

        if (claimedAdminUserId != facts.SessionAdminUserId)
        {
            return Deny(EditorialDenialReason.SessionUnknown);
        }

        switch (SessionPolicy.Evaluate(facts.IssuedAt, facts.LastSeenAt, facts.RevokedAt, now))
        {
            case SessionState.Valid:
                break;
            case SessionState.Revoked:
                return Deny(EditorialDenialReason.SessionRevoked);
            default:
                return Deny(EditorialDenialReason.SessionExpired);
        }

        if (!facts.AccountIsActive)
        {
            return Deny(EditorialDenialReason.AccountInactive);
        }

        if (!AdminRole.All.Contains(facts.AccountRole, StringComparer.Ordinal))
        {
            return Deny(EditorialDenialReason.RoleUnrecognized);
        }

        var actor = new EditorialActor(facts.SessionAdminUserId, facts.AccountHomeNode, facts.AccountRole, facts.SessionId);

        if (facts.InstallationCount == 0)
        {
            return Deny(EditorialDenialReason.InstallationMissing, actor);
        }

        if (facts.InstallationCount > 1 || facts.InstallationKey is not { } key || key == Guid.Empty)
        {
            return Deny(EditorialDenialReason.InstallationIdentityInvalid, actor);
        }

        var installation = new EditorialInstallation(key, facts.LicenseType ?? "", facts.LicensedUntil);

        if (facts.InstallationSetupComplete != true)
        {
            return Deny(EditorialDenialReason.InstallationIncomplete, actor, installation);
        }

        switch (facts.LicenseType)
        {
            case LicenseType.Trial or LicenseType.Subscription:
                if (facts.LicensedUntil is not { } until)
                {
                    return Deny(EditorialDenialReason.LicenseExpiryMissing, actor, installation);
                }

                if (now >= until)
                {
                    return Deny(EditorialDenialReason.LicenseExpired, actor, installation);
                }

                break;

            case LicenseType.Perpetual:
                if (facts.LicensedUntil is not null)
                {
                    return Deny(EditorialDenialReason.LicenseInconsistent, actor, installation);
                }

                break;

            default:
                return Deny(EditorialDenialReason.LicenseUnrecognized, actor, installation);
        }

        if (!facts.ModuleDeclared)
        {
            return Deny(EditorialDenialReason.ModuleMissing, actor, installation);
        }

        if (!facts.ActivationPresent || !facts.ActivationIsActive)
        {
            return Deny(EditorialDenialReason.ModuleInactive, actor, installation);
        }

        if (facts.ActivationExpiresAt is { } expires && now >= expires)
        {
            return Deny(EditorialDenialReason.ModuleExpired, actor, installation);
        }

        if (string.IsNullOrWhiteSpace(request.ArticleId)
            || request.ArticleId.Length > ArticleIdMaxLength
            || request.ArticleId.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return Deny(EditorialDenialReason.ResourceInvalid, actor, installation);
        }

        if (!Enum.IsDefined(request.Operation))
        {
            return Deny(EditorialDenialReason.OperationUnknown, actor, installation);
        }

        // Todos los hechos a favor. Sin política por artículo y operación
        // ratificada, un rol global no es un permiso sobre este artículo.
        return Deny(EditorialDenialReason.ContextualPolicyUnratified, actor, installation);
    }
}

/// <summary>
/// Productor candidato de <see cref="IAssistantEditorialAuthority"/>.
/// </summary>
/// <remarks>
/// Solo usa los claims para saber qué sesión mirar; todo lo demás lo relee. El
/// reloj se lee una vez, después de la lectura.
/// </remarks>
internal sealed class AssistantEditorialAuthority(
    CoreDbContext database,
    CurrentAdmin currentAdmin,
    TimeProvider clock,
    ILogger<AssistantEditorialAuthority> logger) : IAssistantEditorialAuthority
{
    /// <summary>Código de módulo del Asistente.</summary>
    internal const string AssistantModuleCode = "assistant";

    /// <inheritdoc />
    public async Task<EditorialAuthorityDecision> EvaluateAsync(EditorialAuthorityRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (currentAdmin.SessionId is not { } sessionId || currentAdmin.AdminUserId is null)
        {
            return new(EditorialAuthorityOutcome.Denied, EditorialDenialReason.SessionMissing, null, null, clock.GetUtcNow());
        }

        EditorialFactsRow? facts;
        try
        {
            facts = await FactsQuery(sessionId).SingleOrDefaultAsync(cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Evaluación cancelada por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo leer la autoridad editorial del Asistente; se deniega.");
            return new(EditorialAuthorityOutcome.Denied, EditorialDenialReason.AuthorityUnavailable, null, null, clock.GetUtcNow());
        }

        return AssistantEditorialEvaluator.Evaluate(request, currentAdmin.AdminUserId, facts, clock.GetUtcNow());
    }

    /// <summary>Una sola sentencia: la sesión con su cuenta, y la instalación y el módulo como subconsultas.</summary>
    internal IQueryable<EditorialFactsRow> FactsQuery(Guid sessionId)
        => database.AdminSessions
            .AsNoTracking()
            .Where(session => session.AdminSessionId == sessionId)
            .Select(session => new EditorialFactsRow(
                session.AdminSessionId,
                session.AdminUserId,
                session.IssuedAt,
                session.LastSeenAt,
                session.RevokedAt,
                session.AdminUser!.IsActive,
                session.AdminUser.Role,
                session.AdminUser.HomeNode,
                // Con predicado a propósito: un Count() sin él EF lo ejecuta aparte,
                // como consulta propia, y los hechos dejarían de ser de una sola
                // sentencia. ck_installation_singleton obliga a singleton = true.
                database.Installations.Count(i => i.Singleton),
                database.Installations.Select(i => (Guid?)i.InstallationKey).FirstOrDefault(),
                database.Installations.Select(i => (bool?)i.IsSetupComplete).FirstOrDefault(),
                database.Installations.Select(i => i.LicenseType).FirstOrDefault(),
                database.Installations.Select(i => i.LicensedUntil).FirstOrDefault(),
                database.Modules.Any(m => m.Code == AssistantModuleCode),
                database.Modules.Any(m => m.Code == AssistantModuleCode && m.Activation != null),
                database.Modules.Any(m => m.Code == AssistantModuleCode && m.Activation != null && m.Activation.IsActive),
                database.Modules
                    .Where(m => m.Code == AssistantModuleCode && m.Activation != null)
                    .Select(m => m.Activation!.ExpiresAt)
                    .FirstOrDefault()));
}

/// <summary>Registro explícito y opcional del productor editorial candidato.</summary>
/// <remarks>No lo llama <c>CoreServices</c> ni el host. Solo en modo normal, donde existe <see cref="CurrentAdmin"/>.</remarks>
public static class CandidateAssistantEditorialRegistration
{
    /// <summary>Registra <see cref="IAssistantEditorialAuthority"/> con el productor candidato.</summary>
    public static IServiceCollection AddCandidateAssistantEditorialAuthority(this IServiceCollection services)
    {
        services.AddScoped<IAssistantEditorialAuthority, AssistantEditorialAuthority>();
        return services;
    }
}
