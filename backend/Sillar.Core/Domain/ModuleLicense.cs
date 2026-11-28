namespace Sillar.Core.Domain;

/// <summary>Derecho local de uso. Es independiente de encender o apagar el módulo y no se replica.</summary>
public sealed class ModuleLicense
{
    public required string ModuleCode { get; set; }
    public bool IsLicensed { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required string Source { get; set; }
}
