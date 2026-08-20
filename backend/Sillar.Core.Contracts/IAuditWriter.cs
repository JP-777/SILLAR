namespace Sillar.Core.Contracts;

/// <summary>Lo que se registra de una acción administrativa.</summary>
/// <param name="Action">Acción realizada. Ver <see cref="AuditAction"/>.</param>
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
/// Lo implementa CORE, único dueño de <c>core.audit_log</c>. Vive aquí y no
/// dentro de CORE porque M01 es su segundo caso real: todo módulo con
/// endpoints de escritura necesita dejar rastro (SPEC de M01 §8), y un módulo
/// nunca mapea la tabla de otro.
/// </remarks>
public interface IAuditWriter
{
    /// <summary>Registra una acción.</summary>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);
}
