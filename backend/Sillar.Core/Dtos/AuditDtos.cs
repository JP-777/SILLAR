namespace Sillar.Core.Dtos;

/// <summary>Una entrada del registro de auditoría.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
/// <param name="AdminUserId">Quién actuó, si su cuenta sigue existiendo.</param>
/// <param name="AdminUserEmail">
/// Correo de quien actuó, tal como se guardó. Es un snapshot, no una unión con
/// <c>admin_users</c>: por eso el registro sobrevive al borrado del usuario.
/// </param>
/// <param name="ModuleCode">Módulo donde ocurrió.</param>
/// <param name="EntityType">Tipo de entidad afectada.</param>
/// <param name="EntityId">Identificador de la entidad afectada.</param>
/// <param name="Action">Acción realizada.</param>
/// <param name="Summary">Descripción legible.</param>
/// <param name="IpAddress">Dirección de origen.</param>
public sealed record AuditEntryResponse(
    long Id,
    DateTimeOffset OccurredAt,
    int? AdminUserId,
    string? AdminUserEmail,
    string? ModuleCode,
    string? EntityType,
    string? EntityId,
    string Action,
    string? Summary,
    string? IpAddress);
