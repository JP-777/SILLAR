using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// R10-E13-B contra PostgreSQL real y con los lectores <b>reales</b> de CORE: el pedido
/// crea, lee la moneda y numera con una sola conexión mientras retiene el bloqueo (D-1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Preparadas, no acreditadas.</b> Se escribieron en P1-POST-G6-R10-E13-B sin
/// autorización para PostgreSQL físico, y no se ejecutaron. Hasta que se ejecuten, que D-1
/// esté eliminado <b>no está demostrado</b>.
/// </para>
/// <para>
/// <b>CORE real, no simulado.</b> <see cref="IInstallationCurrencyAuthority"/> e
/// <see cref="IInstallationCurrencyTransactionalAuthority"/> salen del registro público de
/// CORE (<c>AddCoreEssentials</c> y <c>AddCoreAuthentication</c>), con la misma cadena —y
/// por tanto el mismo pool— que el contexto de Ventas, como en producción («Default»).
/// </para>
/// <para>
/// <b>Precondición de las que crean pedido:</b> la base tiene la moneda acreditada por la
/// operación real de CORE (<c>PUT /api/admin/settings/installation-currency</c> como
/// <c>super_admin</c>). Si no la tiene, fallan diciéndolo; no se saltan.
/// </para>
/// <para>
/// <b>No destructivas.</b> Nada se confirma. Para que la lectura previa —que va con la
/// conexión de CORE— no compita con la prueba por el pool, la transacción de la prueba se
/// abre <b>justo después</b> de esa lectura (<see cref="PrestaLaTransaccionTrasLaPrevia"/>):
/// el creador se une a ella como a la de cualquier llamador, y la prueba la deshace. Así el
/// orden de uso del pool es el de producción: la lectura previa suelta su conexión antes de
/// que Ventas tome la suya.
/// </para>
/// <para>
/// <b>Por qué discriminan.</b> Con el diseño de R10-E6.1 (segunda lectura con la conexión de
/// CORE bajo el bloqueo), un pool de una conexión ya ocupada por el pedido no tiene nada que
/// dar: la lectura acaba en «pool agotado». Con el de E13-B no hace falta otra.
/// </para>
/// </remarks>
[Collection("SalesDb")]
public sealed class UnaConexionBajoElBloqueoPersistenciaTests(SalesDbFixture fixture)
    : IClassFixture<SalesDbFixture>
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private const string BloqueosDeMoneda =
        "SELECT pid, mode, granted FROM pg_locks " +
        "WHERE locktype = 'advisory' AND classid = 1397312588 AND objid = 1 AND objsubid = 2";

    // ================================================================== una conexión

    [Fact]
    public async Task Con_un_pool_de_una_sola_conexion_el_pedido_nace_y_CORE_lee_por_la_conexion_del_bloqueo()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = Pool(maximo: 1, espera: 3);
        var moneda = await MonedaAcreditadaAsync(ct);

        await using var core = Core(cadena);
        await using var ambito = core.CreateAsyncScope();
        await using var db = Ventas(cadena);
        var (cliente, item) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var autoridad = new PrestaLaTransaccionTrasLaPrevia(Adaptador(ambito.ServiceProvider), db, cliente, item);

        var r = await Creador(db, cliente, item, autoridad).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

        Assert.True(r.Creado, $"motivo: {r.Motivo}");
        Assert.Equal(moneda, await db.Orders.Where(o => o.OrderCode == r.OrderCode).Select(o => o.CurrencyCode).SingleAsync(ct));
        Assert.Equal(autoridad.PidDeLaPrestada, autoridad.PidEnLaLectura);   // la misma conexión
        Assert.True(autoridad.CompartidoConcedidoEnLaLectura);
        Assert.Equal(1, await ConexionesDelPoolAsync(cadena, ct));

        await db.Database.CurrentTransaction!.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));
        Assert.Equal(0, await PedidosAsync(r.OrderCode!, ct));               // nada a medias
    }

    [Fact]
    public async Task Con_un_pool_reducido_y_pedidos_a_la_vez_ninguno_se_queda_sin_conexion()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = Pool(maximo: 2, espera: 15);
        await MonedaAcreditadaAsync(ct);

        await using var core = Core(cadena);
        var maximo = 0;
        using var vigilancia = new CancellationTokenSource();
        var vigia = Task.Run(async () =>
        {
            while (!vigilancia.IsCancellationRequested)
            {
                var ahora = await ConexionesDelPoolAsync(cadena, CancellationToken.None);
                InterlockedMax(ref maximo, ahora);
                await Task.Delay(20, CancellationToken.None);
            }
        }, CancellationToken.None);

        var codigos = new ConcurrentBag<string>();
        var pedidos = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            await using var ambito = core.CreateAsyncScope();
            await using var db = Ventas(cadena);
            var (cliente, item) = (Guid.CreateVersion7(), Guid.CreateVersion7());
            var autoridad = new PrestaLaTransaccionTrasLaPrevia(Adaptador(ambito.ServiceProvider), db, cliente, item);

            var r = await Creador(db, cliente, item, autoridad).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

            if (db.Database.CurrentTransaction is { } tx)
            {
                await tx.RollbackAsync(ct);
            }

            Assert.True(r.Creado, $"motivo: {r.Motivo}");
            Assert.Equal(autoridad.PidDeLaPrestada, autoridad.PidEnLaLectura);
            codigos.Add(r.OrderCode!);
        }, ct)).ToArray();

        await Task.WhenAll(pedidos).WaitAsync(TimeSpan.FromSeconds(60), ct);
        await vigilancia.CancelAsync();
        await vigia;

        Assert.Equal(6, codigos.Count);
        Assert.InRange(maximo, 1, 2);                                        // nunca más que el pool
        Assert.Empty(await BloqueosAsync(ct));
        foreach (var codigo in codigos)
        {
            Assert.Equal(0, await PedidosAsync(codigo, ct));
        }
    }

    [Fact]
    public async Task Si_la_unica_conexion_esta_ocupada_por_el_llamador_la_lectura_previa_no_esta_disponible_y_no_se_dice_que_falte_moneda()
    {
        // Lo que E12 vio como «falta configurar la moneda» con el pool agotado: ahora es
        // «no disponible», y no se llega a tomar el bloqueo.
        var ct = TestContext.Current.CancellationToken;
        var cadena = Pool(maximo: 1, espera: 2);

        await using var core = Core(cadena);
        await using var ambito = core.CreateAsyncScope();
        await using var db = Ventas(cadena);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var (cliente, item) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var r = await Creador(db, cliente, item, Adaptador(ambito.ServiceProvider)).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.Empty(await BloqueosAsync(ct));
        await tx.RollbackAsync(ct);
    }

    // ================================================================== errores de contrato

    [Fact]
    public async Task Una_transaccion_prestada_que_en_el_servidor_es_repeatable_read_la_rechaza_CORE_y_no_nace_nada()
    {
        // El cliente no declara aislamiento (pasa el filtro de Ventas); el servidor sí lo
        // tiene. Lo caza la comprobación de CORE en la propia transacción.
        var ct = TestContext.Current.CancellationToken;
        var cadena = Pool(maximo: 3, espera: 5);
        await MonedaAcreditadaAsync(ct);

        await using var core = Core(cadena);
        await using var ambito = core.CreateAsyncScope();
        await using var db = Ventas(cadena);
        var (cliente, item) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var autoridad = new PrestaLaTransaccionTrasLaPrevia(
            Adaptador(ambito.ServiceProvider), db, cliente, item, primeraSentencia: "SET TRANSACTION ISOLATION LEVEL REPEATABLE READ");

        var r = await Creador(db, cliente, item, autoridad).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

        Assert.Equal(MotivoDeNoCreacion.CosturaMonetariaDefectuosa, r.Motivo);
        Assert.Null(r.OrderCode);
        await db.Database.CurrentTransaction!.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));
    }

    [Fact]
    public async Task Una_transaccion_prestada_declarada_repeatable_read_se_rechaza_sin_tomar_el_bloqueo()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = Pool(maximo: 3, espera: 5);
        await MonedaAcreditadaAsync(ct);

        await using var core = Core(cadena);
        await using var ambito = core.CreateAsyncScope();
        await using var db = Ventas(cadena);
        var (cliente, item) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var autoridad = new PrestaLaTransaccionTrasLaPrevia(
            Adaptador(ambito.ServiceProvider), db, cliente, item, aislamiento: IsolationLevel.RepeatableRead);

        var r = await Creador(db, cliente, item, autoridad).CrearAsync(cliente, [new LineaPedida(item, 1)], ct);

        Assert.Equal(MotivoDeNoCreacion.CosturaMonetariaDefectuosa, r.Motivo);
        Assert.Empty(await BloqueosAsync(ct));                               // ni siquiera se pidió
        Assert.NotNull(db.Database.CurrentTransaction);                      // sigue siendo del llamador
        await db.Database.CurrentTransaction!.RollbackAsync(ct);
    }

    [Fact]
    public async Task Sin_el_bloqueo_concedido_la_lectura_transaccional_real_es_uso_indebido_y_no_lee()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var core = Core(fixture.ConnectionString);
        await using var ambito = core.CreateAsyncScope();
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var lectura = await Adaptador(ambito.ServiceProvider).LeerEnTransaccionAsync(tx.GetDbTransaction(), ct);

        Assert.Equal(LecturaDeMonedaDeVenta.UsoIndebido, lectura);
        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Si_la_transaccion_ya_esta_abortada_la_lectura_real_no_esta_disponible_y_al_deshacer_se_suelta_el_bloqueo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var core = Core(fixture.ConnectionString);
        await using var ambito = core.CreateAsyncScope();
        await using var db = fixture.NuevoContexto();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await BarreraMonetariaDeVentas.AdquirirCompartidaAsync(db, ct);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("SELECT 1 / 0", ct));

        var lectura = await Adaptador(ambito.ServiceProvider).LeerEnTransaccionAsync(tx.GetDbTransaction(), ct);

        Assert.Equal(LecturaDeMonedaDeVenta.NoDisponible, lectura);
        await tx.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));
    }

    [Fact]
    public async Task La_lectura_transaccional_real_no_abre_otra_conexion_ni_toca_la_transaccion()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = Pool(maximo: 1, espera: 2);
        await using var core = Core(cadena);
        await using var ambito = core.CreateAsyncScope();
        await using var db = Ventas(cadena);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await BarreraMonetariaDeVentas.AdquirirCompartidaAsync(db, ct);

        var lectura = await Adaptador(ambito.ServiceProvider).LeerEnTransaccionAsync(tx.GetDbTransaction(), ct);

        // Con pool 1 ya ocupado, si hubiera pedido otra conexión sería NoDisponible por agotamiento.
        Assert.NotEqual(LecturaDeMonedaDeVenta.NoDisponible, lectura);
        Assert.NotEqual(LecturaDeMonedaDeVenta.UsoIndebido, lectura);
        Assert.NotNull(tx.GetDbTransaction().Connection);                    // sigue viva
        Assert.Equal(1, (await db.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").ToListAsync(ct)).Single());
        await tx.RollbackAsync(ct);
        Assert.Empty(await BloqueosAsync(ct));
    }

    // ================================================================== montaje

    /// <summary>
    /// La autoridad real, con una diferencia de montaje: tras la lectura previa abre la
    /// transacción «del llamador» en el contexto de Ventas y crea ahí cliente y artículo.
    /// En la lectura transaccional anota por qué proceso del servidor va y si tiene el
    /// compartido, consultándolo <b>en esa misma conexión</b>.
    /// </summary>
    private sealed class PrestaLaTransaccionTrasLaPrevia(
        IAutoridadDeMoneda real,
        SalesDbContext db,
        Guid cliente,
        Guid item,
        IsolationLevel aislamiento = IsolationLevel.ReadCommitted,
        string? primeraSentencia = null) : IAutoridadDeMoneda
    {
        public int? PidDeLaPrestada { get; private set; }

        public int? PidEnLaLectura { get; private set; }

        public bool CompartidoConcedidoEnLaLectura { get; private set; }

        public async Task<LecturaDeMonedaDeVenta> MonedaDeVentaAsync(CancellationToken cancellationToken)
        {
            var previa = await real.MonedaDeVentaAsync(cancellationToken);

            await db.Database.BeginTransactionAsync(
                primeraSentencia is null ? aislamiento : IsolationLevel.Unspecified, cancellationToken);
            if (primeraSentencia is not null)
            {
                await db.Database.ExecuteSqlRawAsync(primeraSentencia, cancellationToken);
            }

            PidDeLaPrestada = (await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").ToListAsync(cancellationToken)).Single();
            await PrepararAsync(db, cliente, item, cancellationToken);
            return previa;
        }

        public async Task<LecturaDeMonedaDeVenta> LeerEnTransaccionAsync(DbTransaction transaccion, CancellationToken cancellationToken)
        {
            await using (var comando = transaccion.Connection!.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText =
                    "SELECT pg_backend_pid(), EXISTS (" + BloqueosDeMoneda + " AND mode = 'ShareLock' AND granted AND pid = pg_backend_pid())";
                await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
                await lector.ReadAsync(cancellationToken);
                PidEnLaLectura = lector.GetInt32(0);
                CompartidoConcedidoEnLaLectura = lector.GetBoolean(1);
            }

            return await real.LeerEnTransaccionAsync(transaccion, cancellationToken);
        }
    }

    /// <summary>
    /// La cadena de la base de pruebas con un pool propio y acotado. El nombre de
    /// aplicación separa el pool de los demás y permite contarlo en <c>pg_stat_activity</c>.
    /// </summary>
    private string Pool(int maximo, int espera)
        => new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            MaxPoolSize = maximo,
            MinPoolSize = 0,
            Timeout = espera,
            ApplicationName = $"e13b-{Guid.NewGuid():N}"[..30],
        }.ConnectionString;

    /// <summary>CORE real, por su registro público, sobre la cadena dada.</summary>
    private static ServiceProvider Core(string cadena)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [NodeIdentity.SettingKey] = "principal" })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddCoreEssentials(configuracion, cadena);
        servicios.AddCoreAuthentication();
        return servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static AutoridadDeMonedaDeCore Adaptador(IServiceProvider ambito) => new(
        ambito.GetRequiredService<IInstallationCurrencyAuthority>(),
        ambito.GetRequiredService<IInstallationCurrencyTransactionalAuthority>(),
        NullLogger<AutoridadDeMonedaDeCore>.Instance);

    private static SalesDbContext Ventas(string cadena) => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(cadena, npgsql => npgsql.MigrationsHistoryTable(SalesDbContext.MigrationsHistoryTable, SalesDbContext.Schema))
            .Options,
        new NodeIdentity("principal"),
        new Dobles.RelojFijo(Ahora));

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

    /// <summary>La moneda que CORE da por acreditada en esta base, o un fallo que lo dice.</summary>
    private async Task<string> MonedaAcreditadaAsync(CancellationToken ct)
    {
        await using var core = Core(fixture.ConnectionString);
        await using var ambito = core.CreateAsyncScope();
        var lectura = await ambito.ServiceProvider.GetRequiredService<IInstallationCurrencyAuthority>().ReadAsync(ct);

        if (!lectura.IsAccredited || lectura.Currency is null)
        {
            Assert.Fail(
                $"Precondición no cumplida: CORE lee la moneda como {lectura.Status}. Acredítala antes con la " +
                "operación real (PUT /api/admin/settings/installation-currency como super_admin) en esta base.");
        }

        return lectura.Currency!.Code;
    }

    private static async Task PrepararAsync(SalesDbContext db, Guid cliente, Guid item, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO crm.customers (customer_id, full_name, email, origin_node) VALUES ({0}, {1}, {2}, 'principal')",
            [cliente, $"Cliente {cliente:N}", $"{cliente:N}@prueba.pe"], ct);

        var producto = Guid.CreateVersion7();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO catalog.products (id, name, slug, origin_node) VALUES ({0}, {1}, {2}, 'principal')",
            [producto, $"Producto de prueba {producto:N}", $"producto-{producto:N}"], ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO catalog.product_items (id, product_id, origin_node) VALUES ({0}, {1}, 'principal')",
            [item, producto], ct);
    }

    private async Task<int> ConexionesDelPoolAsync(string cadena, CancellationToken ct)
    {
        var nombre = new NpgsqlConnectionStringBuilder(cadena).ApplicationName;
        await using var conexion = new NpgsqlConnection(fixture.ConnectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand("SELECT count(*)::int FROM pg_stat_activity WHERE application_name = $1", conexion);
        comando.Parameters.Add(new NpgsqlParameter { Value = nombre });
        return (int)(await comando.ExecuteScalarAsync(ct))!;
    }

    private async Task<int> PedidosAsync(string codigo, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(fixture.ConnectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand("SELECT count(*)::int FROM sales.orders WHERE order_code = $1", conexion);
        comando.Parameters.Add(new NpgsqlParameter { Value = codigo });
        return (int)(await comando.ExecuteScalarAsync(ct))!;
    }

    private async Task<List<(int Pid, string Mode, bool Granted)>> BloqueosAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(fixture.ConnectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(BloqueosDeMoneda, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var bloqueos = new List<(int, string, bool)>();
        while (await lector.ReadAsync(ct))
        {
            bloqueos.Add((lector.GetInt32(0), lector.GetString(1), lector.GetBoolean(2)));
        }

        return bloqueos;
    }

    private static void InterlockedMax(ref int destino, int valor)
    {
        int actual;
        while (valor > (actual = Volatile.Read(ref destino))
               && Interlocked.CompareExchange(ref destino, valor, actual) != actual)
        {
        }
    }
}
