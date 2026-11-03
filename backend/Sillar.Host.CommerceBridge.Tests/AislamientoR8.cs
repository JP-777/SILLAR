using System.Globalization;
using System.Net;
using Npgsql;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// Identidad del PostgreSQL desechable, obtenida por un canal independiente de la conexión que
/// se va a verificar: el script de la instancia la lee con <c>docker exec</c> por el socket local
/// del contenedor recién creado y la deja en el archivo que apunta <see cref="AislamientoR8.VariableIdentidad"/>.
/// </summary>
public sealed record IdentidadEsperadaR8(string Contenedor, string SystemIdentifier, string ClusterName, int Puerto);

/// <summary>Una cadena que pasó la barrera: todos sus campos eran explícitos y coinciden con la identidad.</summary>
public sealed record CadenaVerificadaR8(string Cadena, string Host, int Puerto, string Usuario);

/// <summary>
/// R8-01. Barrera previa a cualquier operación de la fixture física: primero, sin abrir conexión,
/// la cadena tiene que estar completa y apuntar al puerto del contenedor esperado; después, con una
/// conexión de solo lectura, el servidor tiene que tener la identidad que dio el canal independiente.
/// Solo entonces hay <c>CREATE DATABASE</c>, migraciones o inserciones.
/// </summary>
public static class AislamientoR8
{
    /// <summary>Ruta del archivo de identidad que escribe <c>pg-temporal.sh up</c>.</summary>
    public const string VariableIdentidad = "R8A_IDENTIDAD";

    /// <summary>Claves que deben venir escritas: nada se deja al valor por omisión de Npgsql.</summary>
    private static readonly string[] Obligatorias = ["Host", "Port", "Username", "Password", "Database"];

    /// <summary>Únicas claves admitidas; cualquier alias (Server, User Id, Pwd…) se rechaza.</summary>
    private static readonly string[] Admitidas = [.. Obligatorias, "Timeout"];

    private static InvalidOperationException Rechazo(string motivo) => new($"R8-01: {motivo}. No se abre ninguna conexión.");

    /// <summary>Lee el archivo de identidad. Sin archivo o con un campo ausente, rechaza.</summary>
    public static IdentidadEsperadaR8 LeerIdentidad(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            throw Rechazo($"falta la identidad esperada de la instancia ({VariableIdentidad} sin archivo)");
        }

        return ParsearIdentidad(File.ReadAllLines(ruta));
    }

    public static IdentidadEsperadaR8 ParsearIdentidad(IEnumerable<string> lineas)
    {
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var linea in lineas.Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var i = linea.IndexOf('=', StringComparison.Ordinal);
            if (i <= 0 || !campos.TryAdd(linea[..i], linea[(i + 1)..]))
            {
                throw Rechazo("el archivo de identidad está mal formado o repite un campo");
            }
        }

        string Campo(string nombre) => campos.TryGetValue(nombre, out var v) && v.Length > 0
            ? v
            : throw Rechazo($"el archivo de identidad no trae «{nombre}»");

        var sysid = Campo("system_identifier");
        if (!ulong.TryParse(sysid, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw Rechazo("system_identifier esperado no es numérico");
        }

        if (!int.TryParse(Campo("puerto"), NumberStyles.None, CultureInfo.InvariantCulture, out var puerto) || puerto is < 1024 or > 65535 or 5432)
        {
            throw Rechazo("el puerto esperado no es válido para una instancia desechable");
        }

        return new(Campo("contenedor"), sysid, Campo("cluster_name"), puerto);
    }

    /// <summary>
    /// Validación sin red. Se analiza el texto tal como llegó, no lo que Npgsql completaría:
    /// una cadena cortada en el primer <c>;</c> queda sin puerto y se rechaza aquí.
    /// </summary>
    public static CadenaVerificadaR8 ValidarCadena(string? cadena, IdentidadEsperadaR8 esperada)
    {
        ArgumentNullException.ThrowIfNull(esperada);
        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw Rechazo("la cadena está vacía");
        }

        var pares = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parte in cadena.Split(';'))
        {
            if (parte.Length == 0)
            {
                continue;
            }

            var i = parte.IndexOf('=', StringComparison.Ordinal);
            if (i <= 0)
            {
                throw Rechazo("la cadena tiene un fragmento sin clave=valor");
            }

            var clave = parte[..i].Trim();
            var valor = parte[(i + 1)..];
            if (!Admitidas.Contains(clave, StringComparer.OrdinalIgnoreCase))
            {
                throw Rechazo($"la clave «{clave}» no está admitida");
            }

            if (valor.Length == 0 || valor != valor.Trim() || valor.Contains('"') || valor.Contains('\''))
            {
                throw Rechazo($"«{clave}» está vacío o tiene blancos o comillas");
            }

            if (!pares.TryAdd(clave, valor))
            {
                throw Rechazo($"«{clave}» aparece dos veces");
            }
        }

        foreach (var obligatoria in Obligatorias)
        {
            if (!pares.ContainsKey(obligatoria))
            {
                throw Rechazo($"falta «{obligatoria}»: no se admiten valores implícitos (¿cadena truncada?)");
            }
        }

        if (pares["Host"] != "127.0.0.1")
        {
            throw Rechazo("el servidor no es 127.0.0.1");
        }

        if (!int.TryParse(pares["Port"], NumberStyles.None, CultureInfo.InvariantCulture, out var puerto) || puerto == 5432)
        {
            throw Rechazo("el puerto no es explícito o es el 5432 por omisión");
        }

        if (puerto != esperada.Puerto)
        {
            throw Rechazo($"el puerto {puerto} no es el del contenedor esperado ({esperada.Puerto})");
        }

        if (pares["Database"] != "postgres")
        {
            throw Rechazo("la base de entrada no es la de mantenimiento «postgres»");
        }

        // Lo que Npgsql entiende tiene que ser exactamente lo escrito.
        var npgsql = new NpgsqlConnectionStringBuilder(cadena);
        if (npgsql.Host != "127.0.0.1" || npgsql.Port != puerto || npgsql.Username != pares["Username"] || npgsql.Database != "postgres" || npgsql.Password != pares["Password"])
        {
            throw Rechazo("Npgsql interpreta la cadena distinto de como está escrita");
        }

        return new(cadena, npgsql.Host!, puerto, npgsql.Username!);
    }

    /// <summary>La identidad real comparada con la esperada. Distinta, rechaza.</summary>
    public static void CompararIdentidad(IdentidadEsperadaR8 esperada, string systemIdentifier, string clusterName)
    {
        if (!string.Equals(esperada.SystemIdentifier, systemIdentifier, StringComparison.Ordinal)
            || !string.Equals(esperada.ClusterName, clusterName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"R8-01: el servidor no es el contenedor esperado ({esperada.Contenedor}): system_identifier={systemIdentifier} cluster_name={clusterName}, " +
                $"esperados {esperada.SystemIdentifier} y {esperada.ClusterName}. No se ejecuta ningún SQL mutante.");
        }
    }

    /// <summary>
    /// Toda la barrera: valida sin red y, solo si pasa, consulta la identidad con dos lecturas
    /// (<c>pg_control_system()</c> y <c>cluster_name</c>) y la compara. Devuelve la cadena verificada.
    /// </summary>
    public static async Task<CadenaVerificadaR8> VerificarAsync(string? cadena, IdentidadEsperadaR8 esperada, CancellationToken ct = default)
    {
        var verificada = ValidarCadena(cadena, esperada);
        var lectura = new NpgsqlConnectionStringBuilder(verificada.Cadena) { ApplicationName = "r8a-verificacion" }.ConnectionString;
        await using var c = new NpgsqlConnection(lectura);
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("select system_identifier::text, current_setting('cluster_name') from pg_control_system()", c);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        CompararIdentidad(esperada, r.GetString(0), r.GetString(1));
        return verificada;
    }

    /// <summary>Para las regresiones: un puerto de bucle local que cuenta quién intentó conectar.</summary>
    public sealed class Escucha : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _l = new(IPAddress.Loopback, 0);

        public Escucha() => _l.Start();

        public int Puerto => ((IPEndPoint)_l.LocalEndpoint).Port;

        /// <summary>Hay una conexión esperando a ser aceptada.</summary>
        public bool AlguienConecto => _l.Pending();

        public void Dispose() => _l.Stop();
    }
}
