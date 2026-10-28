namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Valor agregado de los pedidos de M03, agrupado por moneda, para consumidores como M10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nunca suma monedas entre sí y nunca suma lo que no tiene moneda.</b> Un total sin
/// unidad es un número que invita a sumarlo con otro: de los pedidos sin moneda solo se
/// publica cuántos son.
/// </para>
/// <para>
/// Solo trabaja sobre esta base de datos, no agrega nodos. No publica clientes ni
/// identificadores de pedidos. Es independiente del lector de recuentos de pedidos y no
/// lo modifica.
/// </para>
/// </remarks>
public interface IOrderValueAggregateReader
{
    /// <summary>
    /// Agrega los pedidos creados en el intervalo semiabierto
    /// [<paramref name="startInclusive"/>, <paramref name="endExclusive"/>), en UTC,
    /// sin excluir bajas lógicas.
    /// </summary>
    /// <remarks>
    /// <b>Propaga un fallo real</b> de la base o una cancelación: disfrazar una avería de
    /// cohorte vacía es la forma de que nadie la vea.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">El fin no es posterior al inicio.</exception>
    Task<OrderValueAggregateSnapshot> ReadCreatedBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);
}

/// <summary>
/// Una cohorte, un instante de lectura. Invariante:
/// <c>CohortCount = Σ ByCurrency(ActiveCount + InactiveCount) + WithoutCurrency.Active + WithoutCurrency.Inactive</c>.
/// </summary>
/// <param name="StartInclusive">Límite inferior incluido, en UTC.</param>
/// <param name="EndExclusive">Límite superior excluido, en UTC.</param>
/// <param name="ObservedAtUtc">Instante del servidor tomado antes de la consulta.</param>
/// <param name="CohortCount">Todos los pedidos creados en la cohorte, con o sin moneda, activos o no.</param>
/// <param name="ByCurrency">Un grupo por moneda presente, ordenado por código. Vacío si ninguno tiene moneda.</param>
/// <param name="WithoutCurrency">Los pedidos sin moneda: <b>solo se cuentan</b>.</param>
public sealed record OrderValueAggregateSnapshot(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    long CohortCount,
    IReadOnlyList<OrderValueByCurrency> ByCurrency,
    OrdersWithoutCurrency WithoutCurrency);

/// <summary>
/// Valor congelado de los pedidos de una moneda. Activos e inactivos van separados y
/// <b>no se suman entre sí</b>: la baja lógica es un hecho, no un filtro implícito.
/// </summary>
/// <param name="CurrencyCode">Código ISO 4217.</param>
/// <param name="ActiveCount">Pedidos activos en esa moneda.</param>
/// <param name="ActiveFrozenTotal">Suma de sus totales congelados.</param>
/// <param name="InactiveCount">Pedidos dados de baja en esa moneda.</param>
/// <param name="InactiveFrozenTotal">Suma de sus totales congelados.</param>
public sealed record OrderValueByCurrency(
    string CurrencyCode,
    long ActiveCount,
    decimal ActiveFrozenTotal,
    long InactiveCount,
    decimal InactiveFrozenTotal);

/// <summary>Pedidos cuya moneda es desconocida. Sin importe, a propósito.</summary>
/// <param name="Active">Activos sin moneda.</param>
/// <param name="Inactive">Dados de baja sin moneda.</param>
public sealed record OrdersWithoutCurrency(long Active, long Inactive)
{
    /// <summary>Todos los pedidos sin moneda de la cohorte.</summary>
    public long Total => Active + Inactive;
}
