using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
/// ninguna es un detalle. <b>La primera está impuesta</b>, no solo escrita: sin
/// transacción abierta este método lanza antes de tocar nada.
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
        // Lo primero, y antes de tocar la configuración o la base: sin transacción
        // abierta este método NO numera.
        //
        // Hasta ahora esto estaba documentado y no impuesto, que es la definición de
        // una barrera escrita y no puesta. Fuera de una transacción el UPDATE del
        // contador se confirma solo, así que un fallo posterior al insertar el pedido
        // dejaría el número consumido y un hueco permanente en la serie — exactamente
        // lo que «sin huecos» prohíbe, y por el mismo mecanismo que descartó nextval.
        //
        // La guarda va en la operación y no en quien llama: ponerla en el llamador
        // protegería de ese llamador, y aquí protege de todos, incluidos los que
        // todavía no existen. Mismo efecto que la que M07 ya demostró en su numerador
        // de cotizaciones.
        if (database.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "El número de pedido se pide dentro de la misma transacción que " +
                "persiste el pedido, y no hay ninguna abierta. Fuera de ella el " +
                "contador se confirmaría solo: si el pedido fallara después, el número " +
                "quedaría consumido y la serie tendría un hueco permanente.");
        }

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

        // El año es el de Lima, no el de UTC: el 31 de diciembre a las 20:00 de Lima
        // ya es el 1 de enero en UTC, y el pedido de esa tarde pertenece a la serie
        // del año que el cliente tiene en su calendario. La conversión vive en
        // OrderCode.AnioDe, que es puro y por eso su frontera se prueba sin base.
        var year = OrderCode.AnioDe(clock.GetUtcNow());

        // Un solo comando: inserta la serie si no existe y, si ya existía, incrementa.
        // El RETURNING devuelve el número en la misma ida y vuelta.
        //
        // Va por DbCommand y no por SqlQueryRaw<int>, y la razón es concreta: un
        // UPDATE … RETURNING no es SQL componible, así que EF Core intenta envolverlo
        // en una subconsulta al añadirle el Single() y falla al traducir. Se descubrió
        // ejecutándolo de verdad contra PostgreSQL —el barrido a mano con psql no pasa
        // por este camino—, que es exactamente lo que el §4 de
        // ANTES-DE-EMPEZAR-UN-MODULO advierte: una comprobación por otra vía no
        // acredita la vía que el código usa.
        //
        // El ON CONFLICT resuelve además el cambio de año: el primer pedido de enero
        // puede llegar dos veces a la vez y las dos intentarían crear la fila.
        const string tomarNumero =
            """
            INSERT INTO sales.order_series (node_code, year, last_number)
            VALUES (@nodo, @anio, 1)
            ON CONFLICT (node_code, year)
            DO UPDATE SET last_number = sales.order_series.last_number + 1
            RETURNING last_number
            """;

        var conexion = database.Database.GetDbConnection();
        await using var comando = conexion.CreateCommand();

        comando.CommandText = tomarNumero;
        comando.Transaction = database.Database.CurrentTransaction!.GetDbTransaction();
        Parametro(comando, "nodo", node.Code);
        Parametro(comando, "anio", year);

        var correlative = Convert.ToInt32(
            await comando.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);

        return OrderCode.Componer(label, year, correlative);
    }

    /// <summary>Un parámetro con nombre, sin repetir las cuatro líneas en cada uso.</summary>
    private static void Parametro(System.Data.Common.DbCommand comando, string nombre, object valor)
    {
        var p = comando.CreateParameter();
        p.ParameterName = nombre;
        p.Value = valor;
        comando.Parameters.Add(p);
    }
}
