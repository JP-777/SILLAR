using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Lo que M03 le dice a CORE antes de que cambie la moneda: si ya hay pedidos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cualquier fila de <c>sales.orders</c> cuenta</b> (N1–N5 ratificadas): activas o de
/// baja lógica, de cualquier estado, históricas y con <c>currency_code</c> nulo. Una baja
/// lógica sigue siendo un compromiso registrado, y un pedido sin moneda es un importe
/// cuya unidad nadie registró: ninguno de los dos se puede reinterpretar. Los pagos no
/// hace falta mirarlos: cuelgan de un pedido por clave foránea y no hay borrado físico.
/// </para>
/// <para>
/// <b>Solo lee, y nunca pide el bloqueo de moneda.</b> CORE la llama teniendo ya
/// <see cref="MonetaryAuthorityLock.AcquireExclusive"/>, en otra conexión: si la sonda
/// pidiera el compartido, esperaría a CORE y CORE a ella, y PostgreSQL no lo vería como
/// interbloqueo. Tampoco <c>FOR SHARE</c> ni <c>FOR UPDATE</c>.
/// </para>
/// <para>
/// <b>Instantánea fresca.</b> Con una transacción ya abierta en este contexto la consulta
/// podría ver una foto anterior al bloqueo de CORE —en <c>REPEATABLE READ</c> o más— y no
/// ver un pedido recién confirmado. Como no se puede saber desde aquí con qué aislamiento
/// se abrió, la respuesta en ese caso es <see cref="MonetaryRecordsState.Unknown"/>.
/// </para>
/// <para>
/// <b>Ante la duda, <see cref="MonetaryRecordsState.Unknown"/>.</b> Sin schema, sin
/// conexión, con cualquier error de PostgreSQL: CORE no cambia la moneda. Solo la
/// cancelación del llamador sale como excepción.
/// </para>
/// </remarks>
internal sealed class SondaDeRegistrosMonetarios(
    SalesDbContext database,
    ILogger<SondaDeRegistrosMonetarios> logger) : IMonetaryRecordsProbe
{
    /// <inheritdoc />
    public async Task<MonetaryRecordsState> ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (database.Database.CurrentTransaction is not null)
        {
            logger.LogWarning(
                "La sonda de pedidos se llamó con una transacción abierta en su contexto; " +
                "su foto podría ser anterior al bloqueo de moneda. Se responde que no se sabe.");
            return MonetaryRecordsState.Unknown;
        }

        try
        {
            return await ClasificarAsync(database, cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Sonda de pedidos cancelada por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo comprobar si hay pedidos; se responde que no se sabe.");
            return MonetaryRecordsState.Unknown;
        }
    }

    /// <summary>
    /// Los pedidos que cuentan: todos. Sin filtro, sin bloqueo y sin filtros globales.
    /// </summary>
    internal static IQueryable<Order> Pedidos(SalesDbContext database)
        => database.Orders.IgnoreQueryFilters().AsNoTracking();

    /// <summary>La consulta en sí, sin las guardas de <see cref="ProbeAsync"/>.</summary>
    /// <remarks>
    /// Separada para que la prueba física la ejecute dentro de una transacción que se
    /// deshace; en producción solo se llega aquí sin transacción abierta.
    /// </remarks>
    internal static async Task<MonetaryRecordsState> ClasificarAsync(
        SalesDbContext database,
        CancellationToken cancellationToken)
        => await Pedidos(database).AnyAsync(cancellationToken)
            ? MonetaryRecordsState.Present
            : MonetaryRecordsState.None;
}
