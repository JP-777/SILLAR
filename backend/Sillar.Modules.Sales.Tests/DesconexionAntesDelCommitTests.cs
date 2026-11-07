using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Endpoints;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// <b>R10-E13-G, D-3: deshacer una transacción cuya conexión ya se rompió no sustituye la causa.</b>
/// </summary>
/// <remarks>
/// <para>
/// En C7 (R10-E13-F) el servidor cerró la sesión del pedido antes del <c>COMMIT</c>. Ventas
/// reconoció el <c>57P01</c> y decidió <see cref="MotivoDeNoCreacion.ServicioNoDisponible"/>,
/// pero al salir <c>TransaccionDeOperacion.DisposeAsync</c> intentó deshacer: Npgsql ya
/// había desechado la transacción y lanzó <see cref="ObjectDisposedException"/>, que salió
/// como un 500.
/// </para>
/// <para>
/// <b>Lo que faltaba en las pruebas anteriores</b> era la transacción desechada. Aquí la
/// base guionada la reproduce con <see cref="BaseGuionadaDeVentas.Romper"/>: la conexión de
/// la transacción pasa a ser nula y deshacerla lanza <see cref="ObjectDisposedException"/>
/// con el motivo dentro, como <c>NpgsqlTransaction.CheckDisposed</c> en la traza de C7.
/// </para>
/// <para>
/// <b>Sin PostgreSQL.</b> Estas pruebas no acreditan que el servidor deshaga la transacción
/// ni que suelte el bloqueo al cerrar la sesión: eso lo acreditó C7 físicamente (sin pedido,
/// sin número, sin advisory) y lo repetirá QA sobre esta candidata.
/// </para>
/// </remarks>
public sealed class DesconexionAntesDelCommitTests
{
    private const string Bloqueo = "pg_advisory_xact_lock_shared(";
    private const string Serie = "\"sales\".\"order_series\"";
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Item = Guid.CreateVersion7();

    // ============================================================ T1, T2, T7 · corte antes del COMMIT

    [Theory]
    [InlineData("bloqueo")]
    [InlineData("moneda")]
    [InlineData("numero")]
    public async Task Un_corte_57P01_antes_del_COMMIT_es_servicio_no_disponible_aunque_la_transaccion_ya_este_desechada(string donde)
    {
        var corte = Terminada();
        var m = Montar(
            falla: (b, sql) => (donde, sql) switch
            {
                ("bloqueo", _) when sql.Contains(Bloqueo, StringComparison.Ordinal) => Cortar(b, corte),
                ("numero", _) when sql.Contains(Serie, StringComparison.Ordinal) => Cortar(b, corte),
                ("insercion", _) when EsInsercionDelPedido(sql) => Cortar(b, corte),
                _ => null,
            },
            enTransaccion: donde == "moneda" ? b => throw Cortar(b, corte) : null);

        var r = await m.CrearAsync();

        // T2: el motivo decidido se conserva, y su respuesta es 503.
        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.Null(r.OrderCode);
        Assert.Equal(503, Assert.IsType<ProblemHttpResult>(PedidosDelClienteEndpoints.RespuestaDeRechazo(r.Motivo)).StatusCode);

        // T1: la limpieza sí encontró la transacción desechada, con el corte como motivo.
        var aviso = Assert.Single(m.Registro.Entradas, e => e.Evento == TransaccionDeOperacion.RollbackTrasConexionRota);
        Assert.Equal(LogLevel.Warning, aviso.Nivel);
        var secundaria = Assert.IsType<ObjectDisposedException>(aviso.Excepcion);
        Assert.Same(corte, secundaria.InnerException);

        // T7: nada confirmado, y nada que deshacer en el cliente: la sesión ya no existe.
        var pasos = m.Base.Pasos;
        Assert.Contains(BaseGuionadaDeVentas.ConexionRota, pasos);
        Assert.DoesNotContain("COMMIT", pasos);
        Assert.Equal(BaseGuionadaDeVentas.ConexionRota, pasos[^1]);
        Assert.Null(Assert.Single(m.Base.Transacciones).Connection);
        if (donde is "bloqueo" or "moneda")
        {
            Assert.DoesNotContain(pasos, p => p.Contains(Serie, StringComparison.Ordinal));
        }

        if (donde != "insercion")
        {
            Assert.DoesNotContain(pasos, EsInsercionDelPedido);
        }
    }

    [Fact]
    public async Task El_corte_original_queda_en_el_registro_ademas_del_rollback_imposible()
    {
        var corte = Terminada();
        var m = Montar(falla: (b, sql) => sql.Contains(Serie, StringComparison.Ordinal) ? Cortar(b, corte) : null);

        await m.CrearAsync();

        // La causa: la registra el creador al decidir «servicio no disponible».
        Assert.Contains(m.Registro.Entradas, e => e.Evento == AutoridadDeMonedaDeCore.MonedaIlegible && ReferenceEquals(e.Excepcion, corte));
        // La consecuencia: la registra la limpieza, aparte, sin sustituir a la causa.
        Assert.Contains(m.Registro.Entradas, e => e.Evento == TransaccionDeOperacion.RollbackTrasConexionRota);
        Assert.DoesNotContain(m.Registro.Entradas, e => e.Mensaje.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    // ============================================================ T3 · rollback normal

    [Fact]
    public async Task Con_la_conexion_viva_un_rechazo_bajo_el_bloqueo_se_deshace_normalmente()
    {
        var m = Montar(enTransaccion: _ => Con(InstallationCurrencyStatus.NotEstablished));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.MonedaSinAutoridad, r.Motivo);
        Assert.Equal("ROLLBACK", m.Base.Pasos[^1]);
        Assert.DoesNotContain(m.Registro.Entradas, e => e.Evento == TransaccionDeOperacion.RollbackTrasConexionRota);
    }

    // ============================================================ T5, T6 · lo que no se silencia

    [Fact]
    public async Task Un_fallo_genuino_al_deshacer_sin_corte_previo_se_propaga()
    {
        var genuino = new PostgresException("simulado", "ERROR", "ERROR", "XX000");
        var m = Montar(enTransaccion: _ => Con(InstallationCurrencyStatus.NotEstablished), alDeshacer: () => genuino);

        var error = await Assert.ThrowsAsync<PostgresException>(m.CrearAsync);

        Assert.Same(genuino, error);
        Assert.DoesNotContain(m.Registro.Entradas, e => e.Evento == TransaccionDeOperacion.RollbackTrasConexionRota);
    }

    [Theory]
    [InlineData("sin motivo")]
    [InlineData("motivo que no rompe la conexión")]
    [InlineData("motivo ajeno a Npgsql")]
    [InlineData("otra excepción")]
    public async Task Tras_un_corte_una_excepcion_de_limpieza_no_reconocida_no_se_oculta(string cual)
    {
        // El corte original es transitorio y el creador decide «servicio no disponible»,
        // pero lo que lanza la limpieza no acredita una conexión rota: se propaga.
        Exception secundaria = cual switch
        {
            "sin motivo" => new ObjectDisposedException("NpgsqlTransaction"),
            "motivo que no rompe la conexión" => new ObjectDisposedException("NpgsqlTransaction", new PostgresException("simulado", "ERROR", "ERROR", "23505")),
            "motivo ajeno a Npgsql" => new ObjectDisposedException("NpgsqlTransaction", new IOException("sin Npgsql")),
            _ => new InvalidOperationException("This NpgsqlTransaction has completed; it is no longer usable."),
        };
        var m = Montar(
            falla: (_, sql) => sql.Contains(Serie, StringComparison.Ordinal)
                ? new NpgsqlException("Exception while reading from stream", new IOException())
                : null,
            alDeshacer: () => secundaria);

        var error = await Assert.ThrowsAnyAsync<Exception>(m.CrearAsync);

        Assert.Same(secundaria, error);
        Assert.DoesNotContain(m.Registro.Entradas, e => e.Evento == TransaccionDeOperacion.RollbackTrasConexionRota);
    }

    // ============================================================ T8 · COMMIT incierto

    [Fact]
    public async Task Un_corte_en_el_COMMIT_sigue_saliendo_como_su_propia_excepcion_sin_reintento_ni_503()
    {
        var corte = Terminada();
        var m = Montar(guardadoSuprimido: true, alConfirmar: b => Cortar(b, corte));

        var error = await Assert.ThrowsAsync<PostgresException>(m.CrearAsync);

        // La causa del COMMIT, no la de la limpieza: antes de E13-G salía el ObjectDisposedException.
        Assert.Same(corte, error);
        var pasos = m.Base.Pasos;
        Assert.Single(pasos, p => p == "BEGIN");
        Assert.Single(pasos, p => p.Contains(Serie, StringComparison.Ordinal));
        Assert.Single(pasos, p => p.StartsWith("GUARDADO SUPRIMIDO", StringComparison.Ordinal));
        Assert.DoesNotContain("COMMIT", pasos);
        // No se tradujo a «servicio no disponible»: nadie invita a reintentar.
        Assert.DoesNotContain(m.Registro.Entradas, e => e.Evento == AutoridadDeMonedaDeCore.MonedaIlegible);
    }

    [Fact]
    public async Task Sin_fallos_el_pedido_se_confirma_como_antes()
    {
        var m = Montar(guardadoSuprimido: true);

        var r = await m.CrearAsync();

        Assert.True(r.Creado);
        Assert.Equal("P-2026-0007", r.OrderCode);
        Assert.Equal("COMMIT", m.Base.Pasos[^1]);
        Assert.DoesNotContain("ROLLBACK", m.Base.Pasos);
    }

    // ============================================================ TransaccionDeOperacion, sola

    [Fact]
    public async Task Sin_completar_y_con_la_conexion_viva_deshace()
    {
        var (base_, contexto, _) = Base();
        var t = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(contexto, IsolationLevel.ReadCommitted, null, Cancelacion);

        await t.DisposeAsync();

        Assert.Equal(["BEGIN", "ROLLBACK"], base_.Pasos);
        Assert.Null(t.RollbackImposible);
    }

    [Fact]
    public async Task Con_la_conexion_rota_no_lanza_lo_registra_y_conserva_el_motivo()
    {
        var registro = new Registro();
        var (base_, contexto, _) = Base();
        var t = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(contexto, IsolationLevel.ReadCommitted, registro.Para<CreadorDePedidos>(), Cancelacion);
        var corte = Terminada();
        base_.Romper(corte);

        await t.DisposeAsync();

        Assert.Same(corte, t.RollbackImposible!.InnerException);
        Assert.Equal(["BEGIN", BaseGuionadaDeVentas.ConexionRota], base_.Pasos);
        var aviso = Assert.Single(registro.Entradas);
        Assert.Equal((LogLevel.Warning, TransaccionDeOperacion.RollbackTrasConexionRota), (aviso.Nivel, aviso.Evento));
        Assert.Contains("57P01", aviso.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_registro_tambien_conserva_el_motivo()
    {
        // VencimientoDePlazos usa la sobrecarga sin registro: la tolerancia es la misma, y el
        // fallo original sale por su propio camino.
        var (base_, contexto, _) = Base();
        var t = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(contexto, Cancelacion);
        base_.Romper(new NpgsqlException("Exception while reading from stream", new IOException()));

        await t.DisposeAsync();

        Assert.NotNull(t.RollbackImposible);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Un_fallo_al_deshacer_que_no_acredita_una_conexion_rota_se_propaga(int caso)
    {
        Exception fallo = caso switch
        {
            0 => new PostgresException("simulado", "ERROR", "ERROR", "XX000"),
            1 => new ObjectDisposedException("NpgsqlTransaction"),
            2 => new ObjectDisposedException("NpgsqlTransaction", new PostgresException("simulado", "ERROR", "ERROR", "23505")),
            3 => new ObjectDisposedException("NpgsqlTransaction", new IOException("sin Npgsql")),
            4 => new InvalidOperationException("transacción ya terminada"),
            _ => new NpgsqlException("no transitorio"),
        };
        var (base_, contexto, _) = Base();
        base_.FallaAlDeshacer = () => fallo;
        var t = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(contexto, IsolationLevel.ReadCommitted, null, Cancelacion);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => t.DisposeAsync().AsTask());

        Assert.Same(fallo, error);
        Assert.Null(t.RollbackImposible);
    }

    [Fact]
    public async Task Completada_no_deshace()
    {
        var (base_, contexto, _) = Base();
        var t = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(contexto, IsolationLevel.ReadCommitted, null, Cancelacion);

        await t.CompletarAsync(Cancelacion);
        await t.DisposeAsync();

        Assert.Equal(["BEGIN", "COMMIT"], base_.Pasos);
    }

    [Fact]
    public async Task La_del_llamador_no_se_toca_ni_rota()
    {
        var (base_, contexto, _) = Base();
        await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, Cancelacion);
        base_.FallaAlDeshacer = () => new InvalidOperationException("no debía deshacerse");
        var t = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(contexto, IsolationLevel.ReadCommitted, null, Cancelacion);
        base_.Romper(Terminada());

        await t.DisposeAsync();

        Assert.Equal(["BEGIN", BaseGuionadaDeVentas.ConexionRota], base_.Pasos);
        Assert.Null(t.RollbackImposible);
    }

    [Fact]
    public async Task Un_COMMIT_cortado_sale_con_su_excepcion_y_no_con_la_de_la_limpieza()
    {
        var (base_, contexto, _) = Base();
        var corte = Terminada();
        base_.FallaAlConfirmar = () => Cortar(base_, corte);

        async Task Operacion()
        {
            await using var t = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(contexto, IsolationLevel.ReadCommitted, null, Cancelacion);
            await t.CompletarAsync(Cancelacion);
        }

        var error = await Assert.ThrowsAnyAsync<Exception>(Operacion);

        Assert.Same(corte, error);
    }

    // ============================================================ clasificador

    [Theory]
    [InlineData("57P01", "FATAL", true)]
    [InlineData("57P02", "FATAL", true)]
    [InlineData("57P03", "FATAL", true)]
    [InlineData("08006", "ERROR", true)]
    [InlineData("08P01", "ERROR", true)]
    [InlineData("XX000", "PANIC", true)]
    [InlineData("55P03", "ERROR", false)]
    [InlineData("40001", "ERROR", false)]
    [InlineData("23505", "ERROR", false)]
    [InlineData("XX000", "ERROR", false)]
    public void Una_conexion_rota_se_reconoce_por_su_codigo_o_su_gravedad(string codigo, string gravedad, bool rota)
        => Assert.Equal(rota, FallosDeInfraestructura.EsConexionRota(new PostgresException("simulado", gravedad, gravedad, codigo)));

    [Fact]
    public void Una_conexion_rota_del_lado_del_cliente_o_envuelta_tambien_se_reconoce()
    {
        Assert.True(FallosDeInfraestructura.EsConexionRota(new NpgsqlException("Exception while reading from stream", new IOException())));
        Assert.True(FallosDeInfraestructura.EsConexionRota(new NpgsqlException("Timeout", new TimeoutException())));
        Assert.True(FallosDeInfraestructura.EsConexionRota(new DbUpdateException("guardando", Terminada())));
        Assert.True(FallosDeInfraestructura.EsConexionRota(new InvalidOperationException("transient", Terminada())));

        Assert.False(FallosDeInfraestructura.EsConexionRota(null));
        Assert.False(FallosDeInfraestructura.EsConexionRota(new IOException("sin Npgsql")));
        Assert.False(FallosDeInfraestructura.EsConexionRota(new NpgsqlException("no transitorio")));

        // ObjectDisposedException deriva de InvalidOperationException y se desenvuelve: un
        // desechado cuyo motivo es una conexión rota acredita una conexión rota. Sin motivo, no.
        Assert.True(FallosDeInfraestructura.EsConexionRota(new ObjectDisposedException("NpgsqlTransaction", Terminada())));
        Assert.False(FallosDeInfraestructura.EsConexionRota(new ObjectDisposedException("NpgsqlTransaction")));
    }

    // ============================================================ guarda estructural

    [Fact]
    public void La_limpieza_solo_tolera_la_transaccion_desechada_por_una_conexion_rota()
    {
        var codigo = SoloCodigo("Sillar.Modules.Sales/Data/TransaccionDeOperacion.cs");

        var capturas = Regex.Matches(codigo, @"catch\b[^\n]*").Select(c => c.Value.Trim()).ToList();
        Assert.Equal(
            ["catch (ObjectDisposedException secundaria) when (FallosDeInfraestructura.EsConexionRota(secundaria.InnerException))"],
            capturas);

        // El COMMIT no se toca: CompletarAsync sigue sin capturar nada.
        var completar = codigo.IndexOf("public async Task CompletarAsync", StringComparison.Ordinal);
        var desechar = codigo.IndexOf("public async ValueTask DisposeAsync", StringComparison.Ordinal);
        Assert.True(completar > 0 && desechar > completar);
        Assert.DoesNotContain("catch", codigo[completar..desechar], StringComparison.Ordinal);
    }

    // ============================================================ montaje

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private sealed record Montaje(BaseGuionadaDeVentas Base, CreadorDePedidos Creador, Registro Registro)
    {
        public Task<ResultadoDeCreacion> CrearAsync()
            => Creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken);
    }

    private static (BaseGuionadaDeVentas Base, SalesDbContext Contexto, Registro Registro) Base()
    {
        var base_ = new BaseGuionadaDeVentas();
        return (base_, base_.Contexto(), new Registro());
    }

    private static Montaje Montar(
        Func<BaseGuionadaDeVentas, string, Exception?>? falla = null,
        Func<BaseGuionadaDeVentas, InstallationCurrencyReading>? enTransaccion = null,
        Func<Exception?>? alDeshacer = null,
        Func<BaseGuionadaDeVentas, Exception?>? alConfirmar = null,
        bool guardadoSuprimido = false)
    {
        var base_ = new BaseGuionadaDeVentas
        {
            Responde = sql => sql.Contains("current_setting('lock_timeout')", StringComparison.Ordinal)
                ? BaseGuionadaDeVentas.Escalar("Value", "0")
                : null,
            RespondeEscalar = sql => sql.Contains(Serie, StringComparison.Ordinal) ? 7 : null,
            SuprimirGuardado = guardadoSuprimido,
        };
        base_.FallaAlEjecutar = sql => falla?.Invoke(base_, sql);
        base_.FallaAlDeshacer = alDeshacer ?? (() => null);
        base_.FallaAlConfirmar = () => alConfirmar?.Invoke(base_);

        var registro = new Registro();
        var contexto = base_.Contexto();
        var creador = new CreadorDePedidos(
            contexto,
            new CongeladorDeCliente(new Dobles.M04Dice(Dobles.Cliente(Guid.CreateVersion7()))),
            new Dobles.CatalogoDice().Con(Item, activo: true, precio: 8.50m),
            new OrderCodeAllocator(contexto, new NodeIdentity("prueba"), new Dobles.RelojFijo(Ahora)),
            new Dobles.AjustesDice(),
            new AutoridadDeMonedaDeCore(
                new CorePrevia(),
                new CoreEnTransaccion(base_, enTransaccion ?? (_ => Acreditada)),
                registro.Para<AutoridadDeMonedaDeCore>()),
            new Dobles.RelojFijo(Ahora),
            registro.Para<CreadorDePedidos>());

        return new Montaje(base_, creador, registro);
    }

    /// <summary>El servidor cierra la sesión del pedido, como un <c>pg_terminate_backend</c>.</summary>
    private static PostgresException Terminada()
        => new("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown);

    private static Exception Cortar(BaseGuionadaDeVentas base_, Exception corte)
    {
        base_.Romper(corte);
        return corte;
    }

    private static bool EsInsercionDelPedido(string sql)
        => Regex.IsMatch(sql, @"INSERT INTO ""?sales""?\.""?orders""?\s*\(");

    private static InstallationCurrencyReading Acreditada => new(
        InstallationCurrencyStatus.Accredited, new SupportedCurrency("PEN", 2), SupportedCurrencies.Version, Ahora);

    private static InstallationCurrencyReading Con(InstallationCurrencyStatus estado) => new(
        estado, null, SupportedCurrencies.Version, Ahora);

    private sealed class CorePrevia : IInstallationCurrencyAuthority
    {
        public Task<InstallationCurrencyReading> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Acreditada);
    }

    private sealed class CoreEnTransaccion(BaseGuionadaDeVentas base_, Func<BaseGuionadaDeVentas, InstallationCurrencyReading> responde)
        : IInstallationCurrencyTransactionalAuthority
    {
        public Task<InstallationCurrencyReading> ReadInTransactionAsync(DbTransaction transaction, CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(responde(base_));
            }
            catch (Exception excepcion)
            {
                return Task.FromException<InstallationCurrencyReading>(excepcion);
            }
        }
    }

    private sealed class Registro
    {
        public List<(LogLevel Nivel, EventId Evento, Exception? Excepcion, string Mensaje)> Entradas { get; } = [];

        public ILogger<T> Para<T>() => new Captura<T>(this);

        private sealed class Captura<T>(Registro destino) : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => destino.Entradas.Add((logLevel, eventId, exception, formatter(state, exception)));
        }
    }

    private static string SoloCodigo(string relativa)
    {
        for (var directorio = new DirectoryInfo(AppContext.BaseDirectory); directorio is not null; directorio = directorio.Parent)
        {
            var ruta = Path.Combine(directorio.FullName, "backend", relativa.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(Path.Combine(directorio.FullName, "backend", "Sillar.sln")))
            {
                return string.Join('\n', File.ReadAllLines(ruta)
                    .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                        && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)));
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
