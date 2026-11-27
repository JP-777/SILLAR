namespace Sillar.Core.Domain;

/// <summary>Evidencia local de la zona de esta instalación; no se replica ni referencia administradores.</summary>
public sealed class InstallationTimeZoneAttestation
{
    public int Id { get; set; }
    public Guid AttestationId { get; set; } = Guid.CreateVersion7();
    public Guid InstallationKey { get; set; }
    public required string ZoneId { get; set; }
    public required string SourceName { get; set; }
    public required string SourceVersion { get; set; }
    public required string AttestedByName { get; set; }
    public int AttestedByAdminUserLocalId { get; set; }
    public required string AttestedByAdminUserHomeNode { get; set; }
    public DateTimeOffset AttestedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public required string AuthorityVersion { get; set; }
}
