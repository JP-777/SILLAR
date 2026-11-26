using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Endpoints;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// R10-E13-B, sin PostgreSQL: el pedido lee la moneda acreditada en su propia transacción
/// y conexión, y distingue «sin moneda» de «no se pudo leer» y de «costura mal usada».
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué demuestran.</b> Que, con el adaptador real <see cref="AutoridadDeMonedaDeCore"/>
/// y los contratos de CORE simulados, el creador hace <b>una sola</b> lectura con la conexión
/// de CORE —antes de abrir su transacción— y la lectura que congela la pide con
/// <b>la misma</b> <see cref="DbTransaction"/> que abrió EF, cuya conexión es la del
/// contexto de Ventas.
/// </para>
/// <para>
/// <b>Qué no demuestran.</b> Que PostgreSQL reciba esas sentencias por esa conexión, ni que
/// el pool no se agote: eso solo lo acredita PostgreSQL físico
/// (<c>UnaConexionBajoElBloqueoPersistenciaTests</c>, preparadas y no ejecutadas aquí).
/// </para>
/// </remarks>
public sealed class UnaConexionBajoElBloqueoTests
{
    private const string LecturaPropiaDeCore = "CORE-LECTURA-CON-SU-CONEXION";

    private const string LecturaEnTransaccionDeCore = "CORE-LECTURA-EN-LA-TRANSACCION-DEL-PEDIDO";

    private const string CadenaSinBase =
        "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";

    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Item = Guid.CreateVersion7();

    private static InstallationCurrencyReading Acreditada => new(
        InstallationCurrencyStatus.Accredited, new SupportedCurrency("PEN", 2), SupportedCurrencies.Version, Ahora);

    private static InstallationCurrencyReading Con(InstallationCurrencyStatus estado) => new(
        estado, null, SupportedCurrencies.Version, Ahora);

    // ================================================================== una sola conexión

    [Fact]
    public async Task La_unica_lectura_con_la_conexion_de_CORE_es_la_previa_y_va_antes_de_abrir_la_transaccion()
    {
        var m = Montar();

        await Assert.ThrowsAsync<NumeracionDetenida>(() => m.CrearAsync());

        Assert.Equal(1, m.Core.Lecturas);
        var pasos = m.Base.Pasos;
        Assert.Equal(LecturaPropiaDeCore, pasos[0]);
        Assert.Equal("BEGIN", pasos[1]);
        Assert.Single(pasos, p => p == LecturaPropiaDeCore);
    }

    [Fact]
    public async Task La_lectura_que_se_congela_va_en_la_misma_transaccion_y_conexion_del_pedido_y_despues_del_bloqueo()
    {
        var m = Montar();

        // El numerador se detiene a propósito: la base guionada no sabe insertar. Lo que
        // importa es lo que pasó antes de numerar.
        await Assert.ThrowsAsync<NumeracionDetenida>(() => m.CrearAsync());

        var transaccion = Assert.Single(m.Base.Transacciones);
        var recibida = Assert.Single(m.CoreEnTransaccion.Transacciones);
        Assert.Same(transaccion, recibida);
        Assert.Same(m.Base.Conexion, m.CoreEnTransaccion.Conexiones.Single());

        var pasos = m.Base.Pasos;
        var bloqueo = pasos.FindIndex(p => p.Contains("pg_advisory_xact_lock_shared(", StringComparison.Ordinal));
        var lectura = pasos.IndexOf(LecturaEnTransaccionDeCore);
        var numero = pasos.FindIndex(p => p.Contains("order_series", StringComparison.Ordinal));
        Assert.True(bloqueo > 0 && lectura > bloqueo && numero > lectura, string.Join(" | ", pasos));
        Assert.Equal("ROLLBACK", pasos[^1]);
    }

    [Fact]
    public async Task La_transaccion_propia_se_abre_en_read_committed_explicito()
    {
        var m = Montar();

        await Assert.ThrowsAsync<NumeracionDetenida>(() => m.CrearAsync());

        Assert.Equal([IsolationLevel.ReadCommitted], m.Base.Aislamientos);
    }

    [Fact]
    public async Task Desde_el_bloqueo_hasta_el_final_el_creador_no_pide_nada_que_abra_otra_conexion()
    {
        // Lectura del código: entre la barrera y la confirmación no hay ninguna lectura con
        // la conexión de CORE, ni ámbitos, contextos o conexiones nuevas.
        var fuente = Fuente("Sillar.Modules.Sales/Pedidos/CreadorDePedidos.cs");
        var desde = fuente.IndexOf("BarreraMonetariaDeVentas.AdquirirCompartidaAsync", StringComparison.Ordinal);
        var hasta = fuente.IndexOf("transaccion.CompletarAsync", StringComparison.Ordinal);
        Assert.True(desde > 0 && hasta > desde);
        var tramo = fuente[desde..hasta];

        Assert.DoesNotContain("MonedaDeVentaAsync", tramo, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadAsync", tramo, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateScope", tramo, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", tramo, StringComparison.Ordinal);
        Assert.DoesNotContain("new SalesDbContext", tramo, StringComparison.Ordinal);
        Assert.DoesNotContain("NpgsqlConnection", tramo, StringComparison.Ordinal);
        Assert.Contains("database.Database.CurrentTransaction!.GetDbTransaction()", tramo, StringComparison.Ordinal);

        // Y el creador no conoce el lector de CORE con conexión propia: solo el adaptador.
        Assert.DoesNotContain("IInstallationCurrencyAuthority", fuente, StringComparison.Ordinal);
    }

    [Fact]
    public void Ventas_no_copia_la_consulta_privada_de_CORE()
    {
        // La lectura y la evaluación son las de CORE: Ventas no nombra sus tablas ni su clave.
        foreach (var archivo in ArchivosDeVentas())
        {
            // Solo el código: los comentarios pueden nombrarlas para decir que no se tocan.
            var texto = string.Join('\n', File.ReadAllLines(archivo)
                .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            Assert.DoesNotContain("site_settings", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("audit_log", texto, StringComparison.Ordinal);
            Assert.DoesNotContain(InstallationCurrency.SettingKey, texto, StringComparison.Ordinal);
            Assert.DoesNotContain("pg_locks", texto, StringComparison.Ordinal);
        }
    }

    // ================================================================== rechazos sin tocar la base

    [Fact]
    public async Task Si_la_lectura_previa_no_se_pudo_hacer_no_se_abre_nada_y_no_se_dice_que_falte_la_moneda()
    {
        var m = Montar(previa: Con(InstallationCurrencyStatus.Unavailable));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.Equal([LecturaPropiaDeCore], m.Base.Pasos);
        Assert.Empty(m.CoreEnTransaccion.Transacciones);
    }

    [Fact]
    public async Task Si_la_lectura_previa_lanza_tampoco_se_dice_que_falte_la_moneda()
    {
        var m = Montar(previaLanza: new NpgsqlException("pool agotado", new TimeoutException()));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.DoesNotContain("BEGIN", m.Base.Pasos);
    }

    [Theory]
    [InlineData(InstallationCurrencyStatus.NotPrepared)]
    [InlineData(InstallationCurrencyStatus.NotEstablished)]
    [InlineData(InstallationCurrencyStatus.Unsupported)]
    [InlineData(InstallationCurrencyStatus.NotAttested)]
    public async Task Sin_moneda_acreditada_en_la_lectura_previa_sigue_sin_abrirse_nada_D20_A(InstallationCurrencyStatus estado)
    {
        var m = Montar(previa: Con(estado));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.MonedaSinAutoridad, r.Motivo);
        Assert.Equal([LecturaPropiaDeCore], m.Base.Pasos);
    }

    // ================================================================== rechazos dentro de la transacción

    [Theory]
    [InlineData(InstallationCurrencyStatus.NotPrepared)]
    [InlineData(InstallationCurrencyStatus.NotEstablished)]
    [InlineData(InstallationCurrencyStatus.Unsupported)]
    [InlineData(InstallationCurrencyStatus.NotAttested)]
    public async Task Si_bajo_el_bloqueo_ya_no_esta_acreditada_no_nace_no_se_numera_y_se_deshace(InstallationCurrencyStatus estado)
    {
        var m = Montar(enTransaccion: Con(estado));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.MonedaSinAutoridad, r.Motivo);
        Assert.Null(r.OrderCode);
        SinNumeroNiInsercionYDeshecha(m.Base);
    }

    [Fact]
    public async Task Si_bajo_el_bloqueo_CORE_no_pudo_leer_no_esta_disponible_y_se_deshace()
    {
        var m = Montar(enTransaccion: Con(InstallationCurrencyStatus.Unavailable));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        SinNumeroNiInsercionYDeshecha(m.Base);
    }

    [Fact]
    public async Task Si_CORE_rechaza_la_transaccion_por_uso_fuera_de_contrato_es_un_defecto_registrado_y_se_deshace()
    {
        var registro = new RegistroCapturado();
        var m = Montar(
            enTransaccionLanza: new InvalidOperationException("La transacción comercial no tiene el bloqueo de moneda"),
            registro: registro);

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.CosturaMonetariaDefectuosa, r.Motivo);
        SinNumeroNiInsercionYDeshecha(m.Base);
        var error = Assert.Single(registro.Entradas, e => e.Nivel == LogLevel.Error);
        Assert.Equal(AutoridadDeMonedaDeCore.CosturaFueraDeContrato, error.Evento);
        Assert.IsType<InvalidOperationException>(error.Excepcion);
    }

    [Fact]
    public async Task Si_la_lectura_en_transaccion_lanza_otro_fallo_no_esta_disponible()
    {
        var m = Montar(enTransaccionLanza: new NpgsqlException("conexión cortada", new IOException()));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        SinNumeroNiInsercionYDeshecha(m.Base);
    }

    [Fact]
    public async Task La_cancelacion_durante_la_lectura_en_transaccion_se_propaga_y_deshace_la_propia()
    {
        var m = Montar(enTransaccionLanza: new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => m.CrearAsync());

        SinNumeroNiInsercionYDeshecha(m.Base);
    }

    // ================================================================== fallos de infraestructura

    [Fact]
    public async Task Si_no_se_puede_abrir_la_transaccion_por_un_fallo_transitorio_no_esta_disponible()
    {
        var m = Montar(alEmpezar: () => new NpgsqlException("The connection pool has been exhausted", new TimeoutException()));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.Equal([LecturaPropiaDeCore], m.Base.Pasos);
        Assert.Empty(m.CoreEnTransaccion.Transacciones);
    }

    [Fact]
    public async Task Tambien_sin_envolver_el_fallo_transitorio_al_abrir_no_esta_disponible()
    {
        // Así lo lanza Npgsql; EF lo envuelve fuera de transacción (la prueba de arriba).
        // Cualquiera de las dos formas es lo mismo para el cliente.
        var m = Montar(alEmpezar: () => new NpgsqlException("The connection pool has been exhausted", new TimeoutException()), envolverAlEmpezar: false);

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
    }

    [Fact]
    public async Task La_espera_del_bloqueo_agotada_envuelta_por_EF_sigue_siendo_moneda_en_cambio()
    {
        var m = Montar(falla: sql => sql.Contains("pg_advisory_xact_lock_shared(", StringComparison.Ordinal)
            ? new InvalidOperationException(
                "An exception has been raised that is likely due to a transient failure.",
                new PostgresException("canceling statement due to lock timeout", "ERROR", "ERROR", PostgresErrorCodes.LockNotAvailable))
            : null);

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.MonedaEnCambio, r.Motivo);
        SinNumeroNiInsercionYDeshecha(m.Base);
    }

    [Fact]
    public async Task Un_fallo_transitorio_envuelto_que_sale_de_la_lectura_de_CORE_no_es_uso_indebido()
    {
        var m = Montar(enTransaccionLanza: new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new NpgsqlException("Exception while reading from stream", new IOException())));

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        SinNumeroNiInsercionYDeshecha(m.Base);
    }

    [Fact]
    public async Task Un_fallo_no_transitorio_al_abrir_la_transaccion_sale_tal_cual()
    {
        var m = Montar(alEmpezar: () => new PostgresException("simulado", "FATAL", "FATAL", "28P01"));

        await Assert.ThrowsAsync<PostgresException>(() => m.CrearAsync());
    }

    [Fact]
    public async Task Si_la_conexion_se_corta_al_pedir_el_bloqueo_no_esta_disponible_y_no_es_moneda_en_cambio()
    {
        var m = Montar(falla: sql => sql.Contains("pg_advisory_xact_lock_shared(", StringComparison.Ordinal)
            ? new NpgsqlException("Exception while reading from stream", new IOException())
            : null);

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.Empty(m.CoreEnTransaccion.Transacciones);
        SinNumeroNiInsercionYDeshecha(m.Base);
    }

    [Fact]
    public async Task Si_la_conexion_se_corta_al_numerar_antes_del_COMMIT_no_esta_disponible_y_se_deshace()
    {
        var m = Montar(falla: sql => sql.Contains("order_series", StringComparison.Ordinal)
            ? new NpgsqlException("Exception while reading from stream", new IOException())
            : null);

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.Null(r.OrderCode);
        Assert.DoesNotContain("COMMIT", m.Base.Pasos);
        Assert.Equal("ROLLBACK", m.Base.Pasos[^1]);
    }

    [Fact]
    public void El_COMMIT_queda_fuera_de_la_captura_de_fallos_transitorios()
    {
        // Si el COMMIT falla no se sabe si el pedido quedó: decir «vuelve a confirmarlo»
        // podría duplicarlo. Por eso la captura termina en SaveChanges.
        var fuente = Fuente("Sillar.Modules.Sales/Pedidos/CreadorDePedidos.cs");
        var guardado = fuente.IndexOf("await database.SaveChangesAsync(cancellationToken);", StringComparison.Ordinal);
        var captura = fuente.IndexOf("catch (Exception excepcion)", guardado, StringComparison.Ordinal);
        var confirmacion = fuente.IndexOf("await transaccion.CompletarAsync(cancellationToken);", StringComparison.Ordinal);

        Assert.True(guardado > 0 && captura > guardado && confirmacion > captura);
        Assert.DoesNotContain("catch", fuente[confirmacion..fuente.IndexOf("return new ResultadoDeCreacion(", confirmacion, StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void Un_fallo_transitorio_envuelto_por_SaveChanges_tambien_es_transitorio()
    {
        Assert.True(FallosDeInfraestructura.EsTransitorio(
            new DbUpdateException("guardando", new NpgsqlException("Exception while reading from stream", new IOException()))));
        Assert.False(FallosDeInfraestructura.EsTransitorio(
            new DbUpdateException("guardando", new PostgresException("simulado", "ERROR", "ERROR", "23505"))));
        Assert.False(FallosDeInfraestructura.EsEsperaDelBloqueoAgotada(
            new PostgresException("simulado", "ERROR", "ERROR", "40001")));
    }

    [Fact]
    public void El_agotamiento_del_pool_que_lanza_Npgsql_se_clasifica_como_transitorio()
    {
        // Npgsql agota el pool con una NpgsqlException cuya causa es un TimeoutException
        // (R10-E12, api-pool3-1.log:32). Que eso sea IsTransient es lo que hace que el
        // creador lo trate como no disponible. Si Npgsql cambia, esta prueba lo dice.
        Assert.True(new NpgsqlException("The connection pool has been exhausted", new TimeoutException()).IsTransient);
        Assert.True(new NpgsqlException("Exception while reading from stream", new IOException()).IsTransient);
        Assert.False(new PostgresException("simulado", "ERROR", "ERROR", "42501").IsTransient);

        // 55P03 también es transitorio para Npgsql: por eso el creador pregunta primero si
        // es la espera del bloqueo, y solo después si es transitorio.
        Assert.True(new PostgresException("simulado", "ERROR", "ERROR", PostgresErrorCodes.LockNotAvailable).IsTransient);
    }

    // ================================================================== transacción prestada

    [Theory]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    [InlineData(IsolationLevel.Snapshot)]
    public async Task Una_transaccion_prestada_con_otro_aislamiento_se_rechaza_sin_bloqueo_y_sin_tocarla(IsolationLevel aislamiento)
    {
        var registro = new RegistroCapturado();
        var m = Montar(registro: registro);
        await m.Contexto.Database.BeginTransactionAsync(aislamiento, TestContext.Current.CancellationToken);

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.CosturaMonetariaDefectuosa, r.Motivo);
        Assert.Equal(["BEGIN", LecturaPropiaDeCore], m.Base.Pasos);   // la del llamador; ni bloqueo, ni COMMIT, ni ROLLBACK
        Assert.Empty(m.CoreEnTransaccion.Transacciones);
        Assert.NotNull(m.Contexto.Database.CurrentTransaction);        // sigue siendo del llamador
        Assert.Equal(AutoridadDeMonedaDeCore.CosturaFueraDeContrato, Assert.Single(registro.Entradas, e => e.Nivel == LogLevel.Error).Evento);
    }

    [Fact]
    public async Task Con_una_transaccion_prestada_read_committed_se_usa_esa_misma_para_leer()
    {
        var m = Montar();
        await m.Contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<NumeracionDetenida>(() => m.CrearAsync());

        Assert.Same(Assert.Single(m.Base.Transacciones), Assert.Single(m.CoreEnTransaccion.Transacciones));
    }

    [Fact]
    public async Task Si_la_lectura_falla_con_una_transaccion_prestada_no_se_confirma_ni_se_deshace_la_ajena()
    {
        var m = Montar(enTransaccion: Con(InstallationCurrencyStatus.Unavailable));
        await m.Contexto.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var r = await m.CrearAsync();

        Assert.Equal(MotivoDeNoCreacion.ServicioNoDisponible, r.Motivo);
        Assert.DoesNotContain("COMMIT", m.Base.Pasos);
        Assert.DoesNotContain("ROLLBACK", m.Base.Pasos);
        Assert.NotNull(m.Contexto.Database.CurrentTransaction);
    }

    // ================================================================== adaptador

    [Fact]
    public async Task El_adaptador_pasa_a_CORE_la_misma_transaccion_y_no_usa_el_lector_con_conexion_propia()
    {
        var core = new CoreDice(Acreditada);
        var enTransaccion = new CoreEnTransaccionDice(Acreditada);
        var adaptador = new AutoridadDeMonedaDeCore(core, enTransaccion, NullLogger<AutoridadDeMonedaDeCore>.Instance);
        var transaccion = await TransaccionGuionadaAsync();

        var lectura = await adaptador.LeerEnTransaccionAsync(transaccion, TestContext.Current.CancellationToken);

        Assert.Equal(LecturaDeMonedaDeVenta.Acreditada("PEN"), lectura);
        Assert.Same(transaccion, Assert.Single(enTransaccion.Transacciones));
        Assert.Equal(0, core.Lecturas);
    }

    [Fact]
    public async Task La_lectura_previa_del_adaptador_no_usa_la_transaccional()
    {
        var core = new CoreDice(Acreditada);
        var enTransaccion = new CoreEnTransaccionDice(Acreditada);
        var adaptador = new AutoridadDeMonedaDeCore(core, enTransaccion, NullLogger<AutoridadDeMonedaDeCore>.Instance);

        Assert.Equal(LecturaDeMonedaDeVenta.Acreditada("PEN"), await adaptador.MonedaDeVentaAsync(TestContext.Current.CancellationToken));
        Assert.Empty(enTransaccion.Transacciones);
    }

    [Fact]
    public async Task Acreditada_sin_moneda_en_la_transaccion_es_sin_autoridad()
    {
        var adaptador = new AutoridadDeMonedaDeCore(
            new CoreDice(Acreditada),
            new CoreEnTransaccionDice(new(InstallationCurrencyStatus.Accredited, null, SupportedCurrencies.Version, Ahora)),
            NullLogger<AutoridadDeMonedaDeCore>.Instance);

        Assert.Equal(
            LecturaDeMonedaDeVenta.SinAutoridad,
            await adaptador.LeerEnTransaccionAsync(await TransaccionGuionadaAsync(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Con_el_token_ya_cancelado_la_lectura_en_transaccion_no_pregunta_a_CORE()
    {
        var enTransaccion = new CoreEnTransaccionDice(Acreditada);
        var adaptador = new AutoridadDeMonedaDeCore(new CoreDice(Acreditada), enTransaccion, NullLogger<AutoridadDeMonedaDeCore>.Instance);
        var transaccion = await TransaccionGuionadaAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adaptador.LeerEnTransaccionAsync(transaccion, new CancellationToken(canceled: true)));
        Assert.Empty(enTransaccion.Transacciones);
    }

    [Fact]
    public async Task Sin_transaccion_el_adaptador_se_niega_antes_de_preguntar_a_CORE()
    {
        var enTransaccion = new CoreEnTransaccionDice(Acreditada);
        var adaptador = new AutoridadDeMonedaDeCore(new CoreDice(Acreditada), enTransaccion, NullLogger<AutoridadDeMonedaDeCore>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => adaptador.LeerEnTransaccionAsync(null!, TestContext.Current.CancellationToken));
        Assert.Empty(enTransaccion.Transacciones);
    }

    // ================================================================== respuesta HTTP

    [Theory]
    [InlineData(MotivoDeNoCreacion.MonedaSinAutoridad, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(MotivoDeNoCreacion.MonedaEnCambio, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(MotivoDeNoCreacion.ServicioNoDisponible, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(MotivoDeNoCreacion.CosturaMonetariaDefectuosa, StatusCodes.Status500InternalServerError)]
    public void Cada_motivo_monetario_tiene_su_codigo_HTTP(MotivoDeNoCreacion motivo, int codigo)
        => Assert.Equal(codigo, Problema(motivo).StatusCode);

    [Fact]
    public void Las_cuatro_frases_monetarias_son_distintas()
    {
        MotivoDeNoCreacion[] motivos =
        [
            MotivoDeNoCreacion.MonedaSinAutoridad,
            MotivoDeNoCreacion.MonedaEnCambio,
            MotivoDeNoCreacion.ServicioNoDisponible,
            MotivoDeNoCreacion.CosturaMonetariaDefectuosa,
        ];

        Assert.Equal(4, motivos.Select(m => Problema(m).ProblemDetails.Title).Distinct().Count());
        Assert.Equal(4, motivos.Select(m => Problema(m).ProblemDetails.Detail).Distinct().Count());
    }

    [Theory]
    [InlineData(MotivoDeNoCreacion.ServicioNoDisponible)]
    [InlineData(MotivoDeNoCreacion.CosturaMonetariaDefectuosa)]
    public void No_disponible_y_defecto_no_dicen_que_falte_configurar_la_moneda(MotivoDeNoCreacion motivo)
    {
        var problema = Problema(motivo).ProblemDetails;
        var texto = $"{problema.Title} {problema.Detail}";

        Assert.DoesNotContain("moneda", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("configurar", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("carrito se conserva", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void El_defecto_de_integracion_no_expone_nada_interno()
    {
        var problema = Problema(MotivoDeNoCreacion.CosturaMonetariaDefectuosa).ProblemDetails;
        var texto = $"{problema.Title} {problema.Detail}";

        foreach (var interno in new[] { "CORE", "transacción", "aislamiento", "bloqueo", "Exception", "SQL", "pool", "contrato" })
        {
            Assert.DoesNotContain(interno, texto, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(problema.Extensions, e => e.Key != "traceId");
    }

    [Fact]
    public void Ningun_motivo_de_rechazo_cae_en_la_respuesta_sin_frase()
    {
        foreach (var motivo in Enum.GetValues<MotivoDeNoCreacion>().Where(m => m != MotivoDeNoCreacion.Ninguno))
        {
            var problema = Problema(motivo).ProblemDetails;
            Assert.False(string.IsNullOrWhiteSpace(problema.Title), motivo.ToString());
            Assert.False(string.IsNullOrWhiteSpace(problema.Detail), motivo.ToString());
        }
    }

    // ================================================================== registro

    [Fact]
    public void Con_los_dos_lectores_de_CORE_presentes_la_autoridad_de_M03_se_resuelve_por_peticion()
    {
        var servicios = ServiciosDeM03();
        servicios.AddLogging();
        servicios.AddScoped<IInstallationCurrencyAuthority>(_ => new CoreDice(Acreditada));
        servicios.AddScoped<IInstallationCurrencyTransactionalAuthority>(_ => new CoreEnTransaccionDice(Acreditada));

        using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
        using var ambito = proveedor.CreateScope();

        Assert.IsType<AutoridadDeMonedaDeCore>(ambito.ServiceProvider.GetRequiredService<IAutoridadDeMoneda>());
    }

    [Fact]
    public void M03_no_registra_el_lector_transaccional_de_CORE_ni_cambia_sus_propios_registros_monetarios()
    {
        var servicios = ServiciosDeM03();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IInstallationCurrencyTransactionalAuthority));
        var autoridad = Assert.Single(servicios, d => d.ServiceType == typeof(IAutoridadDeMoneda));
        Assert.Equal((typeof(AutoridadDeMonedaDeCore), ServiceLifetime.Scoped), (autoridad.ImplementationType, autoridad.Lifetime));
        var sonda = Assert.Single(servicios, d => d.ServiceType == typeof(IMonetaryRecordsProbe));
        Assert.Equal((typeof(SondaDeRegistrosMonetarios), ServiceLifetime.Scoped), (sonda.ImplementationType, sonda.Lifetime));
    }

    // ================================================================== montaje

    private static void SinNumeroNiInsercionYDeshecha(BaseGuionadaDeVentas base_)
    {
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("order_series", StringComparison.Ordinal));
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("COMMIT", base_.Pasos);
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
    }

    private static ProblemHttpResult Problema(MotivoDeNoCreacion motivo)
        => Assert.IsType<ProblemHttpResult>(PedidosDelClienteEndpoints.RespuestaDeRechazo(motivo));

    private static async Task<DbTransaction> TransaccionGuionadaAsync()
    {
        var base_ = new BaseGuionadaDeVentas();
        var contexto = base_.Contexto();
        await contexto.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        return Assert.Single(base_.Transacciones);
    }

    private sealed record Montaje(
        BaseGuionadaDeVentas Base,
        SalesDbContext Contexto,
        CreadorDePedidos Creador,
        CoreDice Core,
        CoreEnTransaccionDice CoreEnTransaccion)
    {
        public Task<ResultadoDeCreacion> CrearAsync()
            => Creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// El creador con el adaptador real sobre CORE simulado, en la base guionada. El
    /// numerador se detiene con <see cref="NumeracionDetenida"/>: lo que se mira es lo
    /// anterior a numerar.
    /// </summary>
    private static Montaje Montar(
        InstallationCurrencyReading? previa = null,
        Exception? previaLanza = null,
        InstallationCurrencyReading? enTransaccion = null,
        Exception? enTransaccionLanza = null,
        Func<string, Exception?>? falla = null,
        Func<Exception?>? alEmpezar = null,
        RegistroCapturado? registro = null,
        bool envolverAlEmpezar = true)
    {
        var base_ = new BaseGuionadaDeVentas
        {
            Responde = sql => sql.Contains("current_setting('lock_timeout')", StringComparison.Ordinal)
                ? BaseGuionadaDeVentas.Escalar("Value", "0")
                : null,
            FallaAlEjecutar = sql => falla?.Invoke(sql)
                ?? (sql.Contains("order_series", StringComparison.Ordinal) ? new NumeracionDetenida() : null),
            // Fuera de transacción, EF entrega envuelto lo que Npgsql da por transitorio.
            FallaAlEmpezar = alEmpezar is null
                ? () => null
                : envolverAlEmpezar
                    ? () => alEmpezar() is { } e
                        ? e is NpgsqlException { IsTransient: true }
                            ? new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", e)
                            : e
                        : null
                    : alEmpezar,
        };
        var contexto = base_.Contexto();
        var core = new CoreDice(previa ?? Acreditada, previaLanza, base_);
        var coreEnTransaccion = new CoreEnTransaccionDice(enTransaccion ?? Acreditada, enTransaccionLanza, base_);
        registro ??= new RegistroCapturado();

        var creador = new CreadorDePedidos(
            contexto,
            new CongeladorDeCliente(new Dobles.M04Dice(Dobles.Cliente(Guid.CreateVersion7()))),
            new Dobles.CatalogoDice().Con(Item, activo: true, precio: 8.50m),
            new OrderCodeAllocator(contexto, new NodeIdentity("prueba"), TimeProvider.System),
            new Dobles.AjustesDice(),
            new AutoridadDeMonedaDeCore(core, coreEnTransaccion, registro.Para<AutoridadDeMonedaDeCore>()),
            new Dobles.RelojFijo(Ahora),
            registro.Para<CreadorDePedidos>());

        return new Montaje(base_, contexto, creador, core, coreEnTransaccion);
    }

    /// <summary>Lo que lanza la base guionada al numerar: aquí termina lo que se mira.</summary>
    private sealed class NumeracionDetenida() : Exception("La base guionada no numera.");

    /// <summary>El lector de CORE con su propia conexión, simulado.</summary>
    private sealed class CoreDice(InstallationCurrencyReading lectura, Exception? lanza = null, BaseGuionadaDeVentas? base_ = null)
        : IInstallationCurrencyAuthority
    {
        public int Lecturas { get; private set; }

        public Task<InstallationCurrencyReading> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Lecturas++;
            base_?.Pasos.Add(LecturaPropiaDeCore);
            return lanza is not null ? Task.FromException<InstallationCurrencyReading>(lanza) : Task.FromResult(lectura);
        }
    }

    /// <summary>El lector transaccional de CORE, simulado: anota la transacción y su conexión.</summary>
    private sealed class CoreEnTransaccionDice(InstallationCurrencyReading lectura, Exception? lanza = null, BaseGuionadaDeVentas? base_ = null)
        : IInstallationCurrencyTransactionalAuthority
    {
        public List<DbTransaction> Transacciones { get; } = [];

        public List<DbConnection?> Conexiones { get; } = [];

        public Task<InstallationCurrencyReading> ReadInTransactionAsync(DbTransaction transaction, CancellationToken cancellationToken)
        {
            Transacciones.Add(transaction);
            Conexiones.Add(transaction.Connection);
            base_?.Pasos.Add(LecturaEnTransaccionDeCore);
            return lanza is not null ? Task.FromException<InstallationCurrencyReading>(lanza) : Task.FromResult(lectura);
        }
    }

    /// <summary>Un registro que guarda nivel, evento y excepción de cada entrada.</summary>
    private sealed class RegistroCapturado
    {
        public List<(LogLevel Nivel, EventId Evento, Exception? Excepcion, string Mensaje)> Entradas { get; } = [];

        public ILogger<T> Para<T>() => new Captura<T>(this);

        private sealed class Captura<T>(RegistroCapturado destino) : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => destino.Entradas.Add((logLevel, eventId, exception, formatter(state, exception)));
        }
    }

    private static ServiceCollection ServiciosDeM03()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SalesModule.ConnectionStringName}"] = CadenaSinBase,
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddSingleton(TimeProvider.System);
        new SalesModule().RegisterServices(servicios, configuracion);
        return servicios;
    }

    private static string Backend()
    {
        for (var directorio = new DirectoryInfo(AppContext.BaseDirectory); directorio is not null; directorio = directorio.Parent)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "backend", "Sillar.sln")))
            {
                return Path.Combine(directorio.FullName, "backend");
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    private static string Fuente(string relativa)
        => File.ReadAllText(Path.Combine(Backend(), relativa.Replace('/', Path.DirectorySeparatorChar)));

    private static IEnumerable<string> ArchivosDeVentas()
    {
        var separador = Path.DirectorySeparatorChar;
        var raiz = Path.Combine(Backend(), "Sillar.Modules.Sales");
        return Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separador}obj{separador}", StringComparison.Ordinal)
                && !f.Contains($"{separador}bin{separador}", StringComparison.Ordinal)
                && !f.Contains($"{separador}Migrations{separador}", StringComparison.Ordinal));
    }
}
