namespace Sillar.Core.Domain;

/// <summary>Excepción local al permiso heredado del rol. No se replica.</summary>
public sealed class ModuleOperationPermission
{
    public int AdminUserId { get; set; }
    public required string ModuleCode { get; set; }
    public required string OperationCode { get; set; }
    public bool Allowed { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
