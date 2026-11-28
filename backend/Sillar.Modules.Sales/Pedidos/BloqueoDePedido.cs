using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>Todos los escritores del pedido se serializan en su fila, dentro de una transacción.</summary>
internal static class BloqueoDePedido
{
    public static async Task<Order?> ObtenerAsync(SalesDbContext db, string code, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Bloquear un pedido exige una transacción abierta.");
        var pedido = await db.Orders.FromSqlInterpolated(
            $"SELECT * FROM sales.orders WHERE order_code = {code} AND is_active FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        return pedido is null ? null : RastrearActual(db, pedido);
    }

    public static Order RastrearActual(SalesDbContext db, Order actual)
    {
        var previo = db.Orders.Local.SingleOrDefault(p => p.OrderId == actual.OrderId);
        if (previo is null) { db.Attach(actual); return actual; }
        if (db.Entry(previo).State != EntityState.Unchanged)
            throw new InvalidOperationException("El pedido tiene cambios sin guardar antes de tomar el bloqueo.");
        db.Entry(previo).CurrentValues.SetValues(actual);
        db.Entry(previo).State = EntityState.Unchanged;
        return previo;
    }
}
