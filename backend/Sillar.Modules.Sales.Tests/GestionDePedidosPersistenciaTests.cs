using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Tests;

[Collection("SalesDb")]
public sealed class GestionDePedidosPersistenciaTests(SalesDbFixture fixture) : IClassFixture<SalesDbFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
    private sealed class Reloj : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Autoridad : IModuleAuthorization
    {
        public AuthorizedAdmin Person { get; set; } = new(7, "Ana congelada", "nodo-de-la-cuenta", "ana@prueba.pe", AdminRole.Admin);
        public int Calls { get; private set; }
        public int? DenyAt { get; init; }
        public List<string> Operations { get; } = [];
        public Task<AuthorizedAdmin> RequireAsync(string module, string operation, CancellationToken ct)
        {
            Assert.Equal("sales", module); Operations.Add(operation);
            if (++Calls == DenyAt) throw new ModuleAccessDeniedException(403, "Permiso revocado.");
            return Task.FromResult(Person);
        }
    }
    private static GestionDePedidos Gestion(SalesDbContext db, Autoridad? authority = null)
        => new(db, authority ?? new(), new Reloj());
    private static RegistrarPagoManual Pago(decimal amount = 10, bool canFulfill = true)
        => new(Guid.NewGuid(), PaymentMethod.Yape, amount, "operacion-manual", canFulfill);
    private static async Task<Order> Crear(SalesDbContext db, string status = OrderStatus.PendingPayment, bool late = false)
    {
        var ct = TestContext.Current.CancellationToken;
        var customer = Guid.CreateVersion7();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO crm.customers (customer_id, full_name, email, origin_node) VALUES ({0}, 'Cliente físico', {1}, 'principal')",
            [customer, $"{customer:N}@prueba.pe"], ct);
        var p = new Order { OrderCode = $"2026-{Guid.CreateVersion7():N}", CustomerId = customer,
            CustomerFullName = "Cliente físico", CustomerEmail = $"{customer:N}@prueba.pe", TotalAmount = 10,
            CurrencyCode = "PEN", Status = status, PaymentDueAt = late ? Now.AddHours(-1) : Now.AddHours(48) };
        db.Orders.Add(p); await db.SaveChangesAsync(ct); return p;
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.PaymentToVerify)]
    [InlineData(OrderStatus.Expired)]
    public async Task Cobro_completo_conserva_autoria_real_y_prepara_incluso_vencido(string state)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj());
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db, state, late: true); var a = new Autoridad();
        Assert.True((await Gestion(db, a).RegistrarPagoAsync(p.OrderCode, Pago(), ct)).Success);
        var saved = await db.OrderPayments.AsNoTracking().SingleAsync(x => x.OrderId == p.OrderId, ct);
        Assert.Equal("Ana congelada", saved.RegisteredBy); Assert.Equal(7, saved.RegisteredByAdminUserLocalId);
        Assert.Equal("nodo-de-la-cuenta", saved.RegisteredByAdminUserHomeNode);
        Assert.Equal("principal", saved.OriginNode); Assert.Equal("PEN", saved.CurrencyCode);
        Assert.True(saved.WasLate); Assert.Null(saved.ExternalSettlementId); Assert.Equal(PaymentSource.Staff, saved.Source);
        Assert.Equal(OrderStatus.Preparing, p.Status); Assert.Equal(new[] { "pay", "pay" }, a.Operations);
        a.Person = a.Person with { DisplayName = "Ana renombrada" };
        Assert.Equal("Ana congelada", (await Gestion(db, a).ObtenerAsync(p.OrderCode, ct))!.Payments.Single().RegisteredBy);
    }

    [Fact]
    public async Task Reintento_igual_no_duplica_y_reutilizacion_con_otro_importe_se_rechaza()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db); var input = Pago(); var service = Gestion(db);
        Assert.True((await service.RegistrarPagoAsync(p.OrderCode, input, ct)).Success);
        Assert.True((await service.RegistrarPagoAsync(p.OrderCode, input, ct)).Replayed);
        Assert.False((await service.RegistrarPagoAsync(p.OrderCode, input with { Amount = 9 }, ct)).Success);
        Assert.Equal(1, await db.OrderPayments.CountAsync(x => x.OrderId == p.OrderId, ct));
        Assert.Equal(1, await db.OrderStatusChanges.CountAsync(x => x.OrderId == p.OrderId, ct));
    }

    [Fact]
    public async Task Pago_sin_posibilidad_de_atender_deja_pendiente_visible_sin_octavo_estado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db, OrderStatus.Expired, true); var service = Gestion(db);
        Assert.True((await service.RegistrarPagoAsync(p.OrderCode, Pago(canFulfill: false), ct)).Success);
        Assert.Equal(OrderStatus.Expired, p.Status);
        Assert.True((await service.ObtenerAsync(p.OrderCode, ct))!.Order.NeedsAttention);
        Assert.True((await service.CambiarAsync(p.OrderCode, new("prepare", p.RowVersion, null), ct)).Success);
        Assert.False((await service.ObtenerAsync(p.OrderCode, ct))!.Order.NeedsAttention);
    }

    [Fact]
    public async Task Abono_parcial_no_prepara_y_el_segundo_completa_sin_sobrepago()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db); var service = Gestion(db);
        Assert.True((await service.RegistrarPagoAsync(p.OrderCode, Pago(4), ct)).Success);
        Assert.Equal(OrderStatus.PendingPayment, p.Status);
        Assert.False((await service.CambiarAsync(p.OrderCode, new("prepare", p.RowVersion, null), ct)).Success);
        Assert.False((await service.RegistrarPagoAsync(p.OrderCode, Pago(7), ct)).Success);
        Assert.True((await service.RegistrarPagoAsync(p.OrderCode, Pago(6), ct)).Success);
        Assert.Equal(OrderStatus.Preparing, p.Status);
        Assert.False((await service.RegistrarPagoAsync(p.OrderCode, Pago(1), ct)).Success);
        Assert.Equal(10m, await db.OrderPayments.Where(x => x.OrderId == p.OrderId).SumAsync(x => x.Amount, ct));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Permiso_rechazado_antes_o_despues_del_bloqueo_no_asienta_nada(int denyAt)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db); var authority = new Autoridad { DenyAt = denyAt };
        var error = await Assert.ThrowsAsync<ModuleAccessDeniedException>(() => Gestion(db, authority).RegistrarPagoAsync(p.OrderCode, Pago(), ct));
        Assert.Equal(403, error.StatusCode); Assert.False(await db.OrderPayments.AnyAsync(x => x.OrderId == p.OrderId, ct));
        Assert.Equal(OrderStatus.PendingPayment, p.Status);
    }

    [Fact]
    public async Task El_personal_no_salta_estados_y_un_detalle_antiguo_no_cambia_el_actual()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db); var service = Gestion(db);
        await service.RegistrarPagoAsync(p.OrderCode, Pago(), ct);
        Assert.False((await service.CambiarAsync(p.OrderCode, new("deliver", p.RowVersion, null), ct)).Success);
        var old = p.RowVersion;
        Assert.True((await service.CambiarAsync(p.OrderCode, new("ready", old, null), ct)).Success);
        Assert.False((await service.CambiarAsync(p.OrderCode, new("deliver", old, null), ct)).Success);
        Assert.True((await service.CambiarAsync(p.OrderCode, new("deliver", p.RowVersion, null), ct)).Success);
        Assert.False((await service.CambiarAsync(p.OrderCode, new("cancel", p.RowVersion, "Solicitud"), ct)).Success);
    }

    [Fact]
    public async Task Cancelacion_exige_motivo_y_lo_congela_con_la_persona()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db); var service = Gestion(db);
        Assert.False((await service.CambiarAsync(p.OrderCode, new("cancel", p.RowVersion, "  "), ct)).Success);
        Assert.True((await service.CambiarAsync(p.OrderCode, new("cancel", p.RowVersion, "  El cliente lo solicitó  "), ct)).Success);
        var change = await db.OrderStatusChanges.AsNoTracking().SingleAsync(x => x.OrderId == p.OrderId, ct);
        Assert.Equal("El cliente lo solicitó", change.Reason); Assert.Equal("Ana congelada", change.ChangedBy);
        Assert.Equal("nodo-de-la-cuenta", change.ChangedByAdminUserHomeNode);
    }

    [Fact]
    public async Task Vencimiento_no_pisa_un_cobro_en_curso_y_relee_el_estado_confirmado()
    {
        // Esta prueba confirma filas y necesita la identidad de la base propia del arnés.
        var name = new NpgsqlConnectionStringBuilder(fixture.ConnectionString).Database;
        Assert.StartsWith("sillar_verify_", name, StringComparison.Ordinal);
        Assert.Equal(name, Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE"));
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj());
        var p = await Crear(db, late: true);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDePedido.ObtenerAsync(db, p.OrderCode, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var other = fixture.NuevoContexto(new Reloj());
        Assert.DoesNotContain(p.OrderCode, (await new VencimientoDePlazos(other, new Reloj()).VencerLoCumplidoAsync(timeout.Token)).Codigos);
        Assert.True((await Gestion(db).RegistrarPagoAsync(p.OrderCode, Pago(), ct)).Success);
        await tx.CommitAsync(ct);
        Assert.DoesNotContain(p.OrderCode, (await new VencimientoDePlazos(other, new Reloj()).VencerLoCumplidoAsync(ct)).Codigos);
        Assert.Equal(OrderStatus.Preparing, (await other.Orders.AsNoTracking().SingleAsync(x => x.OrderId == p.OrderId, ct)).Status);
    }

    [Fact]
    public async Task Dos_cobros_simultaneos_del_mismo_intento_dejan_un_solo_hecho()
    {
        var name = new NpgsqlConnectionStringBuilder(fixture.ConnectionString).Database;
        Assert.StartsWith("sillar_verify_", name, StringComparison.Ordinal);
        Assert.Equal(name, Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE"));
        var ct = TestContext.Current.CancellationToken;
        await using var setup = fixture.NuevoContexto(new Reloj()); var p = await Crear(setup); var input = Pago();
        await using var first = fixture.NuevoContexto(new Reloj()); await using var second = fixture.NuevoContexto(new Reloj());
        var results = await Task.WhenAll(Gestion(first).RegistrarPagoAsync(p.OrderCode, input, ct), Gestion(second).RegistrarPagoAsync(p.OrderCode, input, ct));
        Assert.All(results, x => Assert.True(x.Success)); Assert.Single(results, x => x.Replayed);
        Assert.Equal(1, await setup.OrderPayments.CountAsync(x => x.OrderId == p.OrderId, ct));
    }
    [Theory]
    [InlineData("UPDATE sales.order_payments SET registered_by = 'Otra persona' WHERE order_id = {0}", "23514")]
    [InlineData("UPDATE sales.order_status_changes SET changed_by_admin_user_home_node = 'otro' WHERE order_id = {0}", "23514")]
    [InlineData("DELETE FROM sales.orders WHERE order_id = {0}", "23503")]
    public async Task SQL_directo_no_reescribe_autoria_ni_borra_el_pedido_con_pagos(string sql, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db); await Gestion(db).RegistrarPagoAsync(p.OrderCode, Pago(), ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, [p.OrderId], ct));
        Assert.Equal(code, error.SqlState);
    }

    [Fact]
    public async Task La_baja_de_migracion_no_descarta_pagos_ni_motivos_conservados()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await Crear(db); await Gestion(db).RegistrarPagoAsync(p.OrderCode, Pago(), ct);
        var guard = new Sillar.Modules.Sales.Migrations.OperacionesManualesDePedidos().DownOperations
            .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().First().Sql;
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(guard, ct));
        Assert.Contains("No se puede retirar la evidencia", error.MessageText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task La_desinstalacion_rechaza_objetos_desconocidos_y_dependencias_ajenas_sin_borrado_parcial(bool foreignDependency)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Reloj()); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "scripts", "verificar.mjs"))) root = root.Parent;
        Assert.NotNull(root);
        var script = await File.ReadAllTextAsync(Path.Combine(root.FullName, "database", "modules", "sales", "99_drop.sql"), ct);
        await db.Database.ExecuteSqlRawAsync(foreignDependency
            ? "CREATE SCHEMA prueba_frontera_ventas; CREATE TABLE prueba_frontera_ventas.referencia (order_id uuid REFERENCES sales.orders(order_id))"
            : "CREATE TABLE sales.objeto_desconocido_de_prueba (id integer)", ct);
        await tx.CreateSavepointAsync("antes_de_baja", ct);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(script, ct));
        await tx.RollbackToSavepointAsync("antes_de_baja", ct);
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('sales.orders') IS NOT NULL AS \"Value\"").SingleAsync(ct));
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('sales.__migrations') IS NOT NULL AS \"Value\"").SingleAsync(ct));
    }

}
