using Microsoft.AspNetCore.Http;
using Sillar.Core.Data;
using Sillar.Core.Domain;

namespace Sillar.Core.Auditing;

/// <summary>Lo que se registra de una acción administrativa.</summary>
/// <param name="Action">Acción realizada. Ver <see cref="Domain.Values.AuditAction"/>.</param>
public sealed record AuditEntry(string Action)
{
    /// <summary>Quién actuó. Nulo si fue el sistema o si el correo no existe.</summary>
    public int? AdminUserId { get; init; }

    /// <summary>Correo de quien actuó, guardado como snapshot.</summary>
    public string? AdminUserEmail { get; init; }

    /// <summary>Módulo donde ocurrió.</summary>
    public string? ModuleCode { get; init; }

    /// <summary>Tipo de entidad afectada.</summary>
    public string? EntityType { get; init; }

    /// <summary>Identificador de la entidad afectada.</summary>
    public string? EntityId { get; init; }

    /// <summary>Descripción legible de lo ocurrido.</summary>
    public string? Summary { get; init; }
}

/// <summary>Escribe en el registro de auditoría.</summary>
/// <remarks>
/// De momento vive dentro de CORE y no en <c>Sillar.Core.Contracts</c>: el SPEC
/// §5 lo sitúa en el contrato público, pero todavía no existe ningún otro módulo
/// que lo use. Se mueve al contrato en cuanto M01 lo necesite, que será su
/// segundo caso real.
/// </remarks>
internal interface IAuditWriter
{
    /// <summary>Registra una acción.</summary>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);
}

/// <inheritdoc />
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
