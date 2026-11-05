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
        Stage(entry);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>El contexto en el que este escritor deja sus filas.</summary>
    /// <remarks>
    /// Quien prepara un registro con <see cref="Stage"/> para guardarlo junto a su
    /// propio cambio comprueba con esto que los dos van en el mismo contexto.
    /// </remarks>
    internal CoreDbContext Context => database;

    /// <summary>
    /// Deja el registro en el contexto <b>sin guardar</b>, para que se confirme en
    /// el mismo <c>SaveChanges</c> que el cambio que audita: o los dos o ninguno.
    /// </summary>
    internal AuditLog Stage(AuditEntry entry)
    {
        var log = new AuditLog
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
        };

        database.AuditLogs.Add(log);
        return log;
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
