using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Tests;

/// <summary>La idempotencia de liquidación se impone en PostgreSQL, incluso por SQL directo.</summary>
[Collection("SalesDb")]
public sealed class IdempotenciaExternaPersistenciaTests(SalesDbFixture fixture)
    : IClassFixture<SalesDbFixture>
{
    [Fact]
    public async Task Los_pagos_manuales_no_inventan_identidad_externa_y_admiten_varios_nulos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var pedido = await CrearPedidoAsync(db, ct);
        db.OrderPayments.AddRange(Pago(pedido, null), Pago(pedido, null));
        await db.SaveChangesAsync(ct);
        var pagos = await db.OrderPayments.AsNoTracking().Where(p => p.OrderId == pedido).ToArrayAsync(ct);
        Assert.Equal(2, pagos.Length);
        Assert.All(pagos, p => Assert.Null(p.ExternalSettlementId));
    }

    [Fact]
    public async Task Una_liquidacion_no_puede_asentar_dos_pagos_aunque_cambie_el_pedido()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var primero = await CrearPedidoAsync(db, ct);
        var segundo = await CrearPedidoAsync(db, ct);
        var externo = $"liquidacion-prueba-{Guid.CreateVersion7():N}";
        var pago = Pago(primero, externo);
        db.OrderPayments.Add(pago);
        await db.SaveChangesAsync(ct);

        // Segunda vía: ni endpoint ni validador EF. La base debe rechazarla.
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO sales.order_payments
                (order_payment_id, order_id, method, amount, currency_code, source,
                 external_settlement_id, registered_by, registered_by_admin_user_local_id,
                 registered_by_admin_user_home_node, registered_at, was_late, origin_node, row_version)
            SELECT {0}, {1}, method, amount, currency_code, source,
                   external_settlement_id, registered_by, registered_by_admin_user_local_id,
                   registered_by_admin_user_home_node, registered_at, was_late, origin_node, row_version
              FROM sales.order_payments WHERE order_payment_id = {2}
            """, [Guid.CreateVersion7(), segundo, pago.OrderPaymentId], ct));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("uq_order_payments_external_settlement_id", error.ConstraintName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task Una_identidad_externa_vacia_no_equivale_a_un_pago_manual(string externo)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var pedido = await CrearPedidoAsync(db, ct);
        db.OrderPayments.Add(Pago(pedido, externo));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal("ck_order_payments_external_settlement_id_no_vacio", postgres.ConstraintName);
    }

    private static OrderPayment Pago(Guid pedido, string? externo) => new()
    {
        OrderId = pedido,
        Method = PaymentMethod.Yape,
        Amount = 5,
        CurrencyCode = "PEN",
        Source = PaymentSource.Staff,
        ExternalSettlementId = externo,
        RegisteredBy = "Personal de prueba",
        RegisteredByAdminUserLocalId = 1,
        RegisteredByAdminUserHomeNode = "otro-nodo",
        RegisteredAt = DateTimeOffset.UtcNow,
    };

    private static async Task<Guid> CrearPedidoAsync(SalesDbContext db, CancellationToken ct)
    {
        var cliente = Guid.CreateVersion7();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO crm.customers (customer_id, full_name, email, origin_node) VALUES ({0}, {1}, {2}, 'principal')",
            [cliente, "Cliente de idempotencia", $"{cliente:N}@prueba.pe"], ct);
        var pedido = new Order
        {
            OrderCode = $"2026-{Guid.CreateVersion7():N}",
            CustomerId = cliente,
            CustomerFullName = "Cliente de idempotencia",
            CustomerEmail = $"{cliente:N}@prueba.pe",
            Status = OrderStatus.PendingPayment,
            PaymentDueAt = DateTimeOffset.UtcNow.AddHours(48),
            TotalAmount = 10,
            CurrencyCode = "PEN",
        };
        db.Orders.Add(pedido);
        await db.SaveChangesAsync(ct);
        return pedido.OrderId;
    }
}
