namespace Sillar.Core.Domain;

/// <summary>
/// Módulo que el producto conoce.
/// </summary>
/// <remarks>
/// Es una proyección del código, no su fuente: la verdad está en las
/// implementaciones de <c>IModule</c> y esta tabla se sincroniza desde ellas al
/// arrancar. Existe para poder consultar el catálogo desde el panel y desde SQL.
/// Editarla a mano no cambia nada: el siguiente arranque la reescribe.
/// </remarks>
public class Module
{
    /// <summary>Identificador.</summary>
    public int ModuleId { get; set; }

    /// <summary>Código del módulo. También el nombre de su schema.</summary>
    public required string Code { get; set; }

    /// <summary>Nombre visible, en español.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Qué hace el módulo, en lenguaje de negocio. Obligatoria.</summary>
    public required string Description { get; set; }

    /// <summary>Versión del módulo.</summary>
    public required string Version { get; set; }

    /// <summary>Verdadero solo en CORE, que no se puede desactivar.</summary>
    public bool IsCore { get; set; }

    /// <summary>Orden de presentación en el panel.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Activación del módulo en esta instalación.</summary>
    public ModuleActivation? Activation { get; set; }

    /// <summary>Módulos de los que este depende.</summary>
    public ICollection<ModuleDependency> Dependencies { get; set; } = [];

    /// <summary>Módulos que dependen de este.</summary>
    public ICollection<ModuleDependency> Dependents { get; set; } = [];
}
