using System.Data.Common;

namespace Sillar.Shared.Configuration;

/// <summary>
/// A qué base y a qué servidor apunta una conexión. <b>Nunca la contraseña.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>De dónde sale.</b> Esto lo hacía el arranque a mano, en línea, como parte
/// del trabajo del pendiente 9: «una configuración mal puesta tiene que poder
/// verse — un <c>.env</c> equivocado levanta, se conecta y funciona, contra la
/// base de otro». Se saca aquí porque hace falta en un segundo sitio, y tener
/// dos formas de decir a qué base apuntamos sería exactamente la clase de
/// duplicación que este repositorio ya ha pagado dos veces.
/// </para>
/// <para>
/// <b>Host, puerto y base; nunca la cadena entera.</b> La contraseña no va a
/// los registros ni a las respuestas del API (CLAUDE.md, «Seguridad»). Este
/// tipo no tiene forma de exponerla: no la guarda.
/// </para>
/// </remarks>
/// <param name="Host">Servidor. <c>(sin host)</c> si la cadena no lo trae.</param>
/// <param name="Puerto">Puerto. <c>5432</c> si no está declarado, que es el que usaría.</param>
/// <param name="Base">Nombre de la base de datos.</param>
public sealed record DestinoDeConexion(string Host, string Puerto, string Base)
{
    /// <summary>Lee el destino de una cadena de conexión, sin quedarse con nada más.</summary>
    public static DestinoDeConexion DeCadena(string? connectionString)
    {
        var destino = new DbConnectionStringBuilder { ConnectionString = connectionString ?? string.Empty };

        return new DestinoDeConexion(
            destino.TryGetValue("Host", out var h) ? $"{h}" : "(sin host)",
            destino.TryGetValue("Port", out var p) ? $"{p}" : "5432",
            destino.TryGetValue("Database", out var d) ? $"{d}" : "(sin base)");
    }

    /// <summary>
    /// Lee el destino de una conexión ya abierta o configurada.
    /// </summary>
    /// <remarks>
    /// <b>Es la fuente más fiable de las dos</b>, y por eso existe: describe la
    /// conexión que de verdad se usó, no la que la configuración decía. Cuando
    /// lo que hay que explicar es un error que acaba de ocurrir <i>en</i> esa
    /// conexión, preguntar a otro sitio sería volver a suponer.
    /// </remarks>
    public static DestinoDeConexion DeConexion(DbConnection conexion)
        => DeCadena(conexion.ConnectionString);

    /// <summary>«base «x» en host:puerto», para meter en un mensaje.</summary>
    public override string ToString() => $"«{Base}» en {Host}:{Puerto}";
}
