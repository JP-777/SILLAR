using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Authentication;
using Sillar.Core.Domain.Values;
using Sillar.Core.Dtos;
using Sillar.Core.Services;
using Sillar.Shared.Paging;

namespace Sillar.Core.Endpoints;

/// <summary>Consulta del registro de auditoría.</summary>
/// <remarks>
/// Solo <c>GET</c>. La ausencia de <c>POST</c>, <c>PUT</c> y <c>DELETE</c> es
/// deliberada: los registros se escriben desde dentro y no se editan ni se borran
/// desde el API (SPEC §8.15).
/// </remarks>
public static class AuditEndpoints
{
    /// <summary>Monta <c>GET /api/admin/audit</c>.</summary>
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/audit", Query)
            .RequireAuthorization(AdminRole.SuperAdmin)
            .WithTags("Auditoría")
            .WithName("QueryAudit")
            .WithSummary("Consulta el registro de auditoría.")
            .WithDescription(
                $"Filtros combinables, orden por fecha descendente y paginación " +
                $"(por defecto {PageRequest.DefaultSize}, máximo {PageRequest.MaxSize}). " +
                "Solo lectura: no existe forma de editar ni borrar registros desde el API.")
            .Produces<PagedResult<AuditEntryResponse>>(StatusCodes.Status200OK);

        return endpoints;
    }

    /// <summary>Consulta la auditoría.</summary>
    /// <param name="audit">Servicio de consulta.</param>
    /// <param name="from">Desde esta fecha, inclusive.</param>
    /// <param name="to">Hasta esta fecha, inclusive.</param>
    /// <param name="adminUserId">Quién actuó.</param>
    /// <param name="moduleCode">Módulo donde ocurrió.</param>
    /// <param name="action">Acción: <c>create</c>, <c>login</c>, <c>activate</c>…</param>
    /// <param name="entityType">Tipo de entidad afectada.</param>
    /// <param name="entityId">Identificador de la entidad afectada.</param>
    /// <param name="page">Número de página, empezando en 1.</param>
    /// <param name="pageSize">Elementos por página. Se recorta al máximo permitido.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Una página de registros, del más reciente al más antiguo.</returns>
    private static async Task<IResult> Query(
        AuditQueryService audit,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? adminUserId,
        string? moduleCode,
        string? action,
        string? entityType,
        string? entityId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var filter = new AuditFilter(from, to, adminUserId, moduleCode, action, entityType, entityId);

        return Results.Ok(await audit.QueryAsync(filter, PageRequest.Of(page, pageSize), cancellationToken));
    }
}
