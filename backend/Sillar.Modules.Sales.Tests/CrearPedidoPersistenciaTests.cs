using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// La creación del pedido contra PostgreSQL real.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí no vale una prueba en memoria</b>, y la razón acaba de quedar escrita en
/// `ANTES-DE-EMPEZAR-UN-MODULO.md` a raíz de C15: nada que solo se rompa cuando EF
/// traduce queda demostrado sin ejecutar contra PostgreSQL. Lo que se acredita aquí es
/// precisamente eso — que el pedido, sus líneas y su asiento se escriben, que las dos
/// claves foráneas cruzadas aceptan, que el contador se mueve dentro de la transacción
/// y que un rollback lo devuelve.
/// </para>
/// <para>
/// <b>Y no se usa <c>Assert.Skip</c>.</b> La misma regla dice que una prueba omitida no
/// acredita ejecución real. Si falta el schema, el fixture falla y lo dice.
/// </para>
/// </remarks>
[Collection("SalesDb")]
public sealed class CrearPedidoPersistenciaTests(SalesDbFixture fixture)
    : IClassFixture<SalesDbFixture>
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static async Task CrearClienteAsync(SalesDbContext db, Guid id, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO crm.customers (customer_id, full_name, email, origin_node) " +
            "VALUES ({0}, {1}, {2}, 'principal')",
            [id, $"Cliente {id:N}", $"{id:N}@prueba.pe"],
            ct);

    /// <summary>Una variante real del catálogo: la FK cruzada es dura y RESTRICT.</summary>
    private static async Task<Guid?> AlgunItemDelCatalogoAsync(SalesDbContext db, CancellationToken ct)
    {
        var ids = await db.Database
            .SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM catalog.product_items LIMIT 1")
            .ToListAsync(ct);

        return ids.Count == 0 ? null : ids[0];
    }

    private CreadorDePedidos Creador(
        SalesDbContext db, Guid cliente, Dobles.CatalogoDice catalogo, bool verificado = true)
        => new(
            db,
            new CongeladorDeCliente(new Dobles.M04Dice(Dobles.Cliente(cliente, verificado))),
            catalogo,
            new OrderCodeAllocator(db, new Dobles.AjustesDice(), new Sillar.Shared.Replication.NodeIdentity("principal"), new Dobles.RelojFijo(Ahora)),
            new Dobles.AjustesDice(),
            new Dobles.RelojFijo(Ahora));

    private static async Task<long> SerieAsync(SalesDbContext db, CancellationToken ct)
    {
        var v = await db.Database
            .SqlQueryRaw<int>(
                "SELECT COALESCE(MAX(last_number), 0) AS \"Value\" FROM sales.order_series " +
                "WHERE node_code = 'principal' AND year = 2026")
            .ToListAsync(ct);
        return v.Count == 0 ? 0 : v[0];
    }

    // ================================================================
    // El camino que funciona, y el congelado.
    // ================================================================

    [Fact]
    public async Task Un_pedido_se_crea_con_el_precio_de_M01_y_su_snapshot_congelado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var item = await AlgunItemDelCatalogoAsync(db, ct);
        Assert.NotNull(item);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var catalogo = new Dobles.CatalogoDice()
            .Con(item!.Value, activo: true, precio: 8.50m, nombre: "Cuaderno universitario A4 100 hojas");

        var r = await Creador(db, ana, catalogo)
            .CrearAsync(ana, [new LineaPedida(item.Value, 3)], ct);

        Assert.True(r.Creado);
        Assert.Equal(25.50m, r.TotalAmount);                     // 8.50 × 3, calculado aquí
        Assert.StartsWith("P-2026-", r.OrderCode!, StringComparison.Ordinal);
        Assert.Equal(Ahora.AddHours(48), r.PaymentDueAt);

        var pedido = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderCode == r.OrderCode, ct);
        Assert.Equal(OrderStatus.PendingPayment, pedido.Status);
        Assert.Equal("Ana Quispe", pedido.CustomerFullName);     // cliente congelado (D1)
        Assert.Equal("dni", pedido.CustomerDocumentType);

        var linea = await db.OrderLines.AsNoTracking().SingleAsync(l => l.OrderId == pedido.OrderId, ct);
        Assert.Equal(8.50m, linea.UnitPrice);                     // precio congelado
        Assert.Equal("Cuaderno universitario A4 100 hojas", linea.ProductName);
        Assert.Equal("Verde", linea.VariantValue);
        Assert.Equal("unidad", linea.SaleUnit);
        Assert.Equal(item.Value, linea.ItemId);

        // El primer asiento: nace sin estado anterior y sin atribuir a ningún
        // trabajador, porque lo creó el cliente.
        var asiento = await db.OrderStatusChanges.AsNoTracking()
            .SingleAsync(c => c.OrderId == pedido.OrderId, ct);
        Assert.Null(asiento.FromStatus);
        Assert.Equal(OrderStatus.PendingPayment, asiento.ToStatus);
        Assert.Null(asiento.ChangedBy);
        Assert.Null(asiento.ChangedByAdminUserLocalId);
        Assert.Null(asiento.ChangedByAdminUserHomeNode);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_snapshot_sigue_congelado_cuando_M01_cambia_despues()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var item = await AlgunItemDelCatalogoAsync(db, ct);
        Assert.NotNull(item);
        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var catalogo = new Dobles.CatalogoDice().Con(item!.Value, true, 8.50m, "Nombre de entonces");
        var r = await Creador(db, ana, catalogo).CrearAsync(ana, [new LineaPedida(item.Value, 1)], ct);

        // M01 cambia: otro precio y otro nombre. El pedido no se entera.
        catalogo.Con(item.Value, true, 99.00m, "Nombre de ahora");

        // OrderLine no tiene propiedad de navegación a propósito: la FK se declara sin
        // ella, así que se resuelve el pedido primero.
        var pedidoId = await db.Orders.AsNoTracking()
            .Where(o => o.OrderCode == r.OrderCode).Select(o => o.OrderId).SingleAsync(ct);

        var linea = await db.OrderLines.AsNoTracking()
            .SingleAsync(l => l.OrderId == pedidoId, ct);

        Assert.Equal(8.50m, linea.UnitPrice);
        Assert.Equal("Nombre de entonces", linea.ProductName);

        await tx.RollbackAsync(ct);
    }

    // ================================================================
    // Los negativos.
    // ================================================================

    [Fact]
    public async Task Un_item_inexistente_o_inactivo_no_produce_pedido()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var inexistente = Guid.CreateVersion7();
        var deBaja = Guid.CreateVersion7();
        var catalogo = new Dobles.CatalogoDice()
            .SinItem(inexistente)
            .Con(deBaja, activo: false, precio: 8.50m);

        foreach (var malo in new[] { inexistente, deBaja })
        {
            var r = await Creador(db, ana, catalogo).CrearAsync(ana, [new LineaPedida(malo, 1)], ct);

            Assert.False(r.Creado);
            Assert.Equal(MotivoDeNoCreacion.ItemNoVendible, r.Motivo);
            Assert.Equal(malo, r.ItemConflictivo);
        }

        Assert.Equal(0, await db.Orders.CountAsync(o => o.CustomerId == ana, ct));

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Un_precio_null_no_entra_al_pedido_y_un_precio_cero_si()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var item = await AlgunItemDelCatalogoAsync(db, ct);
        Assert.NotNull(item);
        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        // Nulo: se cotiza, fuera del pedido.
        var aConsultar = new Dobles.CatalogoDice().Con(item!.Value, true, precio: null);
        var r1 = await Creador(db, ana, aConsultar).CrearAsync(ana, [new LineaPedida(item.Value, 1)], ct);

        Assert.False(r1.Creado);
        Assert.Equal(MotivoDeNoCreacion.ItemAConsultar, r1.Motivo);
        Assert.Equal(item.Value, r1.ItemConflictivo);

        // Cero: es GRATIS y se vende. La distinción que ya mordió una vez.
        var gratis = new Dobles.CatalogoDice().Con(item.Value, true, precio: 0m);
        var r2 = await Creador(db, ana, gratis).CrearAsync(ana, [new LineaPedida(item.Value, 2)], ct);

        Assert.True(r2.Creado);
        Assert.Equal(0m, r2.TotalAmount);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_precio_de_la_linea_lo_pone_M01_y_no_quien_llama()
    {
        // No hay forma de manipularlo: la petición no tiene campo de precio (lo
        // vigila PeticionDePedidoTests) y la línea toma el que devuelve el catálogo,
        // sea el que sea. Aquí se comprueba el efecto: el número que acaba en la base
        // es el del catálogo, y nadie más lo propuso.
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var item = await AlgunItemDelCatalogoAsync(db, ct);
        Assert.NotNull(item);
        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var catalogo = new Dobles.CatalogoDice().Con(item!.Value, true, precio: 12.34m);
        var r = await Creador(db, ana, catalogo).CrearAsync(ana, [new LineaPedida(item.Value, 2)], ct);

        var pedidoId = await db.Orders.AsNoTracking()
            .Where(o => o.OrderCode == r.OrderCode).Select(o => o.OrderId).SingleAsync(ct);

        var linea = await db.OrderLines.AsNoTracking()
            .SingleAsync(l => l.OrderId == pedidoId, ct);

        Assert.Equal(12.34m, linea.UnitPrice);
        Assert.Equal(24.68m, r.TotalAmount);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Se_pregunta_por_la_vendibilidad_antes_de_tomar_el_precio()
    {
        // ObtenerItemAsync devuelve también las variantes de baja —su contrato lo
        // dice—, así que el snapshot por sí solo no acredita que se pueda vender.
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var deBaja = Guid.CreateVersion7();
        var catalogo = new Dobles.CatalogoDice().Con(deBaja, activo: false, precio: 8.50m);

        await Creador(db, ana, catalogo).CrearAsync(ana, [new LineaPedida(deBaja, 1)], ct);

        Assert.Equal(1, catalogo.VecesPreguntadoPorVendibilidad);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Un_correo_sin_verificar_no_llega_a_preguntar_por_el_catalogo()
    {
        // R-07, y el orden importa: si la cuenta no puede comprar no hace falta
        // molestar a M01 por ningún artículo.
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var catalogo = new Dobles.CatalogoDice().Con(Guid.CreateVersion7(), true, 8.50m);
        var r = await Creador(db, ana, catalogo, verificado: false)
            .CrearAsync(ana, [new LineaPedida(Guid.CreateVersion7(), 1)], ct);

        Assert.False(r.Creado);
        Assert.Equal(MotivoDeNoCreacion.CorreoSinVerificar, r.Motivo);
        Assert.Equal(0, catalogo.VecesPreguntadoPorVendibilidad);

        await tx.RollbackAsync(ct);
    }

    // ================================================================
    // El número y el rollback.
    // ================================================================

    [Fact]
    public async Task Un_pedido_que_no_llega_a_crearse_no_consume_numero()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);

        var antes = await SerieAsync(db, ct);

        // Un rechazo por «a consultar»: ni se llega a abrir la transacción interna.
        var aConsultar = Guid.CreateVersion7();
        var catalogo = new Dobles.CatalogoDice().Con(aConsultar, true, precio: null);
        await Creador(db, ana, catalogo).CrearAsync(ana, [new LineaPedida(aConsultar, 1)], ct);

        Assert.Equal(antes, await SerieAsync(db, ct));

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task El_rollback_de_la_transaccion_exterior_devuelve_el_numero()
    {
        // La prueba de continuidad sobre el camino completo: se crea un pedido de
        // verdad, se deshace todo, y el contador vuelve a donde estaba. Es lo que
        // nextval NO haría.
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));

        var antes = await SerieAsync(db, ct);

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var item = await AlgunItemDelCatalogoAsync(db, ct);
            Assert.NotNull(item);
            var ana = Guid.CreateVersion7();
            await CrearClienteAsync(db, ana, ct);

            var catalogo = new Dobles.CatalogoDice().Con(item!.Value, true, 8.50m);
            var r = await Creador(db, ana, catalogo).CrearAsync(ana, [new LineaPedida(item.Value, 1)], ct);

            Assert.True(r.Creado);
            Assert.Equal(antes + 1, await SerieAsync(db, ct));   // dentro: consumido

            await tx.RollbackAsync(ct);
        }

        Assert.Equal(antes, await SerieAsync(db, ct));            // fuera: devuelto
    }

    [Fact]
    public async Task Un_carrito_vacio_no_entra_en_confirmacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var r = await Creador(db, Guid.CreateVersion7(), new Dobles.CatalogoDice())
            .CrearAsync(Guid.CreateVersion7(), [], ct);

        Assert.False(r.Creado);
        Assert.Equal(MotivoDeNoCreacion.SinLineas, r.Motivo);

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Una_cantidad_no_positiva_no_produce_pedido()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ana = Guid.CreateVersion7();
        await CrearClienteAsync(db, ana, ct);
        var item = Guid.CreateVersion7();
        var catalogo = new Dobles.CatalogoDice().Con(item, true, 8.50m);

        foreach (var cantidad in new[] { 0, -2 })
        {
            var r = await Creador(db, ana, catalogo).CrearAsync(ana, [new LineaPedida(item, cantidad)], ct);

            Assert.False(r.Creado);
            Assert.Equal(MotivoDeNoCreacion.CantidadNoPositiva, r.Motivo);
        }

        await tx.RollbackAsync(ct);
    }
}
