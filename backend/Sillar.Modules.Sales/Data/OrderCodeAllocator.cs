using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Data;

/// <summary>
/// Entrega el siguiente código visible de pedido, sin huecos y sin duplicados.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se llama dentro de la transacción que persiste el pedido, y al final.</b>
/// Las dos condiciones son del mecanismo aprobado el 27 de septiembre de 2026 y
/// ninguna es un detalle:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <b>Dentro de la misma transacción</b>, porque así un pedido que falla a
///     mitad devuelve su número: el <c>UPDATE</c> del contador se deshace con el
///     resto. Una secuencia de PostgreSQL no lo haría — <c>nextval</c> es
///     deliberadamente no transaccional y el número se consumiría igual.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Al final, justo antes de confirmar</b>, porque el carrito no consume
///     número. Pedirlo al abrir el carrito gastaría uno en cada carrito abandonado,
///     y los abandonados son la mayoría. Además acorta al mínimo el tramo en que la
///     fila del contador está bloqueada.
///     </description>
///   </item>
/// </list>
/// <para>
/// <b>Lo que cuesta:</b> mientras la transacción vive, la fila de la serie está
/// bloqueada, así que dos confirmaciones de la <b>misma serie</b> se serializan —
/// la segunda espera. Es la contrapartida de no dejar huecos y se acepta a
/// sabiendas.
/// </para>
/// <para>
/// <b>Hasta dónde llega la promesa.</b> Dentro de una serie
/// <c>(nodo, año)</c>, un pedido confirmado consume un número y uno que falla no
/// consume ninguno. <b>No se promete continuidad</b> ante borrados, correcciones
/// manuales ni repartición entre nodos.
/// </para>
/// </remarks>
internal sealed class OrderCodeAllocator(
    SalesDbContext database,
    ISettingsReader settings,
    NodeIdentity node,
    TimeProvider clock)
{
    /// <summary>
    /// Toma el siguiente código de la serie del nodo para el año en curso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El <c>INSERT … ON CONFLICT DO NOTHING</c> resuelve el caso del cambio de
    /// año: el primer pedido de enero puede llegar <b>dos veces a la vez</b> y las
    /// dos transacciones intentarían crear la fila que no existe. Con
    /// <c>ON CONFLICT</c> una la crea, la otra no falla, y el <c>UPDATE …
    /// RETURNING</c> siguiente serializa a las dos sobre la misma fila.
    /// </para>
    /// <para>
    /// <b>Sin <c>SaveChanges</c>:</b> los dos comandos van por SQL directo para que
    /// el <c>UPDATE</c> y su lectura sean una sola operación atómica. Un
    /// leer-luego-escribir desde el rastreador de EF dejaría un hueco entre ambos
    /// por el que dos transacciones obtendrían el mismo número.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Si la etiqueta de serie no está configurada. <b>Es la guarda de arranque en
    /// la operación</b>: sin etiqueta no se compone un código, y componerlo con una
    /// letra inventada dejaría pedidos que nadie puede dictar.
    /// </exception>
    public async Task<string> SiguienteAsync(CancellationToken cancellationToken)
    {
        var label = settings.Get(SalesSettingsKeys.OrderSeriesLabel);

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException(
                "No se puede numerar un pedido: la etiqueta de serie de este nodo no " +
                $"está configurada. Se fija en la instalación, en el ajuste " +
                $"'{SalesSettingsKeys.OrderSeriesLabel}', derivándola del código del " +
                $"nodo ('{node.Code}'). No tiene valor por defecto a propósito: una " +
                "letra igual en dos nodos produce dos pedidos con el mismo código.");
        }

        var year = clock.GetUtcNow().Year;

        // La fila de la serie puede no existir todavía: primer pedido del año, o
        // primera instalación. ON CONFLICT deja que dos transacciones simultáneas
        // lo intenten sin que ninguna falle.
        const string abrirSerie =
            "INSERT INTO sales.order_series (node_code, year, last_number) " +
            "VALUES (@p0, @p1, 0) " +
            "ON CONFLICT (node_code, year) DO NOTHING;";

        await database.Database.ExecuteSqlRawAsync(
            abrirSerie,
            [
                new NpgsqlParameter("p0", node.Code),
                new NpgsqlParameter("p1", year)
            ],
            cancellationToken);

        // UPDATE … RETURNING: incrementar y leer en un solo paso. El bloqueo de
        // fila que PostgreSQL toma aquí es lo que serializa a dos confirmaciones de
        // la misma serie, y lo que hace que no haya huecos. Un leer-luego-escribir
        // desde el rastreador de EF dejaría un hueco entre ambos por el que dos
        // transacciones obtendrían el mismo número.
        const string tomarNumero =
            "UPDATE sales.order_series " +
            "SET last_number = last_number + 1 " +
            "WHERE node_code = @p0 AND year = @p1 " +
            "RETURNING last_number AS \"Value\";";

        var correlative = await database.Database
            .SqlQueryRaw<int>(
                tomarNumero,
                new NpgsqlParameter("p0", node.Code),
                new NpgsqlParameter("p1", year))
            .SingleAsync(cancellationToken);

        return OrderCode.Componer(label, year, correlative);
    }
}
