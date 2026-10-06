using Npgsql;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// Ejecuta un script como lo hace <c>psql -f</c> <b>sin</b> <c>ON_ERROR_STOP</c>:
/// sentencia a sentencia, y un error no detiene las siguientes.
/// </summary>
/// <remarks>
/// Existe porque Npgsql ejecuta el script como un lote y se para en el primer
/// error, y así una guarda con el <c>DROP</c> FUERA de su bloque pasaba igual
/// de verde que una con el <c>DROP</c> dentro. Se vio rompiéndola a propósito
/// (sabotaje S3, <c>C6-AUDITORIA-M07.md</c>).
/// </remarks>
internal static class ComoPsql
{
    /// <summary>Corre todas las sentencias y devuelve los errores, en orden.</summary>
    public static async Task<IReadOnlyList<PostgresException>> EjecutarAsync(string cadena, string script)
    {
        var errores = new List<PostgresException>();
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();

        foreach (var sentencia in Separar(script))
        {
            try
            {
                await using var comando = new NpgsqlCommand(sentencia, conexion);
                await comando.ExecuteNonQueryAsync();
            }
            catch (PostgresException error)
            {
                errores.Add(error);
            }
        }

        return errores;
    }

    /// <summary>
    /// Parte por <c>;</c> fuera de comentarios <c>--</c>, de comillas simples y
    /// de bloques <c>$etiqueta$ … $etiqueta$</c>. Basta para los scripts de
    /// <c>database/modules</c>; no es un analizador de SQL.
    /// </summary>
    public static IReadOnlyList<string> Separar(string script)
    {
        var sentencias = new List<string>();
        var actual = new System.Text.StringBuilder();
        var i = 0;

        while (i < script.Length)
        {
            var c = script[i];

            if (c == '-' && i + 1 < script.Length && script[i + 1] == '-')
            {
                var fin = script.IndexOf('\n', i);
                i = fin < 0 ? script.Length : fin;
                continue;
            }

            if (c == '\'')
            {
                var fin = script.IndexOf('\'', i + 1);
                actual.Append(script, i, fin - i + 1);
                i = fin + 1;
                continue;
            }

            if (c == '$')
            {
                var cierre = script.IndexOf('$', i + 1);
                var etiqueta = cierre > i ? script[i..(cierre + 1)] : null;

                if (etiqueta is not null && etiqueta.Skip(1).SkipLast(1).All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    var fin = script.IndexOf(etiqueta, cierre + 1, StringComparison.Ordinal);
                    actual.Append(script, i, fin + etiqueta.Length - i);
                    i = fin + etiqueta.Length;
                    continue;
                }
            }

            if (c == ';')
            {
                if (!string.IsNullOrWhiteSpace(actual.ToString()))
                {
                    sentencias.Add(actual.ToString().Trim());
                }

                actual.Clear();
                i++;
                continue;
            }

            actual.Append(c);
            i++;
        }

        if (!string.IsNullOrWhiteSpace(actual.ToString()))
        {
            sentencias.Add(actual.ToString().Trim());
        }

        return sentencias;
    }
}
