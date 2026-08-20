using Microsoft.AspNetCore.Http;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;

namespace Sillar.Core.Auditing;

/// <summary>
/// Implementación de <see cref="IAuditWriter"/>. Único escritor de
/// <c>core.audit_log</c>: el contrato es público, esta clase no.
/// </summary>
internal sealed class AuditWriter(
    CoreDbContext database,
    IHttpContextAccessor accessor,
    TimeProvider clock) : IAuditWriter
{
    /// <inheritdoc />
    /// <remarks>
    /// Guarda de inmediato. Dentro de una transacción abierta por quien llama,
    /// participa en ella: si la operación se deshace, su registro también.
    /// </remarks>
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        database.AuditLogs.Add(new AuditLog
        {
            OccurredAt = clock.GetUtcNow(),
            Action = entry.Action,
            AdminUserId = entry.AdminUserId,
            AdminUserEmail = Truncate(entry.AdminUserEmail, 150),
            ModuleCode = entry.ModuleCode,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Summary = Truncate(entry.Summary, 300),
            IpAddress = accessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recorta a lo que cabe en la columna.
    /// </summary>
    /// <remarks>
    /// Un correo inventado de trescientos caracteres en un intento de acceso no
    /// puede tumbar el registro de ese intento. Perder cola es preferible a
    /// perder el registro entero.
    /// </remarks>
    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
