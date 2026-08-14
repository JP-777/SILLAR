using Microsoft.EntityFrameworkCore;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Dtos;
using Sillar.Shared.Paging;

namespace Sillar.Core.Services;

/// <summary>Filtros de la consulta de auditoría. Se combinan entre sí.</summary>
/// <param name="From">Desde esta fecha, inclusive.</param>
/// <param name="To">Hasta esta fecha, inclusive.</param>
/// <param name="AdminUserId">Quién actuó.</param>
/// <param name="ModuleCode">Módulo donde ocurrió.</param>
/// <param name="Action">Acción realizada.</param>
/// <param name="EntityType">Tipo de entidad afectada.</param>
/// <param name="EntityId">Identificador de la entidad afectada.</param>
internal sealed record AuditFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? AdminUserId,
    string? ModuleCode,
    string? Action,
    string? EntityType,
    string? EntityId);

/// <summary>Consulta del registro de auditoría. Solo lectura.</summary>
/// <remarks>
/// No hay contrapartida de escritura ni de borrado: los registros se escriben
/// desde dentro con <c>IAuditWriter</c> y no se editan ni se eliminan desde el
/// API (SPEC §8.15). Que este servicio solo sepa consultar es parte de esa
/// garantía.
/// </remarks>
internal sealed class AuditQueryService(CoreDbContext database)
{
    /// <summary>Consulta la auditoría con filtros y paginación.</summary>
    public async Task<PagedResult<AuditEntryResponse>> QueryAsync(
        AuditFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = Filter(database.AuditLogs.AsNoTracking(), filter);

        // El total se cuenta con el mismo filtro, antes de paginar: es lo que
        // permite al panel saber cuántas páginas hay.
        var total = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.AuditLogId)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(entry => new AuditEntryResponse(
                entry.AuditLogId,
                entry.OccurredAt,
                entry.AdminUserId,
                entry.AdminUserEmail,
                entry.ModuleCode,
                entry.EntityType,
                entry.EntityId,
                entry.Action,
                entry.Summary,
                entry.IpAddress))
            .ToListAsync(cancellationToken);

        return PagedResult<AuditEntryResponse>.From(page, items, total);
    }

    /// <summary>
    /// Aplica los filtros presentes.
    /// </summary>
    /// <remarks>
    /// Cada uno se añade solo si viene, para que EF no genere condiciones
    /// inútiles. El orden sigue a los índices del SPEC §4.9: fecha, usuario y
    /// módulo son los que están indexados.
    /// </remarks>
    private static IQueryable<AuditLog> Filter(IQueryable<AuditLog> query, AuditFilter filter)
    {
        if (filter.From is { } from)
        {
            query = query.Where(entry => entry.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(entry => entry.OccurredAt <= to);
        }

        if (filter.AdminUserId is { } adminUserId)
        {
            query = query.Where(entry => entry.AdminUserId == adminUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ModuleCode))
        {
            query = query.Where(entry => entry.ModuleCode == filter.ModuleCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(entry => entry.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(entry => entry.EntityType == filter.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(entry => entry.EntityId == filter.EntityId);
        }

        return query;
    }
}
