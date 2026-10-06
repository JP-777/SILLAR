using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sillar.Modules.B2B.Data;

namespace Sillar.Modules.B2B.Cotizaciones;

/// <summary>
/// El número visible de una cotización: <c>C-2026-0147</c> (ratificado por JP el 30/09/2026).
/// </summary>
/// <remarks>
/// <para>
/// <c>C</c> identifica la cotización de M07; año visible; correlativo con
/// reinicio anual; serie independiente de la de M03. <b>No lleva UUID ni
/// <c>origin_node</c></b>: es un campo aparte, legible (ADR-016, regla 2).
/// </para>
/// <para>
/// <b>Continuidad transaccional</b> (ADR-016, excepción del 27/09): el contador
/// es una fila por serie y año, y se incrementa con <c>… RETURNING</c> en la
/// MISMA transacción que inserta la cotización. Un rollback deshace el
/// incremento, así que no deja hueco; la fila bloqueada serializa dos altas
/// concurrentes, así que reciben números consecutivos. Por eso se niega a
/// trabajar fuera de una transacción: fuera de ella, el número se confirmaría
/// solo y un fallo posterior sí dejaría hueco.
/// </para>
/// <para>
/// El año es el de America/Lima, que es la zona de las fechas del proyecto.
/// </para>
/// </remarks>
public static class NumeradorDeCotizaciones
{
    public const string Serie = "C";

    private static readonly TimeZoneInfo Lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    public static string Formatear(int anio, int correlativo) => $"{Serie}-{anio}-{correlativo:0000}";

    public static int AnioDe(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Lima).Year;

    /// <summary>Reserva el siguiente número dentro de la transacción abierta del contexto.</summary>
    public static async Task<string> SiguienteAsync(B2bDbContext db, DateTimeOffset ahora, CancellationToken ct)
    {
        var transaccion = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "El número de cotización se pide dentro de la transacción que la crea; fuera de ella, un fallo dejaría hueco.");

        var anio = AnioDe(ahora);
        var conexion = db.Database.GetDbConnection();
        await using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion.GetDbTransaction();
        comando.CommandText = """
            INSERT INTO b2b.quote_number_series (series_code, year, last_value)
            VALUES (@serie, @anio, 1)
            ON CONFLICT (series_code, year)
            DO UPDATE SET last_value = b2b.quote_number_series.last_value + 1
            RETURNING last_value
            """;
        Parametro(comando, "serie", Serie);
        Parametro(comando, "anio", anio);

        var correlativo = Convert.ToInt32(await comando.ExecuteScalarAsync(ct));
        return Formatear(anio, correlativo);
    }

    private static void Parametro(DbCommand comando, string nombre, object valor)
    {
        var p = comando.CreateParameter();
        p.ParameterName = nombre;
        p.Value = valor;
        comando.Parameters.Add(p);
    }
}
