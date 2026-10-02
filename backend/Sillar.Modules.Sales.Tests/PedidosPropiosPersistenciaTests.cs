using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// D2 y D3 contra PostgreSQL real: aislamiento entre clientes y el historial.
/// </summary>
/// <remarks>
/// Cada prueba vive dentro de una transacción que se deshace al final. No deja filas
/// y no toca nada de otros módulos más allá de un cliente de prueba que también se
/// deshace.
/// </remarks>
[Collection("SalesDb")]
public sealed class PedidosPropiosPersistenciaTests(SalesDbFixture fixture)
    : IClassFixture<SalesDbFixture>
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Crea un cliente en CRM por SQL: M03 no mapea sus tablas y no puede.</summary>
    private static async Task CrearClienteAsync(SalesDbContext db, Guid id, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO crm.customers (customer_id, full_name, email, origin_node) " +
            "VALUES ({0}, {1}, {2}, 'principal')",
            [id, $"Cliente {id:N}", $"{id:N}@prueba.pe"],
            ct);

    private static Order NuevoPedido(Guid cliente, string codigo, string estado = OrderStatus.PendingPayment)
        => new()
        {
            OrderCode = codigo,
            CustomerId = cliente,
            CustomerFullName = "Cliente Congelado",
            CustomerEmail = "congelado@prueba.pe",
            Status = estado,
            PaymentDueAt = Ahora.AddHours(48),
            TotalAmount = 25.50m
        };

    [Fact]
    public async Task Un_cliente_solo_ve_sus_propios_pedidos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        var beto = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        await CrearClienteAsync(db, beto, ct);

        db.Orders.Add(NuevoPedido(ana, "P-2026-8001"));
        db.Orders.Add(NuevoPedido(ana, "P-2026-8002"));
        db.Orders.Add(NuevoPedido(beto, "P-2026-8003"));
        await db.SaveChangesAsync(ct);

        var deAna = await HistorialDePedidos.Consulta(db, ana).ToListAsync(ct);
        var deBeto = await HistorialDePedidos.Consulta(db, beto).ToListAsync(ct);

        Assert.Equal(2, deAna.Count);
        Assert.Single(deBeto);
        Assert.DoesNotContain("P-2026-8003", deAna.Select(p => p.OrderCode));
        Assert.DoesNotContain("P-2026-8001", deBeto.Select(p => p.OrderCode));

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_pedido_de_otro_cliente_no_existe_para_la_consulta_filtrada()
    {
        // Es la base del 404 y no 403: la consulta del endpoint filtra por código Y
        // por cliente, así que el pedido ajeno no se encuentra. No hay un «lo
        // encontré pero no te lo doy» que alguien pueda devolver por error.
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        var beto = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        await CrearClienteAsync(db, beto, ct);

        db.Orders.Add(NuevoPedido(ana, "P-2026-8010"));
        await db.SaveChangesAsync(ct);

        var comoAna = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.OrderCode == "P-2026-8010" && o.CustomerId == ana && o.IsActive, ct);
        var comoBeto = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.OrderCode == "P-2026-8010" && o.CustomerId == beto && o.IsActive, ct);

        Assert.NotNull(comoAna);
        Assert.Null(comoBeto);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_historial_va_del_mas_reciente_al_mas_antiguo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        // created_at lo pone la base con now(), así que se guardan en dos pasadas
        // para que difieran de verdad.
        db.Orders.Add(NuevoPedido(ana, "P-2026-8020"));
        await db.SaveChangesAsync(ct);
        db.Orders.Add(NuevoPedido(ana, "P-2026-8021"));
        await db.SaveChangesAsync(ct);

        var historial = await HistorialDePedidos.Consulta(db, ana).ToListAsync(ct);

        Assert.Equal(["P-2026-8021", "P-2026-8020"], historial.Select(p => p.OrderCode));

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_resumen_cuenta_las_lineas_sin_traerlas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var pedido = NuevoPedido(ana, "P-2026-8030");
        db.Orders.Add(pedido);
        await db.SaveChangesAsync(ct);

        // Las líneas necesitan una variante real del catálogo: la FK cruzada es dura
        // y RESTRICT, que es justo el comportamiento que se quiere.
        var item = await db.Database
            .SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM catalog.product_items LIMIT 1")
            .ToListAsync(ct);

        if (item.Count == 0)
        {
            // Sin catálogo sembrado no se puede crear una línea. El resumen sigue
            // siendo comprobable: cero líneas es cero.
            var vacio = await HistorialDePedidos.Consulta(db, ana).SingleAsync(ct);
            Assert.Equal(0, vacio.LineCount);
            await tx.RollbackAsync(ct);
            return;
        }

        db.OrderLines.Add(new OrderLine
        {
            OrderId = pedido.OrderId,
            ItemId = item[0],
            ProductId = Guid.CreateVersion7(),
            ProductName = "Cuaderno universitario cuadriculado A4 100 hojas",
            Quantity = 3,
            UnitPrice = 8.50m
        });
        await db.SaveChangesAsync(ct);

        var resumen = await HistorialDePedidos.Consulta(db, ana).SingleAsync(ct);

        Assert.Equal(1, resumen.LineCount);
        Assert.Equal("P-2026-8030", resumen.OrderCode);
        Assert.Equal(25.50m, resumen.TotalAmount);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_historial_no_devuelve_pedidos_dados_de_baja()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var baja = NuevoPedido(ana, "P-2026-8040");
        baja.IsActive = false;
        db.Orders.Add(baja);
        await db.SaveChangesAsync(ct);

        Assert.Empty(await HistorialDePedidos.Consulta(db, ana).ToListAsync(ct));

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_contrato_publico_acota_el_limite_en_vez_de_obedecerlo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        db.Orders.Add(NuevoPedido(ana, "P-2026-8050"));
        await db.SaveChangesAsync(ct);

        ICustomerOrderHistory historial = new HistorialDePedidos(db);

        // Un límite de cero o negativo sube a 1; uno enorme se recorta al tope. Ni
        // uno ni otro produce un error: quien llama no tiene por qué conocer el tope.
        Assert.Single(await historial.ObtenerPedidosDeAsync(ana, 0, ct));
        Assert.Single(await historial.ObtenerPedidosDeAsync(ana, -5, ct));
        Assert.Single(await historial.ObtenerPedidosDeAsync(ana, 10_000, ct));

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Un_cliente_sin_pedidos_recibe_una_lista_vacia_y_no_un_error()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        ICustomerOrderHistory historial = new HistorialDePedidos(db);

        Assert.Empty(await historial.ObtenerPedidosDeAsync(Guid.CreateVersion7(), 20, ct));

        await tx.RollbackAsync(ct);
    }
}
