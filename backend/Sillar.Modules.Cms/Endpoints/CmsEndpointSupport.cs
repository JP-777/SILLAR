using Microsoft.AspNetCore.Http;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Endpoints;

/// <summary>Traducciones HTTP y auditoría compartidas por las rutas de CMS.</summary>
internal static class CmsEndpointSupport
{
    internal static IResult Result<T>(
        CmsOperation<T> operation,
        string field,
        Func<T, IResult> success)
        => operation.Outcome switch
        {
            CmsOutcome.Ok => success(operation.Value!),
            CmsOutcome.NotFound => Results.NotFound(),
            CmsOutcome.Conflict => Results.Problem(
                title: operation.Error,
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.ValidationProblem(
                new Dictionary<string, string[]> { [field] = [operation.Error!] },
                title: "Los datos del contenido no son válidos.")
        };

    internal static Task AuditAsync(
        IAuditWriter audit,
        ICurrentAdmin currentUser,
        string action,
        string entityType,
        string? entityId,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = currentUser.AdminUserId,
                AdminUserEmail = currentUser.Email,
                ModuleCode = CmsModule.ModuleCode,
                EntityType = entityType,
                EntityId = entityId,
                Summary = summary
            },
            cancellationToken);
}
