namespace Sillar.Core.Domain;

/// <summary>Registro de auditoría. Se escribe, no se edita ni se borra.</summary>
public class AuditLog
{
    /// <summary>Identificador. Es bigint porque esta tabla solo crece.</summary>
    public long AuditLogId { get; set; }

    /// <summary>Cuándo ocurrió.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Quién lo hizo. Nulo si fue el sistema o si el usuario fue eliminado.</summary>
    public int? AdminUserId { get; set; }

    /// <summary>
    /// Correo de quien actuó, guardado como snapshot.
    /// </summary>
    /// <remarks>
    /// Por la misma razón que un pedido guarda el nombre del producto: un
    /// registro de auditoría que pierde la identidad de quien actuó no sirve
    /// de nada.
    /// </remarks>
    public string? AdminUserEmail { get; set; }

    /// <summary>Módulo donde ocurrió. Texto, sin clave foránea.</summary>
    public string? ModuleCode { get; set; }

    /// <summary>Tipo de entidad afectada: <c>product</c>, <c>order</c>, <c>module</c>.</summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Identificador de la entidad afectada, como texto: no todas las claves
    /// del producto son enteras.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>Acción realizada. Ver <see cref="Contracts.AuditAction"/>.</summary>
    public required string Action { get; set; }

    /// <summary>Descripción legible de lo ocurrido.</summary>
    public string? Summary { get; set; }

    /// <summary>Dirección de origen. Admite IPv6.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Usuario que actuó, si sigue existiendo.</summary>
    public AdminUser? AdminUser { get; set; }
}
