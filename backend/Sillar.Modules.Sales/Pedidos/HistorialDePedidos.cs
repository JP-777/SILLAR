using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Implementación de <see cref="ICustomerOrderHistory"/> sobre el schema
/// <c>sales</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No lee nada de CRM.</b> Todo lo que devuelve está congelado en
/// <c>sales.orders</c>: el nombre y el correo del cliente se copiaron al comprar, así
/// que el historial sobrevive a que la ficha cambie — y a que el schema <c>crm</c>
/// desaparezca.
/// </para>
/// <para>
/// <b>Es el hueco que M04 declaró antes de que M03 existiera.</b> Su ficha pide este
/// contrato al contenedor y comprueba si vino; si M03 no está instalado, el hueco dice
/// que no hay módulo de pedidos y <b>no falla</b>.
/// </para>
/// </remarks>
internal sealed class HistorialDePedidos(SalesDbContext database) : ICustomerOrderHistory
{
    /// <summary>
    /// Tope del módulo para una lectura de resumen.
    /// </summary>
    /// <remarks>
    /// El límite <b>se acota, no se obedece</b>: quien llama no tiene por qué conocer
    /// un número que no le importa, y devolver un error le obligaría a conocerlo.
    /// Mismo criterio que <c>ICatalogService.BuscarParaSeleccionAsync</c>.
    /// </remarks>
    private const int TopeDelModulo = 100;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerOrderSummary>> ObtenerPedidosDeAsync(
        Guid customerId,
        int limit,
        CancellationToken cancellationToken)
    {
        var tope = Math.Clamp(limit, 1, TopeDelModulo);

        return await Consulta(database, customerId)
            .Take(tope)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// La consulta de resumen, una sola vez.
    /// </summary>
    /// <remarks>
    /// La comparten el contrato público y los endpoints del cliente: dos consultas
    /// equivalentes son dos sitios donde arreglar el mismo error, y dos que pueden
    /// separarse sin que nadie lo note.
    ///
    /// Ordenada por <c>created_at</c> descendente: del más reciente al más antiguo,
    /// que es como se lee un historial. El desempate va por <c>order_id</c>, que es
    /// <c>uuid</c> v7 y por tanto ordenado en el tiempo — sin él, dos pedidos del
    /// mismo instante saldrían en orden arbitrario entre páginas.
    /// </remarks>
    internal static IQueryable<CustomerOrderSummary> Consulta(
        SalesDbContext database,
        Guid customerId)
        => database.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId && o.IsActive)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.OrderId)
            .Select(o => new CustomerOrderSummary(
                o.OrderCode,
                o.Status,
                o.TotalAmount,
                database.OrderLines.Count(l => l.OrderId == o.OrderId),
                o.CreatedAt,
                o.PaymentDueAt));
}
