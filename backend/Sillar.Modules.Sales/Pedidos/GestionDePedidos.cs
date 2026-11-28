using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
using Sillar.Modules.Sales.Dtos;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>La autorización, el bloqueo y la atribución pertenecen a la operación, no al endpoint.</summary>
internal sealed class GestionDePedidos(SalesDbContext db, IModuleAuthorization authority, TimeProvider clock)
{
    public async Task<IReadOnlyList<PedidoEnPanel>> ListarAsync(string? status, bool attention, CancellationToken ct)
    {
        await authority.RequireAsync("sales", "read", ct);
        await new VencimientoDePlazos(db, clock).VencerLoCumplidoAsync(ct);
        var query = db.Orders.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.Status == status);
        if (attention) query = query.Where(p => p.Status == OrderStatus.Expired
            || ((p.Status == OrderStatus.PendingPayment || p.Status == OrderStatus.PaymentToVerify)
                && db.OrderPayments.Any(x => x.OrderId == p.OrderId && x.CanFulfill == false)));
        return await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.OrderId).Take(100)
            .Select(p => new PedidoEnPanel(p.OrderCode, p.Status, p.CustomerFullName, p.TotalAmount,
                p.CurrencyCode, p.CreatedAt, p.PaymentDueAt, p.RowVersion,
                p.Status == OrderStatus.Expired || ((p.Status == OrderStatus.PendingPayment || p.Status == OrderStatus.PaymentToVerify)
                    && db.OrderPayments.Any(x => x.OrderId == p.OrderId && x.CanFulfill == false))))
            .ToListAsync(ct);
    }

    public async Task<DetalleEnPanel?> ObtenerAsync(string code, CancellationToken ct)
    {
        await authority.RequireAsync("sales", "read", ct);
        await new VencimientoDePlazos(db, clock).VencerLoCumplidoAsync(ct);
        var p = await db.Orders.AsNoTracking().SingleOrDefaultAsync(p => p.OrderCode == code && p.IsActive, ct);
        if (p is null) return null;
        var pagos = await db.OrderPayments.AsNoTracking().Where(x => x.OrderId == p.OrderId)
            .OrderBy(x => x.RegisteredAt).ThenBy(x => x.OrderPaymentId)
            .Select(x => new PagoEnPanel(x.Method, x.Amount, x.CurrencyCode, x.Reference,
                x.RegisteredAt, x.RegisteredBy, x.WasLate, x.CanFulfill)).ToListAsync(ct);
        var lineas = await db.OrderLines.AsNoTracking().Where(x => x.OrderId == p.OrderId)
            .OrderBy(x => x.ProductName).ThenBy(x => x.OrderLineId)
            .Select(x => new LineaDePedidoPropio(x.ProductName, x.VariantValue, x.SaleUnit,
                x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice)).ToListAsync(ct);
        var historia = await db.OrderStatusChanges.AsNoTracking().Where(x => x.OrderId == p.OrderId)
            .OrderBy(x => x.ChangedAt).ThenBy(x => x.OrderStatusChangeId)
            .Select(x => new CambioEnPanel(x.FromStatus, x.ToStatus, x.ChangedAt, x.ChangedBy, x.Reason)).ToListAsync(ct);
        var atencion = p.Status == OrderStatus.Expired || ((p.Status == OrderStatus.PendingPayment || p.Status == OrderStatus.PaymentToVerify)
            && pagos.Any(x => x.CanFulfill == false));
        return new(new(p.OrderCode, p.Status, p.CustomerFullName, p.TotalAmount, p.CurrencyCode,
            p.CreatedAt, p.PaymentDueAt, p.RowVersion, atencion), p.CustomerEmail, p.CustomerPhone, lineas, pagos, historia);
    }

    public async Task<ResultadoDeGestion> RegistrarPagoAsync(string code, RegistrarPagoManual input, CancellationToken ct)
    {
        await authority.RequireAsync("sales", "pay", ct);
        if (input.RequestId == Guid.Empty || !PaymentMethod.All.Contains(input.Method)
            || input.Amount < 0 || input.Amount > 9999999999.99m || decimal.Round(input.Amount, 2) != input.Amount
            || input.Reference?.Length > 200)
            return new(false, "Revisa el medio, el importe con hasta dos decimales y la referencia de hasta 200 caracteres.");
        await using var tx = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(db, ct);
        var p = await BloqueoDePedido.ObtenerAsync(db, code, ct);
        if (p is null) return new(false, "El pedido no existe.");
        // Se relee tras esperar el bloqueo: no se usa una sesión o un permiso anterior a la espera.
        var actor = await authority.RequireAsync("sales", "pay", ct);
        var reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
        var previous = await db.OrderPayments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == p.OrderId && x.ManualRequestId == input.RequestId, ct);
        if (previous is not null)
            return previous.Method == input.Method && previous.Amount == input.Amount
                && previous.Reference == reference && previous.CanFulfill == input.CanFulfill
                ? new(true, Replayed: true)
                : new(false, "Ese intento ya registró otro contenido. Revisa el pago antes de iniciar uno nuevo.");
        if (!CurrencyCode.TieneFormaIso(p.CurrencyCode))
            return new(false, "Este pedido histórico no tiene moneda acreditada. No se puede registrar un importe sin conocer su moneda.");
        var pagos = await db.OrderPayments.AsNoTracking().Where(x => x.OrderId == p.OrderId).ToListAsync(ct);
        if (pagos.Any(x => x.CurrencyCode != p.CurrencyCode))
            return new(false, "Hay pagos con moneda desconocida o diferente. Revisa el historial antes de cobrar.");
        var cobrado = pagos.Sum(x => x.Amount);
        if (input.Amount > p.TotalAmount - cobrado || (input.Amount == 0 && (p.TotalAmount != 0 || pagos.Count > 0)))
            return new(false, "El importe supera el saldo pendiente o repite la confirmación de un pedido sin importe.");
        var persona = StaffAttribution.De(actor.DisplayName, actor.LocalId, actor.HomeNode);
        var transition = OrderTransitionPolicy.RegistrarPago(p.Status, persona, input.CanFulfill);
        if (!transition.Permitida) return new(false, transition.Impedimento);
        var now = clock.GetUtcNow();
        db.OrderPayments.Add(new OrderPayment
        {
            OrderId = p.OrderId, Method = input.Method, Amount = input.Amount, CurrencyCode = p.CurrencyCode,
            Source = PaymentSource.Staff, Reference = reference, ManualRequestId = input.RequestId,
            CanFulfill = input.CanFulfill, RegisteredAt = now, WasLate = now >= p.PaymentDueAt,
            RegisteredBy = persona.Name, RegisteredByAdminUserLocalId = persona.LocalId,
            RegisteredByAdminUserHomeNode = persona.HomeNode,
        });
        // Un pago parcial no afirma que el pedido está pagado. El historial conserva cada abono.
        if (cobrado + input.Amount >= p.TotalAmount && transition.EstadoDestino is { } next && next != p.Status)
            Cambiar(p, next, persona, null, now);
        await db.SaveChangesAsync(ct);
        await tx.CompletarAsync(ct);
        return new(true);
    }

    public async Task<ResultadoDeGestion> CambiarAsync(string code, CambiarPedido input, CancellationToken ct)
    {
        await authority.RequireAsync("sales", "manage", ct);
        if (input.Reason?.Length > 1000) return new(false, "El motivo admite hasta 1000 caracteres.");
        await using var tx = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(db, ct);
        var p = await BloqueoDePedido.ObtenerAsync(db, code, ct);
        if (p is null) return new(false, "El pedido no existe.");
        var actor = await authority.RequireAsync("sales", "manage", ct);
        if (p.RowVersion != input.ExpectedVersion)
            return new(false, "El pedido cambió mientras lo revisabas. Actualiza el detalle antes de continuar.");
        var persona = StaffAttribution.De(actor.DisplayName, actor.LocalId, actor.HomeNode);
        var pagos = await db.OrderPayments.AsNoTracking().Where(x => x.OrderId == p.OrderId).ToListAsync(ct);
        var paid = CurrencyCode.TieneFormaIso(p.CurrencyCode) && pagos.Count > 0
            && pagos.All(x => x.CurrencyCode == p.CurrencyCode) && pagos.Sum(x => x.Amount) >= p.TotalAmount;
        var transition = OrderTransitionPolicy.Gestionar(p.Status, input.Action, persona, input.Reason, paid);
        if (!transition.Permitida) return new(false, transition.Impedimento);
        Cambiar(p, transition.EstadoDestino!, persona, input.Reason?.Trim(), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await tx.CompletarAsync(ct);
        return new(true);
    }

    private void Cambiar(Order p, string next, StaffAttribution who, string? reason, DateTimeOffset now)
    {
        db.OrderStatusChanges.Add(new OrderStatusChange { OrderId = p.OrderId, FromStatus = p.Status,
            ToStatus = next, ChangedAt = now, ChangedBy = who.Name,
            ChangedByAdminUserLocalId = who.LocalId, ChangedByAdminUserHomeNode = who.HomeNode, Reason = reason });
        p.Status = next;
    }
}
