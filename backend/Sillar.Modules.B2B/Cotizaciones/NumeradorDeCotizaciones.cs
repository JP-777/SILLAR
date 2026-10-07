using Sillar.Modules.B2B.Data;
using Sillar.Shared.Data.Numbering;

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

    private static readonly TransactionalSeriesDefinition Series = new(
        B2bDbContext.Schema,
        "quote_number_series",
        "series_code",
        "year",
        "last_value");

    public static string Formatear(int anio, int correlativo) => $"{Serie}-{anio}-{correlativo:0000}";

    public static int AnioDe(DateTimeOffset instante)
        => TransactionalSeriesAllocator.YearInLima(instante);

    /// <summary>Reserva el siguiente número dentro de la transacción abierta del contexto.</summary>
    public static async Task<string> SiguienteAsync(B2bDbContext db, DateTimeOffset ahora, CancellationToken ct)
    {
        var anio = AnioDe(ahora);
        var correlativo = await TransactionalSeriesAllocator.ReserveNextAsync(
            db, Series, Serie, anio, ct);
        return Formatear(anio, correlativo);
    }
}
