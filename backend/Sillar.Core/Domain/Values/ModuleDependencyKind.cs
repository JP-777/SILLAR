namespace Sillar.Core.Domain.Values;

/// <summary>Tipo de dependencia entre dos módulos.</summary>
public static class ModuleDependencyKind
{
    /// <summary>
    /// Dura: el módulo no funciona sin el otro. Permite clave foránea entre
    /// schemas e impide activar el dependiente si el otro está inactivo.
    /// </summary>
    public const string Hard = "hard";

    /// <summary>
    /// Blanda: el módulo funciona solo y se enriquece si el otro está. Prohibida
    /// la clave foránea en la migración; va en un script de integración.
    /// </summary>
    public const string Soft = "soft";

    /// <summary>Todos los valores admitidos.</summary>
    public static readonly string[] All = [Hard, Soft];
}
