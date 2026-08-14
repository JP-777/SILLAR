namespace Sillar.Core.Domain;

/// <summary>
/// Una arista del grafo de dependencias, también sincronizada desde el código.
/// </summary>
public class ModuleDependency
{
    /// <summary>Identificador.</summary>
    public int ModuleDependencyId { get; set; }

    /// <summary>Módulo que depende.</summary>
    public int ModuleId { get; set; }

    /// <summary>Módulo del que se depende.</summary>
    public int DependsOnModuleId { get; set; }

    /// <summary>Dura o blanda. Ver <see cref="Values.ModuleDependencyKind"/>.</summary>
    public required string Kind { get; set; }

    /// <summary>Módulo que depende.</summary>
    public Module? Module { get; set; }

    /// <summary>Módulo del que se depende.</summary>
    public Module? DependsOnModule { get; set; }
}
