using Microsoft.EntityFrameworkCore;
using Sillar.Core.Authentication;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Modularity;

namespace Sillar.Core.Services;

public sealed record ModulePermissionResponse(string ModuleCode, string ModuleName, string OperationCode,
    string OperationName, string MinimumRole, bool? Allowed, bool Effective);

/// <summary>Configuración de permisos con autoridad dentro de la operación y auditoría atómica.</summary>
internal sealed class ModulePermissionService(CoreDbContext database, DeclaredModules declared,
    AdminSessionAuthority session, AuditWriter audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<ModulePermissionResponse>> ListAsync(int userId, CancellationToken ct)
    {
        await session.RequireAsync(AdminRole.SuperAdmin, ct);
        var user = await database.AdminUsers.AsNoTracking().SingleOrDefaultAsync(u => u.AdminUserId == userId, ct)
            ?? throw new ModuleAccessDeniedException(404, "La cuenta no existe.");
        var permissions = await database.ModuleOperationPermissions.AsNoTracking()
            .Where(p => p.AdminUserId == userId).ToListAsync(ct);
        return declared.Modules.OrderBy(m => m.DisplayOrder).SelectMany(m => m.Operations.Select(o =>
        {
            var assigned = permissions.SingleOrDefault(p => p.ModuleCode == m.Code && p.OperationCode == o.Code)?.Allowed;
            return new ModulePermissionResponse(m.Code, m.DisplayName, o.Code, o.DisplayName,
                o.MinimumRole, assigned, user.IsActive && RoleHierarchy.Satisfies(user.Role, o.MinimumRole) && assigned != false);
        })).ToArray();
    }

    public async Task SetAsync(int userId, string moduleCode, string operationCode, bool? allowed, CancellationToken ct)
    {
        var actor = await session.RequireAsync(AdminRole.SuperAdmin, ct);
        var module = declared.Modules.SingleOrDefault(m => m.Code == moduleCode);
        if (module?.Operations.SingleOrDefault(o => o.Code == operationCode) is not { } operation)
            throw new ModuleAccessDeniedException(404, "La operación no existe en este módulo.");
        if (!await database.AdminUsers.AnyAsync(u => u.AdminUserId == userId, ct))
            throw new ModuleAccessDeniedException(404, "La cuenta no existe.");
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var knownAudit = database.ChangeTracker.Entries<Sillar.Core.Domain.AuditLog>().Select(e => e.Entity).ToHashSet();
        try
        {
        if (allowed is { } value)
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.module_operation_permissions(admin_user_id,module_code,operation_code,allowed,updated_at)
                VALUES ({userId},{moduleCode},{operationCode},{value},{clock.GetUtcNow()})
                ON CONFLICT (admin_user_id,module_code,operation_code)
                DO UPDATE SET allowed=EXCLUDED.allowed,updated_at=EXCLUDED.updated_at
                """, ct);
        else
            await database.ModuleOperationPermissions.Where(p => p.AdminUserId == userId
                && p.ModuleCode == moduleCode && p.OperationCode == operationCode).ExecuteDeleteAsync(ct);
        await audit.WriteAsync(new AuditEntry(AuditAction.Update)
        {
            AdminUserId = actor.LocalId, AdminUserName = actor.DisplayName,
            AdminUserHomeNode = actor.HomeNode, AdminUserEmail = actor.Email,
            ModuleCode = "core", EntityType = "module_permission", EntityId = userId.ToString(),
            Summary = $"Permiso {module.DisplayName} / {operation.DisplayName}: {(allowed is null ? "heredado del rol" : allowed.Value ? "permitido dentro del rol" : "denegado")}.",
        }, ct);
        await tx.CommitAsync(ct);
        }
        catch
        {
            foreach (var entry in database.ChangeTracker.Entries<Sillar.Core.Domain.AuditLog>().Where(e => !knownAudit.Contains(e.Entity)).ToArray())
                entry.State = EntityState.Detached;
            throw;
        }
    }
}
