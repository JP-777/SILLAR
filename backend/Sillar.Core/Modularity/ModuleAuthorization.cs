using Microsoft.EntityFrameworkCore;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Modularity;

/// <summary>Permisos locales: catálogo del módulo, disponibilidad, sesión actual y excepción por persona.</summary>
internal sealed class ModuleAuthorization(CoreDbContext database, DeclaredModules declared,
    IModuleRegistry registry, AdminSessionAuthority session) : IModuleAuthorization
{
    public async Task<AuthorizedAdmin> RequireAsync(string moduleCode, string operationCode, CancellationToken ct)
    {
        var module = declared.Modules.SingleOrDefault(m => m.Code == moduleCode);
        var operation = module?.Operations.SingleOrDefault(o => o.Code == operationCode);
        if (module is null || operation is null || !registry.IsActive(moduleCode)
            || !await ModuleLicensing.IsLicensedAsync(database, moduleCode, ct)
            || !await database.Modules.AnyAsync(m => m.Code == moduleCode && m.Activation != null && m.Activation.IsActive, ct))
            throw new ModuleAccessDeniedException(404, "La operación no está disponible.");
        var actor = await session.RequireAsync(operation.MinimumRole, ct);
        var permission = await database.ModuleOperationPermissions.AsNoTracking()
            .Where(p => p.AdminUserId == actor.LocalId && p.ModuleCode == moduleCode && p.OperationCode == operationCode)
            .Select(p => (bool?)p.Allowed).SingleOrDefaultAsync(ct);
        if (permission == false)
            throw new ModuleAccessDeniedException(403, "Tu cuenta no tiene permiso para realizar esta operación.");
        return actor;
    }
}
