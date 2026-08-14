using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sillar.Shared.Modularity;

/// <summary>
/// Contrato que implementa todo módulo de SILLAR, incluido CORE.
/// </summary>
/// <remarks>
/// Es la única forma que tiene el host de conocer un módulo. Lo que un módulo
/// no declare aquí, el host no lo sabe.
///
/// Vive en <c>Sillar.Shared</c> y no en <c>Sillar.Core.Contracts</c> a propósito:
/// no es algo que CORE ofrezca a los demás, es el contrato de la plataforma.
/// CORE también lo implementa.
/// </remarks>
public interface IModule
{
    /// <summary>
    /// Código del módulo. Es también el nombre de su schema en PostgreSQL:
    /// <c>core</c>, <c>catalog</c>, <c>sales</c>.
    /// </summary>
    /// <remarks>
    /// Solo minúsculas, dígitos y guion bajo; empieza por letra; máximo 40
    /// caracteres. El host lo valida al arrancar.
    /// </remarks>
    string Code { get; }

    /// <summary>Nombre visible del módulo, en español. Aparece en el panel.</summary>
    string DisplayName { get; }

    /// <summary>Qué hace el módulo, en lenguaje de negocio. Máximo 300 caracteres.</summary>
    /// <remarks>
    /// Obligatoria. Alimenta la pantalla donde el negocio ve sus módulos y decide
    /// qué activar o qué comprar: un módulo sin descripción es una fila en blanco
    /// en la pantalla que sostiene el argumento de venta.
    /// </remarks>
    string Description { get; }

    /// <summary>Versión del módulo, en formato <c>mayor.menor.parche</c>.</summary>
    string Version { get; }

    /// <summary>Posición del módulo en el panel de administración. CORE es el 0.</summary>
    int DisplayOrder { get; }

    /// <summary>
    /// Códigos de los módulos sin los cuales este no puede funcionar.
    /// </summary>
    /// <remarks>
    /// Habilitan clave foránea entre schemas. Si una dependencia dura está
    /// inactiva, el host aborta el arranque: nunca degrada en silencio.
    /// Todo módulo que no sea CORE depende de CORE de forma dura.
    /// </remarks>
    string[] HardDependencies { get; }

    /// <summary>
    /// Códigos de los módulos que enriquecen a este, pero que pueden faltar.
    /// </summary>
    /// <remarks>
    /// Prohibida la clave foránea: columna nullable más datos snapshot. Si el
    /// otro módulo no está, este degrada su comportamiento sin fallar.
    /// </remarks>
    string[] SoftDependencies { get; }

    /// <summary>
    /// Registra en el contenedor los servicios del módulo: su <c>DbContext</c>,
    /// sus servicios de aplicación y las implementaciones de sus contratos.
    /// </summary>
    /// <remarks>
    /// El host solo lo llama en módulos activos. Un módulo no licenciado no
    /// registra nada.
    /// </remarks>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Monta las rutas del módulo.</summary>
    /// <remarks>
    /// El host solo lo llama en módulos activos: las rutas de un módulo
    /// inactivo no devuelven 403, sencillamente no existen.
    /// </remarks>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
