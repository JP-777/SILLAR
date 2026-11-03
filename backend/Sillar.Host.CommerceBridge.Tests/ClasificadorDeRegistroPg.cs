using System.Text.RegularExpressions;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>Lo que el Host envió a PostgreSQL, separado: lo que se ejecutó, lo que falló y lo que intentó escribir.</summary>
/// <param name="Exitosas">Sentencias registradas (<c>statement:</c> o <c>execute</c>) sin error detrás.</param>
/// <param name="Fallidas">Sentencias que PostgreSQL rechazó, al analizarlas o al ejecutarlas.</param>
/// <param name="Errores">Los mensajes <c>ERROR</c>/<c>FATAL</c>, en el mismo orden que <paramref name="Fallidas"/>.</param>
internal sealed record SentenciasClasificadas(IReadOnlyList<string> Exitosas, IReadOnlyList<string> Fallidas, IReadOnlyList<string> Errores)
{
    /// <summary>Todo lo que se intentó, haya salido bien o no.</summary>
    public IReadOnlyList<string> Intentadas => [.. Exitosas, .. Fallidas];

    /// <summary>Intentos de escribir o de cambiar el esquema, exitosos o fallidos.</summary>
    public IReadOnlyList<string> Mutantes => [.. Intentadas.Where(ClasificadorDeRegistroPg.EsMutante)];

    public string Resumen => $"exitosas={Exitosas.Count} · fallidas={Fallidas.Count} · mutantes={Mutantes.Count}";
}

/// <summary>
/// P1-POST-G6-R10 (O-3). Clasifica las entradas del registro de PostgreSQL
/// (<c>log_statement = all</c>) que <see cref="InstalacionFisicaR8.SentenciasDelHost"/> devuelve sin
/// el prefijo <c>app=…|db=…|</c>.
/// </summary>
/// <remarks>
/// El contador de R9 solo miraba las entradas <c>statement:</c>/<c>execute</c>: una sentencia que
/// falla al analizarse (protocolo extendido) no deja ninguna de las dos, solo <c>ERROR</c> y
/// <c>STATEMENT</c>, así que no se veía. Aquí cada <c>ERROR</c> o <c>FATAL</c> se empareja con su
/// <c>STATEMENT</c>; si la sentencia ya figuraba como registrada, pasa de exitosa a fallida.
/// </remarks>
internal static class ClasificadorDeRegistroPg
{
    private static readonly Regex Mutante = new(@"^\s*(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|CREATE|DROP|ALTER|GRANT|REVOKE|COPY)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool EsMutante(string sql) => Mutante.IsMatch(sql);

    public static SentenciasClasificadas Clasificar(IEnumerable<string> entradas)
    {
        var exitosas = new List<string>();
        var fallidas = new List<string>();
        var errores = new List<string>();
        string? errorPendiente = null;

        void CerrarError(string? sentencia)
        {
            if (errorPendiente is null) return;
            var texto = sentencia ?? "(sin sentencia registrada)";
            // La sentencia ya registrada por «statement:»/«execute» deja de contar como exitosa.
            var i = sentencia is null ? -1 : exitosas.LastIndexOf(sentencia);
            if (i >= 0) exitosas.RemoveAt(i);
            fallidas.Add(texto);
            errores.Add(errorPendiente);
            errorPendiente = null;
        }

        foreach (var e in entradas)
        {
            var entrada = e.TrimEnd();
            if (Empieza(entrada, "ERROR:") || Empieza(entrada, "FATAL:"))
            {
                CerrarError(null);
                errorPendiente = entrada;
                continue;
            }

            if (Empieza(entrada, "STATEMENT:"))
            {
                CerrarError(entrada["STATEMENT:".Length..].Trim());
                continue;
            }

            if (Empieza(entrada, "LOG:") && Registrada(entrada) is { } sql)
            {
                CerrarError(null);
                // El reinicio de una conexión devuelta al pool de Npgsql no es una sentencia del Host.
                if (!sql.StartsWith("DISCARD ALL", StringComparison.Ordinal)) exitosas.Add(sql);
            }
            // DETAIL, HINT, CONTEXT y LOG de otra cosa no son sentencias.
        }

        CerrarError(null);
        return new(exitosas, fallidas, errores);
    }

    private static bool Empieza(string entrada, string etiqueta) => entrada.StartsWith(etiqueta, StringComparison.Ordinal);

    /// <summary>La sentencia de un «LOG: statement:» o «LOG: execute &lt;nombre&gt;:», o null.</summary>
    private static string? Registrada(string entrada)
    {
        var i = entrada.IndexOf("statement: ", StringComparison.Ordinal);
        if (i >= 0) return entrada[(i + "statement: ".Length)..].Trim();
        var j = entrada.IndexOf("execute ", StringComparison.Ordinal);
        if (j >= 0 && entrada.IndexOf(": ", j, StringComparison.Ordinal) is var k and > 0) return entrada[(k + 2)..].Trim();
        return null;
    }
}
