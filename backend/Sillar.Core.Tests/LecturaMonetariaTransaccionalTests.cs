using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Settings;
using Sillar.Shared.Replication;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// E13-A — la moneda acreditada se lee en la conexión y la transacción que ya tiene
/// abiertas Ventas, sin pedir otra conexión, con las mismas garantías que el lector de CORE.
/// </summary>
/// <remarks>
/// Sin PostgreSQL. La conexión y la transacción comerciales son dobles que cuentan cada
/// apertura, cierre, confirmación, reversión y comando. Que el bloqueo sea real, que
/// <c>pg_locks</c> lo muestre así y que el pool no se agote lo demuestra solo la prueba
/// física (<c>ENTREGA-07</c> §15).
/// </remarks>
public sealed class LecturaMonetariaTransaccionalTests
{
    private const string BaseDeCore = "sillar";
    private const string CadenaDeCore = "Host=127.0.0.1;Port=1;Database=sillar;Username=nadie;Password=nada;Timeout=2";

    private static readonly string ComprobanteDePen = InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "PEN");
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    // ── Misma conexión, misma transacción, ninguna otra ────────────────────

    [Fact]
    public async Task Lee_la_moneda_acreditada_en_la_conexion_y_la_transaccion_del_llamador()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);

        var lectura = await Leer(m);

        Assert.Equal(InstallationCurrencyStatus.Accredited, lectura.Status);
        Assert.Equal("PEN", lectura.Currency?.Code);
        Assert.Equal(2, lectura.Currency?.MinorUnits);
        Assert.Equal(Ahora, lectura.ObservedAt);
        Assert.Equal(2, m.Conexion.Ejecutados.Count);
        Assert.All(m.Conexion.Ejecutados, c =>
        {
            Assert.Same(m.Conexion, c.Conexion);
            Assert.Same(m.Transaccion, c.Transaccion);
        });
        NadaAjenoTocado(m);
    }

    [Fact]
    public async Task El_contexto_de_CORE_del_ambito_no_abre_ninguna_conexion()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);

        await Leer(m);

        Assert.Equal(0, m.AperturasDeCore.Aperturas);
        Assert.Equal(ConnectionState.Closed, m.ContextoDeCore.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Primero_comprueba_la_transaccion_y_despues_lee_dentro_de_ella()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);

        await Leer(m);

        Assert.Contains("pg_locks", m.Conexion.Ejecutados[0].Texto);
        Assert.Contains("FROM core.site_settings", m.Conexion.Ejecutados[1].Texto);
        Assert.Contains("core.audit_log", m.Conexion.Ejecutados[1].Texto);
    }

    [Fact]
    public async Task Un_bloqueo_exclusivo_de_la_misma_transaccion_tambien_vale()
    {
        // Lo comprueba la consulta (mode IN ('ShareLock', 'ExclusiveLock')); aquí solo
        // que la respuesta afirmativa basta, sea cual sea el modo.
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);

        Assert.True((await Leer(m)).IsAccredited);
        Assert.Contains("'ExclusiveLock'", InstallationCurrencyTransactionalAuthority.CheckSql().Format);
    }

    // ── Las garantías de la lectura acreditada ────────────────────────────

    public static TheoryData<string, string?, string?, InstallationCurrencyStatus> Estados => new()
    {
        { "acreditada", "PEN", ComprobanteDePen, InstallationCurrencyStatus.Accredited },
        { "sin fila", null, null, InstallationCurrencyStatus.NotPrepared },
        { "pendiente", "PENDIENTE_DEFINIR", null, InstallationCurrencyStatus.NotEstablished },
        { "vacía", "", null, InstallationCurrencyStatus.NotEstablished },
        { "no soportada", "USD", InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "USD"), InstallationCurrencyStatus.Unsupported },
        { "minúsculas", "pen", ComprobanteDePen, InstallationCurrencyStatus.Unsupported },
        { "sin comprobante", "PEN", null, InstallationCurrencyStatus.NotAttested },
        { "comprobante de otra moneda", "PEN", InstallationCurrencyAuditText.Compose("PEN", "USD"), InstallationCurrencyStatus.NotAttested },
        { "comprobante alterado", "PEN", "Moneda de la instalación: anterior=pen; nueva=PEN; lista=sillar-iso4217-v1.", InstallationCurrencyStatus.NotAttested },
        { "comprobante con texto de más", "PEN", ComprobanteDePen + " ", InstallationCurrencyStatus.NotAttested },
    };

    [Theory]
    [MemberData(nameof(Estados))]
    public async Task Solo_la_moneda_acreditada_tiene_moneda_y_el_resto_responde_su_estado(
        string caso, string? valor, string? comprobante, InstallationCurrencyStatus esperado)
    {
        var lectura = await Leer(Montar(valor, comprobante));

        Assert.True(lectura.Status == esperado, $"{caso}: {lectura.Status}");
        Assert.Equal(esperado == InstallationCurrencyStatus.Accredited, lectura.Currency is not null);
        Assert.Equal(SupportedCurrencies.Version, lectura.ListVersion);
    }

    [Theory]
    [MemberData(nameof(Estados))]
    public async Task Responde_lo_mismo_que_el_lector_de_CORE_con_los_mismos_datos(
        string caso, string? valor, string? comprobante, InstallationCurrencyStatus esperado)
    {
        _ = esperado;
        var transaccional = await Leer(Montar(valor, comprobante));

        var base_ = new BaseGuionada { FilasAlLeer = sql => sql.Contains("FROM core.site_settings") ? Hechos(valor, comprobante) : null };
        await using var contexto = base_.Contexto();
        var externa = await new InstallationCurrencyAuthority(contexto, new RelojFijo(Ahora), NullLogger<InstallationCurrencyAuthority>.Instance)
            .ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(transaccional == externa, $"{caso}: {transaccional} ≠ {externa}");
    }

    [Fact]
    public async Task La_lectura_es_la_misma_sentencia_que_la_del_lector_de_CORE()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);
        await Leer(m);

        var base_ = new BaseGuionada { FilasAlLeer = _ => null };
        await using var contexto = base_.Contexto();
        await new InstallationCurrencyAuthority(contexto, TimeProvider.System, NullLogger<InstallationCurrencyAuthority>.Instance)
            .ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Assert.Single(base_.Pasos), m.Conexion.Ejecutados[1].Texto.Trim());
        Assert.DoesNotContain("currency_code", m.Conexion.Ejecutados[1].Texto);
    }

    // ── Ni escrituras, ni auditoría, ni bloqueos ──────────────────────────

    [Fact]
    public async Task No_escribe_no_audita_y_no_toma_ningun_bloqueo()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);

        await Leer(m);

        Assert.All(m.Conexion.Ejecutados, c =>
        {
            Assert.StartsWith("SELECT", c.Texto.TrimStart());
            // Palabras clave en mayúsculas, como las escribe EF: 'update' en minúsculas es
            // el literal de la acción de auditoría que se busca, no una sentencia.
            foreach (var prohibido in new[] { "INSERT ", "UPDATE ", "DELETE ", "FOR UPDATE", "FOR SHARE", "LOCK TABLE", "pg_advisory", "set_config" })
            {
                Assert.DoesNotContain(prohibido, c.Texto, StringComparison.Ordinal);
            }
        });
    }

    [Fact]
    public void La_comprobacion_mira_el_bloqueo_del_contrato_en_su_forma_de_dos_claves()
    {
        var sql = InstallationCurrencyTransactionalAuthority.CheckSql();

        Assert.Contains("pg_catalog.pg_locks", sql.Format);
        Assert.Contains("pg_backend_pid()", sql.Format);
        Assert.Contains("l.granted", sql.Format);
        Assert.Contains("l.objsubid = 2", sql.Format);
        Assert.Contains("current_setting('transaction_isolation')", sql.Format);
        Assert.Equal(new object[] { MonetaryAuthorityLock.Namespace, MonetaryAuthorityLock.CurrencyAuthority }, sql.GetArguments());
        Assert.DoesNotContain("pg_advisory", sql.Format);
    }

    [Fact]
    public void El_codigo_no_abre_ni_cierra_ni_confirma_ni_deshace_nada()
    {
        var fuente = File.ReadAllText(Path.Combine(
            RaizDelRepositorio(), "backend", "Sillar.Core", "Settings", "InstallationCurrencyTransactionalAuthority.cs"));
        var codigo = string.Join('\n', fuente.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));

        foreach (var prohibido in new[]
                 {
                     "BeginTransaction", ".Commit", ".Rollback", ".Open(", "OpenAsync", ".Close(", "CloseAsync",
                     "SaveChanges", "new NpgsqlConnection(", "NpgsqlDataSource", "pg_advisory", "FOR UPDATE",
                     "FOR SHARE", "currency_code", "SettingsCache", "ISettingsReader", "ExecuteSql",
                 })
        {
            Assert.DoesNotContain(prohibido, codigo.Replace("<c>Commit</c>", "").Replace("<c>Rollback</c>", ""), StringComparison.Ordinal);
        }
    }

    // ── Rechazo de usos incompatibles: error de uso, sin leer ─────────────

    public static TheoryData<string, string, string, bool> Incompatibles => new()
    {
        { "aislamiento repeatable read", "repeatable read", BaseDeCore, true },
        { "aislamiento serializable", "serializable", BaseDeCore, true },
        { "aislamiento read uncommitted", "read uncommitted", BaseDeCore, true },
        { "sin el bloqueo de moneda", "read committed", BaseDeCore, false },
        { "en otra base", "read committed", "otra", true },
        { "base que solo difiere en mayúsculas", "read committed", "SILLAR", true },
    };

    [Theory]
    [MemberData(nameof(Incompatibles))]
    public async Task Una_transaccion_incompatible_se_rechaza_sin_leer_la_moneda(string caso, string aislamiento, string base_, bool bloqueo)
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen, aislamiento: aislamiento, baseActual: base_, bloqueo: bloqueo);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Leer(m));

        Assert.False(string.IsNullOrWhiteSpace(error.Message), caso);
        Assert.Single(m.Conexion.Ejecutados);
        Assert.DoesNotContain(m.Conexion.Ejecutados, c => c.Texto.Contains("site_settings"));
        NadaAjenoTocado(m);
    }

    [Fact]
    public async Task Una_conexion_cerrada_se_rechaza_sin_abrirla()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);
        m.Conexion.Estado = ConnectionState.Closed;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Leer(m));

        Assert.Empty(m.Conexion.Ejecutados);
        Assert.Equal(0, m.Conexion.Aperturas);
        Assert.Equal(ConnectionState.Closed, m.Conexion.State);
    }

    [Fact]
    public async Task Una_transaccion_ya_terminada_se_rechaza()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);
        m.Transaccion.Terminada = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Leer(m));

        Assert.Empty(m.Conexion.Ejecutados);
        NadaAjenoTocado(m);
    }

    [Fact]
    public async Task Sin_transaccion_no_hay_lectura()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => m.Lector.ReadInTransactionAsync(null!, TestContext.Current.CancellationToken));

        Assert.Empty(m.Conexion.Ejecutados);
    }

    [Fact]
    public async Task Sin_saber_cual_es_la_base_de_CORE_se_rechaza()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen, cadenaDeCore: "Host=127.0.0.1;Port=1;Username=nadie;Password=nada");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Leer(m));

        Assert.DoesNotContain(m.Conexion.Ejecutados, c => c.Texto.Contains("site_settings"));
    }

    // ── Cancelación y errores: se propagan o se rechazan, nada queda abierto ─

    [Fact]
    public async Task Cancelado_antes_de_empezar_no_ejecuta_nada()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);
        using var origen = new CancellationTokenSource();
        await origen.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => m.Lector.ReadInTransactionAsync(m.Transaccion, origen.Token));

        Assert.Empty(m.Conexion.Ejecutados);
        NadaAjenoTocado(m);
    }

    [Theory]
    [InlineData("pg_locks")]
    [InlineData("FROM core.site_settings")]
    public async Task Cancelado_a_mitad_se_propaga_sin_tocar_la_transaccion_ajena(string sentencia)
    {
        using var origen = new CancellationTokenSource();
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);
        m.Conexion.FallaAlEjecutar = sql =>
        {
            if (!sql.Contains(sentencia))
            {
                return null;
            }

            origen.Cancel();
            return new OperationCanceledException(origen.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => m.Lector.ReadInTransactionAsync(m.Transaccion, origen.Token));

        NadaAjenoTocado(m);
    }

    [Theory]
    [InlineData("pg_locks")]
    [InlineData("FROM core.site_settings")]
    public async Task Si_una_lectura_falla_la_moneda_no_esta_disponible_y_la_transaccion_queda_para_su_dueno(string sentencia)
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);
        m.Conexion.FallaAlEjecutar = sql => sql.Contains(sentencia)
            ? new PostgresException("current transaction is aborted", "ERROR", "ERROR", PostgresErrorCodes.InFailedSqlTransaction)
            : null;

        var lectura = await Leer(m);

        Assert.Equal(InstallationCurrencyStatus.Unavailable, lectura.Status);
        Assert.Null(lectura.Currency);
        NadaAjenoTocado(m);
    }

    [Fact]
    public async Task Leer_varias_veces_en_la_misma_transaccion_no_acumula_nada()
    {
        var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);

        for (var vez = 0; vez < 3; vez++)
        {
            Assert.True((await Leer(m)).IsAccredited);
        }

        Assert.Equal(6, m.Conexion.Ejecutados.Count);
        NadaAjenoTocado(m);
    }

    [Fact]
    public async Task Un_contexto_por_lectura_sobre_conexiones_distintas_no_multiplica_los_proveedores_de_EF()
    {
        // EF lanza ManyServiceProvidersCreatedWarning a partir de veinte proveedores
        // internos. La conexión no forma parte de su clave: treinta pedidos, treinta
        // conexiones, un proveedor.
        for (var pedido = 0; pedido < 30; pedido++)
        {
            var m = Montar(valor: "PEN", comprobante: ComprobanteDePen);
            Assert.True((await Leer(m)).IsAccredited);
            NadaAjenoTocado(m);
        }

        // Y directamente: la caché interna de EF, única por proveedor, es la misma.
        var caches = new HashSet<object>(ReferenceEqualityComparer.Instance);
        for (var pedido = 0; pedido < 30; pedido++)
        {
            await using var contexto = new CoreDbContext(CoreDataServiceExtensions.BuildOptions(new ConexionComercialFalsa()), Nodo, TimeProvider.System);
            caches.Add(contexto.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());
        }

        Assert.Single(caches);
    }

    // ── Registro ───────────────────────────────────────────────────────────

    [Fact]
    public void Se_registra_por_peticion_junto_al_lector_de_CORE()
    {
        var servicios = new ServiceCollection().AddInstallationCurrency();

        var registro = Assert.Single(servicios, s => s.ServiceType == typeof(IInstallationCurrencyTransactionalAuthority));
        Assert.Equal(ServiceLifetime.Scoped, registro.Lifetime);
        Assert.Equal(typeof(InstallationCurrencyTransactionalAuthority), registro.ImplementationType);
        Assert.Single(servicios, s => s.ServiceType == typeof(IInstallationCurrencyAuthority));
    }

    [Fact]
    public async Task Se_resuelve_desde_el_contenedor_de_CORE_sin_abrir_su_conexion()
    {
        var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Nodo)
            .AddSingleton(TimeProvider.System)
            .AddCoreData(CadenaDeCore)
            .AddInstallationCurrency();
        await using var proveedor = servicios.BuildServiceProvider();
        await using var ambito = proveedor.CreateAsyncScope();

        var lector = ambito.ServiceProvider.GetRequiredService<IInstallationCurrencyTransactionalAuthority>();

        Assert.IsType<InstallationCurrencyTransactionalAuthority>(lector);
        Assert.Equal(ConnectionState.Closed, ambito.ServiceProvider.GetRequiredService<CoreDbContext>().Database.GetDbConnection().State);
    }

    // ── Montaje ────────────────────────────────────────────────────────────

    private sealed record Montaje(
        InstallationCurrencyTransactionalAuthority Lector,
        ConexionComercialFalsa Conexion,
        TransaccionComercialFalsa Transaccion,
        CoreDbContext ContextoDeCore,
        ContadorDeAperturas AperturasDeCore);

    private static Montaje Montar(
        string? valor,
        string? comprobante,
        string aislamiento = InstallationCurrencyTransactionalAuthority.RequiredIsolation,
        string baseActual = BaseDeCore,
        bool bloqueo = true,
        string cadenaDeCore = CadenaDeCore)
    {
        var conexion = new ConexionComercialFalsa
        {
            Responder = sql =>
                sql.Contains("pg_locks") ? Comprobacion(aislamiento, baseActual, bloqueo)
                : sql.Contains("FROM core.site_settings") ? Hechos(valor, comprobante)
                : null,
        };
        var transaccion = new TransaccionComercialFalsa(conexion);
        conexion.TransaccionEsperada = transaccion;

        var contador = new ContadorDeAperturas();
        var contextoDeCore = new CoreDbContext(
            new DbContextOptionsBuilder<CoreDbContext>().UseNpgsql(cadenaDeCore).AddInterceptors(contador).Options,
            Nodo,
            TimeProvider.System);

        var lector = new InstallationCurrencyTransactionalAuthority(
            contextoDeCore, Nodo, new RelojFijo(Ahora), NullLogger<InstallationCurrencyTransactionalAuthority>.Instance);
        return new Montaje(lector, conexion, transaccion, contextoDeCore, contador);
    }

    private static Task<InstallationCurrencyReading> Leer(Montaje m)
        => m.Lector.ReadInTransactionAsync(m.Transaccion, TestContext.Current.CancellationToken);

    /// <summary>Lo que no es de CORE sigue exactamente como estaba.</summary>
    private static void NadaAjenoTocado(Montaje m)
    {
        Assert.Equal(0, m.Conexion.Aperturas);
        Assert.Equal(0, m.Conexion.Cierres);
        Assert.Equal(0, m.Conexion.Desechos);
        Assert.Equal(0, m.Conexion.TransaccionesPedidas);
        Assert.Equal(ConnectionState.Open, m.Conexion.State);
        Assert.Equal(0, m.Transaccion.Confirmaciones);
        Assert.Equal(0, m.Transaccion.Reversiones);
        Assert.Equal(0, m.Transaccion.Desechos);
        Assert.Equal(0, m.AperturasDeCore.Aperturas);
    }

    private static DataTable Comprobacion(string aislamiento, string base_, bool bloqueo)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Isolation", typeof(string));
        tabla.Columns.Add("Database", typeof(string));
        tabla.Columns.Add("LockHeld", typeof(bool));
        tabla.Rows.Add(aislamiento, base_, bloqueo);
        return tabla;
    }

    /// <summary>La fila de hechos: valor guardado y último comprobante, en el orden del SELECT.</summary>
    private static DataTable Hechos(string? valor, string? comprobante)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("setting_value", typeof(string));
        tabla.Columns.Add("summary", typeof(string));
        if (valor is not null)
        {
            tabla.Rows.Add(valor, (object?)comprobante ?? DBNull.Value);
        }

        return tabla;
    }

    private static string RaizDelRepositorio()
    {
        for (var directorio = new DirectoryInfo(AppContext.BaseDirectory); directorio is not null; directorio = directorio.Parent)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, "database", "modules", "core")))
            {
                return directorio.FullName;
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    /// <summary>Cuenta las aperturas de la conexión propia de CORE: tienen que ser cero.</summary>
    private sealed class ContadorDeAperturas : DbConnectionInterceptor
    {
        public int Aperturas { get; private set; }

        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            Aperturas++;
            return result;
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Aperturas++;
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>La conexión de Ventas, ya abierta y con el bloqueo puesto. Anota todo lo que se le hace.</summary>
    private sealed class ConexionComercialFalsa : DbConnection
    {
        public ConnectionState Estado { get; set; } = ConnectionState.Open;

        public Func<string, DataTable?> Responder { get; init; } = _ => null;

        public Func<string, Exception?> FallaAlEjecutar { get; set; } = _ => null;

        /// <summary>La transacción comercial: un comando sin ella no se ejecuta.</summary>
        public DbTransaction? TransaccionEsperada { get; set; }

        public List<(string Texto, DbConnection? Conexion, DbTransaction? Transaccion)> Ejecutados { get; } = [];

        public int Aperturas { get; private set; }

        public int Cierres { get; private set; }

        public int Desechos { get; private set; }

        public int TransaccionesPedidas { get; private set; }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = "";

        public override string Database => BaseDeCore;

        public override string DataSource => "ventas";

        public override string ServerVersion => "16.0";

        public override ConnectionState State => Estado;

        public override void ChangeDatabase(string databaseName) => throw new InvalidOperationException("CORE no cambia la base de otro.");

        public override void Open() => Aperturas++;

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            Aperturas++;
            return Task.CompletedTask;
        }

        public override void Close() => Cierres++;

        protected override void Dispose(bool disposing)
        {
            Desechos++;
            base.Dispose(disposing);
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            TransaccionesPedidas++;
            throw new InvalidOperationException("CORE no abre transacciones en la conexión de otro.");
        }

        protected override DbCommand CreateDbCommand() => new ComandoFalso(this);

        private sealed class ComandoFalso(ConexionComercialFalsa conexion) : DbCommand
        {
            private readonly NpgsqlCommand parametros = new();

            [System.Diagnostics.CodeAnalysis.AllowNull]
            public override string CommandText { get; set; } = "";

            public override int CommandTimeout { get; set; }

            public override CommandType CommandType { get; set; }

            public override bool DesignTimeVisible { get; set; }

            public override UpdateRowSource UpdatedRowSource { get; set; }

            // Como cualquier proveedor: el comando nace atado a la conexión que lo creó.
            protected override DbConnection? DbConnection { get; set; } = conexion;

            protected override DbParameterCollection DbParameterCollection => parametros.Parameters;

            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel()
            {
            }

            public override void Prepare()
            {
            }

            protected override DbParameter CreateDbParameter() => new NpgsqlParameter();

            public override int ExecuteNonQuery()
            {
                Anotar();
                return -1;
            }

            public override object? ExecuteScalar()
            {
                Anotar();
                return null;
            }

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                Anotar();
                return (conexion.Responder(CommandText) ?? new DataTable()).CreateDataReader();
            }

            private void Anotar()
            {
                conexion.Ejecutados.Add((CommandText, DbConnection, DbTransaction));
                if (!ReferenceEquals(DbTransaction, conexion.TransaccionEsperada))
                {
                    throw new InvalidOperationException("Comando fuera de la transacción comercial.");
                }

                if (conexion.FallaAlEjecutar(CommandText) is { } excepcion)
                {
                    throw excepcion;
                }
            }
        }
    }

    /// <summary>La transacción de Ventas. Nadie más que su dueño la confirma, la deshace o la desecha.</summary>
    private sealed class TransaccionComercialFalsa(ConexionComercialFalsa conexion) : DbTransaction
    {
        public bool Terminada { get; set; }

        public int Confirmaciones { get; private set; }

        public int Reversiones { get; private set; }

        public int Desechos { get; private set; }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection? DbConnection => Terminada ? null : conexion;

        public override void Commit() => Confirmaciones++;

        public override void Rollback() => Reversiones++;

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Confirmaciones++;
            return Task.CompletedTask;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Reversiones++;
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            Desechos++;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Desechos++;
            return ValueTask.CompletedTask;
        }
    }
}
