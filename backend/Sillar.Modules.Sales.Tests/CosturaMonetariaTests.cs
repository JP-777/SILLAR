using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// La costura de M03 con la autoridad monetaria de CORE, sin PostgreSQL: el adaptador de
/// lectura acreditada, la sonda de pedidos, el orden de la barrera en la creación del
/// pedido, el registro y el inventario de escritores de <c>sales.orders</c>.
/// </summary>
/// <remarks>
/// La exclusión mutua de verdad no se prueba aquí: la prueban, contra PostgreSQL y con dos
/// conexiones, las de <c>CosturaMonetariaPersistenciaTests</c>.
/// </remarks>
public sealed class CosturaMonetariaTests
{
    private const string CadenaSinBase =
        "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";

    private const string LecturaDeMoneda = "LECTURA-DE-MONEDA";

    private static readonly DateTimeOffset Ahora = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly Regex MutanteOBloqueo = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|LOCK)\b"
            + @"|pg_advisory|\bFOR\s+(UPDATE|SHARE|NO\s+KEY|KEY)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // ================================================================== adaptador

    [Theory]
    [InlineData(InstallationCurrencyStatus.NotPrepared)]
    [InlineData(InstallationCurrencyStatus.NotEstablished)]
    [InlineData(InstallationCurrencyStatus.Unsupported)]
    [InlineData(InstallationCurrencyStatus.NotAttested)]
    [InlineData(InstallationCurrencyStatus.Unavailable)]
    public async Task Sin_lectura_acreditada_de_CORE_no_hay_moneda_de_venta(InstallationCurrencyStatus estado)
    {
        // Aunque CORE trajera una moneda en la lectura, sin Accredited no vale.
        var core = new CoreDice(new(estado, new SupportedCurrency("PEN", 2), SupportedCurrencies.Version, Ahora));

        Assert.Null(await Adaptador(core).MonedaDeVentaAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, core.Lecturas);
    }

    [Fact]
    public async Task Acreditada_pero_sin_moneda_no_hay_moneda_de_venta()
    {
        var core = new CoreDice(new(InstallationCurrencyStatus.Accredited, null, SupportedCurrencies.Version, Ahora));

        Assert.Null(await Adaptador(core).MonedaDeVentaAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Con_la_moneda_acreditada_por_CORE_la_moneda_de_venta_es_esa()
    {
        var core = new CoreDice(new(InstallationCurrencyStatus.Accredited, new SupportedCurrency("PEN", 2), SupportedCurrencies.Version, Ahora));

        Assert.Equal("PEN", await Adaptador(core).MonedaDeVentaAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Con_el_token_ya_cancelado_el_adaptador_no_pregunta_a_CORE()
    {
        var core = new CoreDice(new(InstallationCurrencyStatus.Accredited, new SupportedCurrency("PEN", 2), SupportedCurrencies.Version, Ahora));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Adaptador(core).MonedaDeVentaAsync(new CancellationToken(canceled: true)));
        Assert.Equal(0, core.Lecturas);
    }

    [Fact]
    public async Task La_cancelacion_que_propaga_CORE_sale_del_adaptador()
    {
        var core = new CoreDice(lanza: new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Adaptador(core).MonedaDeVentaAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void El_adaptador_solo_depende_del_lector_acreditado_de_CORE_y_no_de_la_configuracion()
    {
        // Sin ISettingsReader no hay forma de leer currency_code, currency_symbol ni la
        // caché de configuración.
        var parametros = typeof(AutoridadDeMonedaDeCore).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        Assert.Equal([typeof(IInstallationCurrencyAuthority), typeof(ILogger<AutoridadDeMonedaDeCore>)], parametros);
    }

    [Fact]
    public async Task Sin_autoridad_sigue_sin_haber_moneda_y_respeta_la_cancelacion()
    {
        Assert.Null(await new SinAutoridadDeMoneda().MonedaDeVentaAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SinAutoridadDeMoneda().MonedaDeVentaAsync(new CancellationToken(canceled: true)));
    }

    // ================================================================== sonda

    [Theory]
    [InlineData(false, MonetaryRecordsState.None)]
    [InlineData(true, MonetaryRecordsState.Present)]
    public async Task La_sonda_dice_lo_que_dice_la_base(bool hay, MonetaryRecordsState esperado)
    {
        var base_ = new BaseGuionadaDeVentas { Responde = _ => BaseGuionadaDeVentas.Escalar("Value", hay) };
        await using var contexto = base_.Contexto();

        Assert.Equal(esperado, await Sonda(contexto).ProbeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task La_sonda_hace_una_sola_lectura_de_sales_orders_sin_filtros_sin_escribir_y_sin_bloquear()
    {
        var base_ = new BaseGuionadaDeVentas { Responde = _ => BaseGuionadaDeVentas.Escalar("Value", false) };
        await using var contexto = base_.Contexto();

        await Sonda(contexto).ProbeAsync(TestContext.Current.CancellationToken);

        var sql = Assert.Single(base_.Pasos);
        Assert.Contains("FROM sales.orders", sql, StringComparison.Ordinal);
        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        // Ni is_active, ni estado, ni moneda: cualquier fila cuenta.
        Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(MutanteOBloqueo, sql);
        Assert.DoesNotContain("BEGIN", base_.Pasos);
    }

    [Fact]
    public void La_consulta_de_la_sonda_es_la_tabla_entera_de_pedidos()
    {
        // ToQueryString no abre conexión, pero necesita un comando real de Npgsql.
        using var contexto = ContextoSinBase();

        var sql = SondaDeRegistrosMonetarios.Pedidos(contexto).ToQueryString();

        Assert.Contains("FROM sales.orders", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("42P01")] // la tabla no existe: M03 sin migrar o desinstalado
    [InlineData("3F000")] // el schema no existe
    [InlineData("57P01")] // el servidor cerró la sesión
    [InlineData("40001")] // fallo de serialización
    public async Task Ante_un_error_de_PostgreSQL_la_sonda_no_sabe(string sqlState)
    {
        var base_ = new BaseGuionadaDeVentas
        {
            FallaAlEjecutar = _ => new PostgresException("simulado", "ERROR", "ERROR", sqlState),
        };
        await using var contexto = base_.Contexto();

        Assert.Equal(MonetaryRecordsState.Unknown, await Sonda(contexto).ProbeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Si_no_hay_conexion_la_sonda_no_sabe()
    {
        // Puerto cerrado de verdad: es Npgsql el que falla, no un doble.
        await using var contexto = ContextoSinBase();

        Assert.Equal(MonetaryRecordsState.Unknown, await Sonda(contexto).ProbeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ante_cualquier_otro_fallo_la_sonda_no_sabe()
    {
        var base_ = new BaseGuionadaDeVentas { FallaAlEjecutar = _ => new InvalidOperationException("simulado") };
        await using var contexto = base_.Contexto();

        Assert.Equal(MonetaryRecordsState.Unknown, await Sonda(contexto).ProbeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Con_el_token_ya_cancelado_la_sonda_no_toca_la_base_y_propaga()
    {
        var base_ = new BaseGuionadaDeVentas();
        await using var contexto = base_.Contexto();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Sonda(contexto).ProbeAsync(new CancellationToken(canceled: true)));
        Assert.Empty(base_.Pasos);
    }

    [Fact]
    public async Task Una_cancelacion_a_mitad_de_la_lectura_sale_como_cancelacion_y_no_como_no_se_sabe()
    {
        using var cancelacion = new CancellationTokenSource();
        var base_ = new BaseGuionadaDeVentas
        {
            FallaAlEjecutar = _ =>
            {
                cancelacion.Cancel();
                return new NpgsqlException("cancelado a mitad");
            },
        };
        await using var contexto = base_.Contexto();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sonda(contexto).ProbeAsync(cancelacion.Token));
    }

    [Fact]
    public async Task Con_una_transaccion_ya_abierta_en_su_contexto_la_sonda_no_sabe_y_no_lee()
    {
        var base_ = new BaseGuionadaDeVentas { Responde = _ => BaseGuionadaDeVentas.Escalar("Value", false) };
        await using var contexto = base_.Contexto();
        await using var tx = await contexto.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonetaryRecordsState.Unknown, await Sonda(contexto).ProbeAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["BEGIN"], base_.Pasos);
    }

    // ================================================================== creación del pedido

    [Fact]
    public async Task El_bloqueo_compartido_va_en_la_transaccion_antes_de_la_lectura_autentica_de_la_moneda()
    {
        // La primera lectura (rechazo rápido) dice PEN; la auténtica, ya con el bloqueo,
        // dice que no hay: CORE la cambió entre las dos. No nace el pedido.
        var (base_, creador) = Montaje(["PEN", null]);

        var r = await creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken);

        Assert.Equal(MotivoDeNoCreacion.MonedaSinAutoridad, r.Motivo);
        Assert.Null(r.OrderCode);

        var pasos = base_.Pasos;
        Assert.Equal(LecturaDeMoneda, pasos[0]);
        Assert.Equal("BEGIN", pasos[1]);
        var bloqueo = pasos.FindIndex(p => p.Contains("pg_advisory_xact_lock_shared(", StringComparison.Ordinal));
        var autentica = pasos.FindLastIndex(p => p == LecturaDeMoneda);
        Assert.True(bloqueo > 1, string.Join(" | ", pasos));
        Assert.True(autentica > bloqueo, string.Join(" | ", pasos));
        Assert.Equal("ROLLBACK", pasos[^1]);

        // Ni numeración ni inserción: la serie no se toca.
        Assert.DoesNotContain(pasos, p => p.Contains("order_series", StringComparison.Ordinal));
        Assert.DoesNotContain(pasos, p => p.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task El_bloqueo_es_el_compartido_de_transaccion_del_contrato_y_nunca_el_exclusivo_ni_el_de_sesion()
    {
        var (base_, creador) = Montaje(["PEN", null]);

        await creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken);

        var avisos = base_.Pasos.Where(p => p.Contains("pg_advisory", StringComparison.Ordinal)).ToArray();
        var unico = Assert.Single(avisos);
        Assert.StartsWith("SELECT pg_advisory_xact_lock_shared(", unico, StringComparison.Ordinal);
        Assert.Equal(
            "SELECT pg_advisory_xact_lock_shared({0}, {1})",
            MonetaryAuthorityLock.AcquireShared().Format);
    }

    [Fact]
    public async Task La_espera_del_bloqueo_se_acota_y_luego_se_restaura_la_de_la_transaccion()
    {
        var (base_, creador) = Montaje(["PEN", null]);

        await creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken);

        var pasos = base_.Pasos;
        var bloqueo = pasos.FindIndex(p => p.Contains("pg_advisory_xact_lock_shared(", StringComparison.Ordinal));
        Assert.Contains("current_setting('lock_timeout')", pasos[bloqueo - 2], StringComparison.Ordinal);
        Assert.Contains("set_config('lock_timeout'", pasos[bloqueo - 1], StringComparison.Ordinal);
        Assert.Contains("set_config('lock_timeout'", pasos[bloqueo + 1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Si_el_bloqueo_no_llega_a_tiempo_el_pedido_no_nace_no_se_numera_y_se_deshace()
    {
        var (base_, creador) = Montaje(["PEN", "PEN"], falla: sql => sql.Contains("pg_advisory_xact_lock_shared(", StringComparison.Ordinal)
            ? new PostgresException("canceling statement due to lock timeout", "ERROR", "ERROR", PostgresErrorCodes.LockNotAvailable)
            : null);

        var r = await creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken);

        Assert.Equal(MotivoDeNoCreacion.MonedaEnCambio, r.Motivo);
        Assert.Null(r.OrderCode);
        Assert.Single(base_.Pasos, p => p == LecturaDeMoneda); // la auténtica ya no se pide
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("order_series", StringComparison.Ordinal));
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
    }

    [Fact]
    public async Task Otro_error_al_pedir_el_bloqueo_no_se_disfraza_de_moneda_en_cambio()
    {
        var (_, creador) = Montaje(["PEN", "PEN"], falla: sql => sql.Contains("pg_advisory_xact_lock_shared(", StringComparison.Ordinal)
            ? new PostgresException("simulado", "ERROR", "ERROR", "57P01")
            : null);

        await Assert.ThrowsAsync<PostgresException>(
            () => creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sin_transaccion_abierta_la_barrera_se_niega_sin_tocar_la_base()
    {
        var base_ = new BaseGuionadaDeVentas();
        await using var contexto = base_.Contexto();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BarreraMonetariaDeVentas.AdquirirCompartidaAsync(contexto, TestContext.Current.CancellationToken));
        Assert.Empty(base_.Pasos);
    }

    [Fact]
    public void Moneda_en_cambio_y_moneda_sin_autoridad_son_motivos_distintos()
    {
        // Una se arregla sola en segundos; la otra exige que la tienda configure su moneda.
        var nombres = Enum.GetNames<MotivoDeNoCreacion>();
        Assert.Contains(nameof(MotivoDeNoCreacion.MonedaEnCambio), nombres);
        Assert.Contains(nameof(MotivoDeNoCreacion.MonedaSinAutoridad), nombres);
    }

    // ================================================================== registro

    [Fact]
    public void M03_registra_la_autoridad_de_CORE_por_peticion_y_ninguna_otra()
    {
        var servicios = ServiciosDeM03();

        var autoridad = Assert.Single(servicios, d => d.ServiceType == typeof(IAutoridadDeMoneda));
        Assert.Equal(typeof(AutoridadDeMonedaDeCore), autoridad.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, autoridad.Lifetime);
        Assert.DoesNotContain(servicios, d => d.ImplementationType == typeof(SinAutoridadDeMoneda));
    }

    [Fact]
    public void M03_registra_una_sola_sonda_de_pedidos_y_es_la_suya()
    {
        var servicios = ServiciosDeM03();

        var sonda = Assert.Single(servicios, d => d.ServiceType == typeof(IMonetaryRecordsProbe));
        Assert.Equal(typeof(SondaDeRegistrosMonetarios), sonda.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, sonda.Lifetime);
    }

    [Fact]
    public void M03_no_registra_ningun_productor_de_la_autoridad_de_CORE()
    {
        // La autoridad la da CORE. Si M03 la registrara, se la estaría inventando.
        Assert.DoesNotContain(ServiciosDeM03(), d => d.ServiceType == typeof(IInstallationCurrencyAuthority));
    }

    [Fact]
    public void Con_CORE_presente_la_autoridad_de_M03_se_resuelve_por_peticion()
    {
        var servicios = ServiciosDeM03();
        servicios.AddLogging();
        servicios.AddScoped<IInstallationCurrencyAuthority>(_ => new CoreDice(
            new(InstallationCurrencyStatus.NotEstablished, null, SupportedCurrencies.Version, Ahora)));

        using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
        using var ambito = proveedor.CreateScope();

        Assert.IsType<AutoridadDeMonedaDeCore>(ambito.ServiceProvider.GetRequiredService<IAutoridadDeMoneda>());
        Assert.IsType<SondaDeRegistrosMonetarios>(ambito.ServiceProvider.GetRequiredService<IMonetaryRecordsProbe>());
    }

    [Fact]
    public void Ventas_usa_la_misma_cadena_de_conexion_que_el_contrato_de_bloqueo_declara_comun()
    {
        // El bloqueo consultivo es de cada base: si Ventas y CORE se conectaran a bases
        // distintas, la barrera desaparecería sin avisar. Las dos usan «Default».
        Assert.Equal("Default", SalesModule.ConnectionStringName);

        // Y Ventas no tiene una cadena propia que la saque de esa base: con otra clave
        // presente en la configuración, sigue usando «Default».
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = CadenaSinBase,
                ["ConnectionStrings:Sales"] = "Host=otra;Database=otra;Username=x;Password=x",
            })
            .Build();
        var servicios = new ServiceCollection();
        servicios.AddSingleton(TimeProvider.System);
        new SalesModule().RegisterServices(servicios, configuracion);

        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();
        var opciones = ambito.ServiceProvider.GetRequiredService<DbContextOptions<SalesDbContext>>();
        var npgsql = opciones.Extensions.OfType<NpgsqlOptionsExtension>().Single();

        Assert.Equal(CadenaSinBase, npgsql.ConnectionString);
    }

    // ================================================================== escritores de sales.orders

    [Fact]
    public void El_unico_escritor_productivo_que_inserta_en_sales_orders_es_el_creador_de_pedidos()
    {
        var escritores = ArchivosDeProduccion()
            .Where(f => Regex.IsMatch(File.ReadAllText(f),
                @"\.Orders\.Add(Range)?\(|Set<Order>\(\)\.Add|INSERT\s+INTO\s+sales\.orders|UPDATE\s+sales\.orders|DELETE\s+FROM\s+sales\.orders|\.Orders\b[^;]*\.Execute(Update|Delete)",
                RegexOptions.IgnoreCase))
            .Select(Relativa)
            .Order()
            .ToArray();

        Assert.Equal(["Sillar.Modules.Sales/Pedidos/CreadorDePedidos.cs"], escritores);
    }

    [Fact]
    public void Los_que_guardan_en_el_contexto_de_ventas_son_conocidos_y_solo_el_creador_pone_moneda()
    {
        // Un escritor nuevo hace fallar esta prueba: hay que decidir si congela moneda y,
        // si la congela, ponerlo detrás de la barrera.
        var guardan = ArchivosDeProduccion()
            .Where(f => f.Contains($"Sillar.Modules.Sales{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("SaveChanges", StringComparison.Ordinal))
            .Select(Relativa)
            .Order()
            .ToArray();

        Assert.Equal(
            [
                "Sillar.Modules.Sales/Commercial/CommercialReadOnlyEvaluation.cs", // solo lo nombra para decir que no lo usa
                "Sillar.Modules.Sales/Data/OrderCodeAllocator.cs",                 // ídem: va por SQL directo
                "Sillar.Modules.Sales/Data/SalesDbContext.cs",                     // la sobrecarga con la guarda de origen
                "Sillar.Modules.Sales/Pedidos/CreadorDePedidos.cs",
                "Sillar.Modules.Sales/Pedidos/VencimientoDePlazos.cs",
            ],
            guardan);

        // VencimientoDePlazos cambia el estado de pedidos que ya existen: no fija moneda.
        var vencimiento = File.ReadAllText(Ruta("Sillar.Modules.Sales/Pedidos/VencimientoDePlazos.cs"));
        Assert.DoesNotContain("CurrencyCode", vencimiento, StringComparison.Ordinal);
        Assert.DoesNotContain("Orders.Add", vencimiento, StringComparison.Ordinal);
    }

    [Fact]
    public void En_el_creador_la_barrera_va_antes_de_la_lectura_autentica_del_numero_y_de_la_insercion()
    {
        var fuente = File.ReadAllText(Ruta("Sillar.Modules.Sales/Pedidos/CreadorDePedidos.cs"));

        var transaccion = fuente.IndexOf("TransaccionDeOperacion.AbrirSiHaceFaltaAsync", StringComparison.Ordinal);
        var barrera = fuente.IndexOf("BarreraMonetariaDeVentas.AdquirirCompartidaAsync", StringComparison.Ordinal);
        var autentica = fuente.LastIndexOf("monedas.MonedaDeVentaAsync", StringComparison.Ordinal);
        var numero = fuente.IndexOf("numerador.SiguienteAsync", StringComparison.Ordinal);
        var insercion = fuente.IndexOf("database.Orders.Add", StringComparison.Ordinal);
        var guardado = fuente.IndexOf("database.SaveChangesAsync", StringComparison.Ordinal);

        Assert.True(transaccion > 0 && transaccion < barrera, "la barrera va dentro de la transacción");
        Assert.True(barrera < autentica, "la lectura auténtica va después de la barrera");
        Assert.True(autentica < numero && numero < insercion && insercion < guardado, "numerar e insertar van después");
        Assert.Equal(2, Regex.Matches(fuente, @"monedas\.MonedaDeVentaAsync").Count);
        Assert.Contains("CurrencyCode = moneda", fuente, StringComparison.Ordinal);
    }

    [Fact]
    public void Ningun_modulo_de_ventas_lee_currency_code_ni_currency_symbol_para_fijar_la_moneda()
    {
        var ventas = ArchivosDeProduccion()
            .Where(f => f.Contains($"Sillar.Modules.Sales{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var archivo in ventas)
        {
            // Solo el código: los comentarios pueden nombrarlos para decir que no se leen.
            var texto = string.Join('\n', File.ReadAllLines(archivo)
                .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            Assert.DoesNotContain("\"currency_code\"", texto.Replace("HasColumnName(\"currency_code\")", "", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.DoesNotContain("currency_symbol", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("PendingMarker", texto, StringComparison.Ordinal);
        }
    }

    // ================================================================== montaje

    private static readonly Guid Item = Guid.CreateVersion7();

    private static (BaseGuionadaDeVentas Base, CreadorDePedidos Creador) Montaje(
        string?[] lecturas, Func<string, Exception?>? falla = null)
    {
        var base_ = new BaseGuionadaDeVentas
        {
            Responde = sql => sql.Contains("current_setting('lock_timeout')", StringComparison.Ordinal)
                ? BaseGuionadaDeVentas.Escalar("Value", "0")
                : null,
            FallaAlEjecutar = falla ?? (_ => null),
        };
        var contexto = base_.Contexto();
        var creador = new CreadorDePedidos(
            contexto,
            new CongeladorDeCliente(new Dobles.M04Dice(Dobles.Cliente(Guid.CreateVersion7()))),
            new Dobles.CatalogoDice().Con(Item, activo: true, precio: 8.50m),
            new OrderCodeAllocator(contexto, new Dobles.AjustesDice(), new NodeIdentity("prueba"), TimeProvider.System),
            new Dobles.AjustesDice(),
            new MonedaEnSecuencia(base_, lecturas),
            new Dobles.RelojFijo(Ahora));
        return (base_, creador);
    }

    /// <summary>Responde, por orden, una lectura de la lista cada vez, y anota cuándo.</summary>
    private sealed class MonedaEnSecuencia(BaseGuionadaDeVentas base_, string?[] lecturas) : IAutoridadDeMoneda
    {
        private int siguiente;

        public Task<string?> MonedaDeVentaAsync(CancellationToken cancellationToken)
        {
            base_.Pasos.Add(LecturaDeMoneda);
            return Task.FromResult(lecturas[siguiente++]);
        }
    }

    private sealed class CoreDice(InstallationCurrencyReading? lectura = null, Exception? lanza = null)
        : IInstallationCurrencyAuthority
    {
        public int Lecturas { get; private set; }

        public Task<InstallationCurrencyReading> ReadAsync(CancellationToken cancellationToken)
        {
            Lecturas++;
            return lanza is not null ? Task.FromException<InstallationCurrencyReading>(lanza) : Task.FromResult(lectura!);
        }
    }

    private static AutoridadDeMonedaDeCore Adaptador(IInstallationCurrencyAuthority core)
        => new(core, NullLogger<AutoridadDeMonedaDeCore>.Instance);

    private static SondaDeRegistrosMonetarios Sonda(SalesDbContext contexto)
        => new(contexto, NullLogger<SondaDeRegistrosMonetarios>.Instance);

    private static SalesDbContext ContextoSinBase() => new(
        new DbContextOptionsBuilder<SalesDbContext>().UseNpgsql(CadenaSinBase).Options,
        new NodeIdentity("prueba"),
        TimeProvider.System);

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

    private static IEnumerable<string> ArchivosDeProduccion()
    {
        var separador = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Backend(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separador}obj{separador}", StringComparison.Ordinal)
                && !f.Contains($"{separador}bin{separador}", StringComparison.Ordinal)
                && !f.Contains($".Tests{separador}", StringComparison.Ordinal));
    }

    private static string Relativa(string archivo)
        => Path.GetRelativePath(Backend(), archivo).Replace(Path.DirectorySeparatorChar, '/');

    private static string Ruta(string relativa)
        => Path.Combine(Backend(), relativa.Replace('/', Path.DirectorySeparatorChar));
}
