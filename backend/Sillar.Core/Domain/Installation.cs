namespace Sillar.Core.Domain;

/// <summary>
/// Identidad del negocio instalado y datos de su licencia. La tabla contiene
/// exactamente una fila.
/// </summary>
/// <remarks>
/// La unicidad la garantiza el par <see cref="Singleton"/> siempre verdadero y
/// único: como solo admite un valor y no se puede repetir, no cabe una segunda
/// fila. Es una restricción de la base, no una regla que alguien deba recordar.
/// </remarks>
public class Installation
{
    /// <summary>Identificador.</summary>
    public int InstallationId { get; set; }

    /// <summary>Siempre verdadero. Junto con su índice único, garantiza fila única.</summary>
    public bool Singleton { get; set; } = true;

    /// <summary>Nombre comercial del negocio instalado.</summary>
    public required string BusinessName { get; set; }

    /// <summary>Identificador de esta instalación, generado al instalar.</summary>
    public Guid InstallationKey { get; set; }

    /// <summary>Versión de SILLAR instalada.</summary>
    public required string ProductVersion { get; set; }

    /// <summary>Tipo de licencia. Ver <see cref="Values.LicenseType"/>.</summary>
    public required string LicenseType { get; set; }

    /// <summary>Vencimiento de la licencia. Nulo en licencia perpetua.</summary>
    public DateTimeOffset? LicensedUntil { get; set; }

    /// <summary>
    /// Marca el fin del modo instalación. Mientras sea falso, el host solo
    /// expone las rutas de instalación.
    /// </summary>
    public bool IsSetupComplete { get; set; }

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
