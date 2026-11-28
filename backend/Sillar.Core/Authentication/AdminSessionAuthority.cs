using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Authentication;

/// <summary>Autoridad de la operación: un principal con claims antiguos no basta.</summary>
internal interface IAdminSessionAuthority
{
    Task<AuthorizedAdmin> RequireAsync(string minimumRole, CancellationToken ct);
}

internal sealed class AdminSessionAuthority(CoreDbContext database, CurrentAdmin current, TimeProvider clock) : IAdminSessionAuthority
{
    public async Task<AuthorizedAdmin> RequireAsync(string minimumRole, CancellationToken ct)
    {
        if (current.SessionId is not { } sessionId || current.AdminUserId is not { } userId)
            throw new ModuleAccessDeniedException(401, "Inicia sesión para realizar esta operación.");
        var session = await database.AdminSessions.AsNoTracking().Include(x => x.AdminUser)
            .SingleOrDefaultAsync(x => x.AdminSessionId == sessionId && x.AdminUserId == userId, ct);
        var now = clock.GetUtcNow();
        if (session?.AdminUser is not { IsActive: true } actor || session.IssuedAt > now
            || session.LastSeenAt > now || session.ExpiresAt <= now
            || SessionPolicy.Evaluate(session.IssuedAt, session.LastSeenAt, session.RevokedAt, now) != SessionState.Valid)
            throw new ModuleAccessDeniedException(401, "La sesión ya no es válida. Vuelve a iniciar sesión.");
        if (!RoleHierarchy.Satisfies(actor.Role, minimumRole))
            throw new ModuleAccessDeniedException(403, "Tu cuenta no tiene permiso para realizar esta operación.");
        if (actor.AdminUserId < 1 || string.IsNullOrWhiteSpace(actor.FullName)
            || string.IsNullOrWhiteSpace(actor.HomeNode) || string.IsNullOrWhiteSpace(actor.Email))
            throw new ModuleAccessDeniedException(403, "La cuenta no tiene una atribución humana completa.");
        return new(actor.AdminUserId, actor.FullName, actor.HomeNode, actor.Email, actor.Role);
    }
}
