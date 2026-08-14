namespace Sillar.Core.Domain;

/// <summary>
/// Qué módulos están activos en esta instalación.
/// </summary>
/// <remarks>
/// Separada de <see cref="Module"/> porque tienen dueños distintos: el catálogo
/// de módulos lo escribe el producto, la activación la escribe la licencia del
/// cliente. Mezclarlas obligaría a que una actualización del producto tocara
/// datos comerciales del negocio.
/// </remarks>
public class ModuleActivation
{
    /// <summary>Identificador.</summary>
    public int ModuleActivationId { get; set; }

    /// <summary>Módulo activado. Uno solo, una activación.</summary>
    public int ModuleId { get; set; }

    /// <summary>Estado actual.</summary>
    public bool IsActive { get; set; }

    /// <summary>Última activación.</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>Última desactivación.</summary>
    public DateTimeOffset? DeactivatedAt { get; set; }

    /// <summary>Vencimiento del módulo, si su licencia lo tiene.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Nota administrativa.</summary>
    public string? Notes { get; set; }

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Módulo activado.</summary>
    public Module? Module { get; set; }
}
