using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// El vencimiento del plazo, contra PostgreSQL real.
/// </summary>
/// <remarks>
/// Lo que hace falta comprobar aquí no se puede acreditar en memoria: que el
/// <c>CHECK</c> de atribución completa <b>acepta los tres nulos</b> y que el asiento
/// queda escrito con ellos. Un proveedor en memoria no tiene <c>CHECK</c>.
/// </remarks>
[Collection("SalesDb")]
public sealed class VencimientoPersistenciaTests(SalesDbFixture fixture)
    : IClassFixture<SalesDbFixture>
{
    /// <summary>Reloj fijo: el vencimiento depende del tiempo y no se deja al azar.</summary>
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private static readonly DateTimeOffset Ahora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static async Task CrearClienteAsync(SalesDbContext db, Guid id, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO crm.customers (customer_id, full_name, email, origin_node) " +
            "VALUES ({0}, {1}, {2}, 'principal')",
            [id, $"Cliente {id:N}", $"{id:N}@prueba.pe"],
            ct);

    private static Order Pedido(Guid cliente, string codigo, string estado, DateTimeOffset vence)
        => new()
        {
            OrderCode = codigo,
            CustomerId = cliente,
            CustomerFullName = "Cliente Congelado",
            CustomerEmail = "congelado@prueba.pe",
            Status = estado,
            PaymentDueAt = vence,
            TotalAmount = 10m
        };

    [Fact]
    public async Task Un_plazo_cumplido_pasa_el_pedido_a_Vencido()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        db.Orders.Add(Pedido(ana, "P-2026-8100", OrderStatus.PendingPayment, Ahora.AddHours(-1)));
        await db.SaveChangesAsync(ct);

        var r = await new VencimientoDePlazos(db, new RelojFijo(Ahora)).VencerLoCumplidoAsync(ct);

        Assert.Equal(1, r.Vencidos);
        Assert.Equal(["P-2026-8100"], r.Codigos);

        var pedido = await db.Orders.AsNoTracking()
            .SingleAsync(o => o.OrderCode == "P-2026-8100", ct);
        Assert.Equal(OrderStatus.Expired, pedido.Status);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_asiento_del_vencimiento_deja_los_TRES_datos_de_atribucion_en_NULL()
    {
        // Es la prueba central del tramo: lo hizo el tiempo, no una persona. Ni
        // «Sistema», ni identificador cero, ni nodo inventado. Y el CHECK
        // ck_order_status_changes_atribucion_completa tiene que ACEPTAR los tres
        // nulos: si exigiera atribución, esta inserción fallaría.
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        var pedido = Pedido(ana, "P-2026-8110", OrderStatus.PendingPayment, Ahora.AddHours(-1));
        db.Orders.Add(pedido);
        await db.SaveChangesAsync(ct);

        await new VencimientoDePlazos(db, new RelojFijo(Ahora)).VencerLoCumplidoAsync(ct);

        var asiento = await db.OrderStatusChanges.AsNoTracking()
            .SingleAsync(c => c.OrderId == pedido.OrderId, ct);

        Assert.Null(asiento.ChangedBy);
        Assert.Null(asiento.ChangedByAdminUserLocalId);
        Assert.Null(asiento.ChangedByAdminUserHomeNode);
        Assert.Null(asiento.Atribucion());

        // Y lo que sí quedó escrito: de dónde a dónde, y cuándo.
        Assert.Equal(OrderStatus.PendingPayment, asiento.FromStatus);
        Assert.Equal(OrderStatus.Expired, asiento.ToStatus);

        // origin_node sí se sella: es el nodo donde ocurrió la actuación, y una
        // actuación del sistema también ocurre en algún nodo.
        Assert.Equal("principal", asiento.OriginNode);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Un_plazo_que_no_ha_llegado_no_vence()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        db.Orders.Add(Pedido(ana, "P-2026-8120", OrderStatus.PendingPayment, Ahora.AddHours(+1)));
        await db.SaveChangesAsync(ct);

        var r = await new VencimientoDePlazos(db, new RelojFijo(Ahora)).VencerLoCumplidoAsync(ct);

        Assert.DoesNotContain("P-2026-8120", r.Codigos);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Solo_vence_lo_que_estaba_pendiente_de_pago()
    {
        // Un pedido que ya se está preparando no vence aunque su plazo pasara: lo que
        // vence es la espera de un pago que no llegó.
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        db.Orders.Add(Pedido(ana, "P-2026-8130", OrderStatus.Preparing, Ahora.AddHours(-5)));
        db.Orders.Add(Pedido(ana, "P-2026-8131", OrderStatus.PaymentToVerify, Ahora.AddHours(-5)));
        db.Orders.Add(Pedido(ana, "P-2026-8132", OrderStatus.Delivered, Ahora.AddHours(-5)));
        await db.SaveChangesAsync(ct);

        var r = await new VencimientoDePlazos(db, new RelojFijo(Ahora)).VencerLoCumplidoAsync(ct);

        Assert.Equal(0, r.Vencidos);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Vencer_no_cancela_y_no_libera_nada()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        var pedido = Pedido(ana, "P-2026-8140", OrderStatus.PendingPayment, Ahora.AddHours(-1));
        db.Orders.Add(pedido);
        await db.SaveChangesAsync(ct);

        await new VencimientoDePlazos(db, new RelojFijo(Ahora)).VencerLoCumplidoAsync(ct);

        var despues = await db.Orders.AsNoTracking()
            .SingleAsync(o => o.OrderCode == "P-2026-8140", ct);

        Assert.NotEqual(OrderStatus.Cancelled, despues.Status);
        Assert.True(despues.IsActive);       // sigue existiendo
        Assert.Equal(10m, despues.TotalAmount);  // y conserva lo que costaba

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Correr_el_vencimiento_dos_veces_no_duplica_asientos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        var pedido = Pedido(ana, "P-2026-8150", OrderStatus.PendingPayment, Ahora.AddHours(-1));
        db.Orders.Add(pedido);
        await db.SaveChangesAsync(ct);

        var operacion = new VencimientoDePlazos(db, new RelojFijo(Ahora));
        var primera = await operacion.VencerLoCumplidoAsync(ct);
        var segunda = await operacion.VencerLoCumplidoAsync(ct);

        Assert.Equal(1, primera.Vencidos);
        Assert.Equal(0, segunda.Vencidos);
        Assert.Equal(1, await db.OrderStatusChanges.CountAsync(c => c.OrderId == pedido.OrderId, ct));

        await tx.RollbackAsync(ct);
    }
}
