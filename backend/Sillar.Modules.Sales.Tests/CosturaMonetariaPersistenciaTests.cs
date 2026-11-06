using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// La barrera de moneda y la sonda de pedidos contra PostgreSQL real, con dos conexiones.
/// </summary>
/// <remarks>
/// <para>
/// <b>Preparadas, no acreditadas.</b> Se escribieron en P1-POST-G6-R10-E6.1 y no se
/// ejecutaron en esa entrega: no había autorización para PostgreSQL físico. Hasta que se
/// ejecuten, la exclusión mutua CORE–Ventas <b>no está demostrada</b>.
/// </para>
/// <para>
/// <b>El lado de CORE es el contrato, no CORE.</b> Las pruebas de M03 no pueden usar el
/// servicio interno de CORE: la otra conexión toma <see cref="MonetaryAuthorityLock.AcquireExclusive"/>
/// tal cual lo publica el contrato, que es exactamente lo que CORE E5.2 ejecuta.
/// </para>
/// <para>
/// <b>No destructivas</b>, como el resto de <c>SalesDb</c>: todo lo de Ventas va en una
/// transacción que se deshace. La consecuencia es que «Ventas confirma y la sonda de CORE
/// lo ve» no se ejecuta aquí con un <c>COMMIT</c> real: se prueba que CORE espera mientras
/// el pedido está a medio crear, y que la sonda ve un pedido presente.
/// </para>
/// </remarks>
[Collection("SalesDb")]
public sealed class CosturaMonetariaPersistenciaTests(SalesDbFixture fixture)
    : IClassFixture<SalesDbFixture>
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string BloqueosDeMoneda =
        "SELECT pid, mode, granted FROM pg_locks " +
        "WHERE locktype = 'advisory' AND classid = 1397312588 AND objid = 1 AND objsubid = 2";

    // ================================================================== sonda

    [Fact]
    public async Task Sin_ninguna_fila_en_sales_orders_la_sonda_dice_que_no_hay()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TablaDePedidosVaciaAsync(db, ct);

        Assert.Equal(MonetaryRecordsState.None, await SondaDeRegistrosMonetarios.ClasificarAsync(db, ct));

        await tx.RollbackAsync(ct);
    }

    [Theory]
    [InlineData(true, "PEN", "pending_payment")]
    [InlineData(false, "PEN", "cancelled")]   // de baja lógica
    [InlineData(true, null, "delivered")]     // histórico, sin moneda
    [InlineData(false, null, "expired")]
    public async Task Cualquier_pedido_cuenta_activo_de_baja_historico_o_sin_moneda(bool activo, string? moneda, string estado)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TablaDePedidosVaciaAsync(db, ct);

        // D-2 (R10-E12): EF Core 10 no sabe a qué tipo de la base corresponde DBNull.Value
        // suelto y falla antes de llegar a PostgreSQL. La moneda va como parámetro de
        // Npgsql con su tipo declarado, text, y entonces el nulo es un NULL de verdad.
        var monedaOVacia = new NpgsqlParameter("moneda", NpgsqlDbType.Text) { Value = (object?)moneda ?? DBNull.Value };
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sales.orders (order_id, order_code, customer_id, customer_full_name, customer_email, " +
            "status, payment_due_at, total_amount, is_active, origin_node, currency_code, created_at) " +
            "VALUES ({0}, 'P-2026-9901', {1}, 'Ana Quispe', 'ana@ejemplo.pe', {2}, now(), 10, {3}, 'principal', {4}, now())",
            [Guid.CreateVersion7(), Guid.CreateVersion7(), estado, activo, monedaOVacia],
            ct);

        // Que la fila quedó como dice el caso, y no con otra moneda o sin la baja.
        var fila = (await db.Database.SqlQueryRaw<string>(
                "SELECT coalesce(currency_code, '<nulo>') || '|' || is_active::text || '|' || status AS \"Value\" " +
                "FROM sales.orders WHERE order_code = 'P-2026-9901'")
            .ToListAsync(ct)).Single();
        Assert.Equal($"{moneda ?? "<nulo>"}|{(activo ? "true" : "false")}|{estado}", fila);

        Assert.Equal(MonetaryRecordsState.Present, await SondaDeRegistrosMonetarios.ClasificarAsync(db, ct));

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Sin_el_schema_de_ventas_la_sonda_no_sabe()
    {
        // La base de mantenimiento del mismo servidor no tiene schema sales: es una
        // instalación sin M03 migrado. Solo se lee.
        var cadena = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = "postgres" }.ConnectionString;
        await using var db = new SalesDbContext(
            new DbContextOptionsBuilder<SalesDbContext>().UseNpgsql(cadena).Options,
            new NodeIdentity("principal"),
            TimeProvider.System);

        Assert.Equal(MonetaryRecordsState.Unknown, await Sonda(db).ProbeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task La_sonda_no_pide_el_bloqueo_y_responde_aunque_CORE_lo_tenga_en_exclusivo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var core = await AbrirAsync(ct);
        await using var txCore = await core.BeginTransactionAsync(ct);
        await TomarExclusivoAsync(core, txCore, ct);

        await using var db = fixture.NuevoContexto();
        var sonda = Sonda(db).ProbeAsync(ct);

        // Si pidiera el compartido, esperaría a CORE para siempre.
        var terminada = await Task.WhenAny(sonda, Task.Delay(TimeSpan.FromSeconds(10), ct));
        Assert.Same(sonda, terminada);
        Assert.NotEqual(MonetaryRecordsState.Unknown, await sonda);

        var bloqueos = await BloqueosAsync(ct);
        Assert.Single(bloqueos);                       // solo el de CORE
        Assert.Equal("ExclusiveLock", bloqueos[0].Mode);

        await txCore.RollbackAsync(ct);
    }

    // ================================================================== creación con la barrera

    [Fact]
    public async Task La_moneda_autentica_se_lee_con_el_bloqueo_compartido_ya_concedido_a_la_conexion_del_pedido()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (cliente, item) = await PrepararAsync(db, ct);
        var pid = await PidAsync(db, ct);

        var moneda = new MonedaQueMiraLosBloqueos(fixture.ConnectionString, pid);
        var r = await Creador(db, cliente, item, moneda).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

        Assert.True(r.Creado);
        Assert.Equal(2, moneda.Lecturas.Count);
        Assert.False(moneda.Lecturas[0], "el rechazo rápido va antes de la transacción y sin bloqueo");
        Assert.True(moneda.Lecturas[1], "la lectura auténtica va con el compartido concedido");
        Assert.Equal(pid, moneda.PidDeLaTransaccion);    // y por la conexión del pedido (D-1)
        Assert.Equal("PEN", await db.Orders.Where(o => o.OrderCode == r.OrderCode).Select(o => o.CurrencyCode).SingleAsync(ct));

        await tx.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));       // se soltó al deshacer
    }

    [Fact]
    public async Task Ventas_primero_CORE_espera_mientras_el_pedido_esta_a_medio_crear_y_entra_al_deshacerse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (cliente, item) = await PrepararAsync(db, ct);

        var r = await Creador(db, cliente, item, new Dobles.MonedaDice("PEN")).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);
        Assert.True(r.Creado);                         // creado, sin confirmar: el compartido sigue puesto

        // CORE con espera corta: no lo obtiene.
        await using (var impaciente = await AbrirAsync(ct))
        await using (var txImpaciente = await impaciente.BeginTransactionAsync(ct))
        {
            await EjecutarAsync(impaciente, txImpaciente, "SET LOCAL lock_timeout = '500ms'", ct);
            var error = await Assert.ThrowsAsync<PostgresException>(() => TomarExclusivoAsync(impaciente, txImpaciente, ct));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
        }

        // CORE sin espera corta: queda esperando, visible en pg_locks.
        await using var core = await AbrirAsync(ct);
        await using var txCore = await core.BeginTransactionAsync(ct);
        var exclusivo = TomarExclusivoAsync(core, txCore, ct);
        await EsperarAsync(async () => (await BloqueosAsync(ct)).Any(b => b.Mode == "ExclusiveLock" && !b.Granted), ct);
        Assert.False(exclusivo.IsCompleted);

        await tx.RollbackAsync(ct);                    // Ventas termina
        await exclusivo.WaitAsync(TimeSpan.FromSeconds(10), ct);

        var bloqueos = await BloqueosAsync(ct);
        Assert.Equal("ExclusiveLock", Assert.Single(bloqueos).Mode);
        Assert.True(bloqueos[0].Granted);
        await txCore.RollbackAsync(ct);
    }

    [Fact]
    public async Task CORE_primero_el_pedido_espera_y_lee_la_moneda_solo_despues_de_que_CORE_termina()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (cliente, item) = await PrepararAsync(db, ct);

        await using var core = await AbrirAsync(ct);
        await using var txCore = await core.BeginTransactionAsync(ct);
        await TomarExclusivoAsync(core, txCore, ct);

        var moneda = new MonedaContada("PEN");
        var creacion = Creador(db, cliente, item, moneda).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

        await EsperarAsync(async () => (await BloqueosAsync(ct)).Any(b => b.Mode == "ShareLock" && !b.Granted), ct);
        Assert.False(creacion.IsCompleted);
        Assert.Equal(1, moneda.Previas);               // solo el rechazo rápido: la auténtica espera
        Assert.Equal(0, moneda.EnTransaccion);

        await txCore.CommitAsync(ct);                  // CORE termina
        var r = await creacion.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.True(r.Creado);
        Assert.Equal(1, moneda.Previas);
        Assert.Equal(1, moneda.EnTransaccion);
        await tx.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));
    }

    [Fact]
    public async Task Si_CORE_retiene_el_bloqueo_mas_que_la_espera_el_pedido_no_nace_y_no_llega_a_leer_la_moneda()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto(new Dobles.RelojFijo(Ahora));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (cliente, item) = await PrepararAsync(db, ct);

        await using var core = await AbrirAsync(ct);
        await using var txCore = await core.BeginTransactionAsync(ct);
        await TomarExclusivoAsync(core, txCore, ct);

        var moneda = new MonedaContada("PEN");
        var r = await Creador(db, cliente, item, moneda).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

        Assert.Equal(MotivoDeNoCreacion.MonedaEnCambio, r.Motivo);
        Assert.Null(r.OrderCode);
        Assert.Equal(1, moneda.Previas);
        Assert.Equal(0, moneda.EnTransaccion);

        await tx.RollbackAsync(ct);
        await txCore.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));
    }

    [Fact]
    public async Task Varios_pedidos_tienen_el_compartido_a_la_vez()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var a = fixture.NuevoContexto();
        await using var b = fixture.NuevoContexto();
        await using var txA = await a.Database.BeginTransactionAsync(ct);
        await using var txB = await b.Database.BeginTransactionAsync(ct);

        await BarreraMonetariaDeVentas.AdquirirCompartidaAsync(a, ct);
        await BarreraMonetariaDeVentas.AdquirirCompartidaAsync(b, ct).WaitAsync(TimeSpan.FromSeconds(10), ct);

        var bloqueos = await BloqueosAsync(ct);
        Assert.Equal(2, bloqueos.Count(x => x.Mode == "ShareLock" && x.Granted));

        await txA.RollbackAsync(ct);
        await txB.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));
    }

    [Fact]
    public async Task La_espera_acotada_no_se_queda_puesta_en_la_transaccion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '7s'", ct);

        await BarreraMonetariaDeVentas.AdquirirCompartidaAsync(db, ct);

        var valor = (await db.Database.SqlQueryRaw<string>("SELECT current_setting('lock_timeout') AS \"Value\"").ToListAsync(ct)).Single();
        Assert.Equal("7s", valor);
        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Ventas_y_el_lado_de_CORE_estan_en_la_misma_base_y_el_exclusivo_excluye_al_compartido()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.NuevoContexto();
        await using var core = await AbrirAsync(ct);

        var ventas = (await db.Database.SqlQueryRaw<string>(
            "SELECT current_database() || '@' || coalesce(inet_server_addr()::text, 'local') || ':' || " +
            "coalesce(inet_server_port()::text, '') || '#' || pg_postmaster_start_time()::text AS \"Value\"")
            .ToListAsync(ct)).Single();
        await using var comando = new NpgsqlCommand(
            "SELECT current_database() || '@' || coalesce(inet_server_addr()::text, 'local') || ':' || " +
            "coalesce(inet_server_port()::text, '') || '#' || pg_postmaster_start_time()::text", core);
        var delCore = (string)(await comando.ExecuteScalarAsync(ct))!;

        Assert.Equal(delCore, ventas);
    }

    // ================================================================== montaje

    private SondaDeRegistrosMonetarios Sonda(SalesDbContext db) => new(db, NullLogger<SondaDeRegistrosMonetarios>.Instance);

    private static CreadorDePedidos Creador(SalesDbContext db, Guid cliente, Guid item, IAutoridadDeMoneda moneda)
        => new(
            db,
            new CongeladorDeCliente(new Dobles.M04Dice(Dobles.Cliente(cliente))),
            new Dobles.CatalogoDice().Con(item, activo: true, precio: 8.50m),
            new OrderCodeAllocator(db, new NodeIdentity("principal"), new Dobles.RelojFijo(Ahora)),
            new Dobles.AjustesDice(),
            moneda,
            new Dobles.RelojFijo(Ahora),
            NullLogger<CreadorDePedidos>.Instance);

    /// <summary>
    /// Dentro de la transacción de la prueba, <c>sales.orders</c> pasa a ser una tabla
    /// vacía con las mismas columnas. Se deshace con el <c>ROLLBACK</c>.
    /// </summary>
    private static async Task TablaDePedidosVaciaAsync(SalesDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE sales.orders RENAME TO orders_r10_e61_aparte", ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE sales.orders (LIKE sales.orders_r10_e61_aparte INCLUDING DEFAULTS INCLUDING CONSTRAINTS)", ct);
    }

    private static async Task<(Guid Cliente, Guid Item)> PrepararAsync(SalesDbContext db, CancellationToken ct)
    {
        var cliente = Guid.CreateVersion7();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO crm.customers (customer_id, full_name, email, origin_node) VALUES ({0}, {1}, {2}, 'principal')",
            [cliente, $"Cliente {cliente:N}", $"{cliente:N}@prueba.pe"], ct);

        var producto = Guid.CreateVersion7();
        var item = Guid.CreateVersion7();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO catalog.products (id, name, slug, origin_node) VALUES ({0}, {1}, {2}, 'principal')",
            [producto, $"Producto de prueba {producto:N}", $"producto-{producto:N}"], ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO catalog.product_items (id, product_id, origin_node) VALUES ({0}, {1}, 'principal')",
            [item, producto], ct);

        return (cliente, item);
    }

    private static async Task<int> PidAsync(SalesDbContext db, CancellationToken ct)
        => (await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").ToListAsync(ct)).Single();

    private async Task<NpgsqlConnection> AbrirAsync(CancellationToken ct)
    {
        var conexion = new NpgsqlConnection(fixture.ConnectionString);
        await conexion.OpenAsync(ct);
        return conexion;
    }

    private static async Task TomarExclusivoAsync(NpgsqlConnection conexion, NpgsqlTransaction tx, CancellationToken ct)
    {
        var sql = MonetaryAuthorityLock.AcquireExclusive();
        await using var comando = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1, $2)", conexion, tx);
        comando.Parameters.Add(new NpgsqlParameter { Value = (int)sql.GetArgument(0)! });
        comando.Parameters.Add(new NpgsqlParameter { Value = (int)sql.GetArgument(1)! });
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task EjecutarAsync(NpgsqlConnection conexion, NpgsqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(sql, conexion, tx);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private async Task<List<(int Pid, string Mode, bool Granted)>> BloqueosAsync(CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = new NpgsqlCommand(BloqueosDeMoneda, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var bloqueos = new List<(int, string, bool)>();
        while (await lector.ReadAsync(ct))
        {
            bloqueos.Add((lector.GetInt32(0), lector.GetString(1), lector.GetBoolean(2)));
        }

        return bloqueos;
    }

    private static async Task EsperarAsync(Func<Task<bool>> condicion, CancellationToken ct)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!await condicion())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new TimeoutException("La condición no se cumplió en 10 s.");
            }

            await Task.Delay(50, ct);
        }
    }

    /// <summary>Responde la moneda dada y anota cuántas veces se le preguntó, por lectura.</summary>
    private sealed class MonedaContada(string moneda) : IAutoridadDeMoneda
    {
        private int previas;
        private int enTransaccion;

        public int Previas => Volatile.Read(ref previas);

        public int EnTransaccion => Volatile.Read(ref enTransaccion);

        public Task<LecturaDeMonedaDeVenta> MonedaDeVentaAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref previas);
            return Task.FromResult(LecturaDeMonedaDeVenta.Acreditada(moneda));
        }

        public Task<LecturaDeMonedaDeVenta> LeerEnTransaccionAsync(DbTransaction transaccion, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref enTransaccion);
            return Task.FromResult(LecturaDeMonedaDeVenta.Acreditada(moneda));
        }
    }

    /// <summary>
    /// Responde PEN y, en cada lectura, mira desde otra conexión si la conexión del pedido
    /// tiene ya concedido el compartido de moneda.
    /// </summary>
    private sealed class MonedaQueMiraLosBloqueos(string cadena, int pidDelPedido) : IAutoridadDeMoneda
    {
        public List<bool> Lecturas { get; } = [];

        /// <summary>El proceso del servidor detrás de la transacción que recibió la lectura transaccional.</summary>
        public int? PidDeLaTransaccion { get; private set; }

        public async Task<LecturaDeMonedaDeVenta> MonedaDeVentaAsync(CancellationToken cancellationToken)
        {
            Lecturas.Add(await CompartidoConcedidoAsync(cancellationToken));
            return LecturaDeMonedaDeVenta.Acreditada("PEN");
        }

        public async Task<LecturaDeMonedaDeVenta> LeerEnTransaccionAsync(DbTransaction transaccion, CancellationToken cancellationToken)
        {
            Lecturas.Add(await CompartidoConcedidoAsync(cancellationToken));

            await using var comando = transaccion.Connection!.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = "SELECT pg_backend_pid()";
            PidDeLaTransaccion = Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken));
            return LecturaDeMonedaDeVenta.Acreditada("PEN");
        }

        private async Task<bool> CompartidoConcedidoAsync(CancellationToken cancellationToken)
        {
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(cancellationToken);
            await using var comando = new NpgsqlCommand(
                BloqueosDeMoneda + " AND mode = 'ShareLock' AND granted AND pid = $1", conexion);
            comando.Parameters.Add(new NpgsqlParameter { Value = pidDelPedido });
            return await comando.ExecuteScalarAsync(cancellationToken) is not null;
        }
    }
}
