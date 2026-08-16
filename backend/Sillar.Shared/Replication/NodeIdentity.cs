using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sillar.Shared.Replication;

/// <summary>
/// Qué nodo es esta instalación.
/// </summary>
/// <remarks>
/// Las tablas que se replican guardan en cada fila el nodo donde nació (ADR-016,
/// regla 4). M16 lo necesitará para ordenar los cambios, y mientras tanto sirve
/// para consultar, filtrar y agrupar por origen.
///
/// El valor sale de la configuración y no de un valor por defecto en la base de
/// datos: la migración es la misma en todos los nodos, así que un DEFAULT
/// grabado allí haría que dos sucursales dijeran que las filas nacieron en el
/// mismo sitio, que es justo lo contrario de lo que esta columna existe para
/// responder.
///
/// Vive en <c>Sillar.Shared</c> porque todo módulo con tablas replicadas escribe
/// esta columna igual: catálogo, clientes, existencias y ventas. Tenerlo una vez
/// evita que un día no coincidan.
/// </remarks>
/// <param name="Code">Identificador del nodo. Corto y estable.</param>
public sealed record NodeIdentity(string Code)
{
    /// <summary>Clave de configuración.</summary>
    public const string SettingKey = "Sillar:Node:Code";

    /// <summary>
    /// Nodo por defecto, para la instalación única que todavía no se replica.
    /// </summary>
    /// <remarks>
    /// Con un solo nodo el valor da igual mientras sea constante; lo que importa
    /// es que exista desde la primera fila. Rellenar esta columna después, con
    /// datos ya escritos, obliga a inventar de dónde vino cada fila.
    /// </remarks>
    public const string DefaultCode = "principal";
}

/// <summary>Registro de <see cref="NodeIdentity"/> en el contenedor.</summary>
public static class NodeIdentityServiceExtensions
{
    /// <summary>
    /// Registra el nodo si nadie lo hizo antes.
    /// </summary>
    /// <remarks>
    /// CORE lo necesita siempre —<c>core.media_assets</c> se replica desde la
    /// ADR-018— y cualquier otro módulo con tablas replicadas también. Sea cual
    /// sea el orden en que arranquen, el primero registra y los demás
    /// encuentran el mismo valor: todos leen la misma clave de configuración.
    /// </remarks>
    public static IServiceCollection TryAddNodeIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(NodeIdentity)))
        {
            services.AddSingleton(new NodeIdentity(
                configuration[NodeIdentity.SettingKey] ?? NodeIdentity.DefaultCode));
        }

        return services;
    }
}
