using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// Moneda explícita por pedido y pago, y los dos agregados monetarios para M10.
/// </summary>
/// <remarks>
/// <b>Ninguna toca la base.</b> El contexto se construye con una cadena a un puerto
/// cerrado y nunca se abre: el modelo, el SQL generado y las reglas de reducción se leen
/// sin conexión. Lo que solo PostgreSQL puede probar —que el CHECK rechace de verdad—
/// queda para cuando la migración candidata esté autorizada.
/// </remarks>
public sealed class MonedaYAgregadosMonetariosTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fin = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Observado = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyCollection<AcreditacionDeEscritor> SinAcreditar =
        AgregadosMonetarios.Acreditaciones;

    // ------------------------------------------------------------------ forma ISO

    [Theory]
    [InlineData("PEN")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Un_codigo_de_tres_mayusculas_tiene_forma_ISO(string codigo)
        => Assert.True(CurrencyCode.TieneFormaIso(codigo));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pen")]
    [InlineData("Pen")]
    [InlineData("PE")]
    [InlineData("PENN")]
    [InlineData("P3N")]
    [InlineData(" PE")]
    [InlineData("ÑAN")]
    public void La_forma_ISO_no_se_corrige_ni_se_supone(string? codigo)
        => Assert.False(CurrencyCode.TieneFormaIso(codigo));

    [Fact]
    public void Un_pedido_nuevo_nace_sin_moneda_y_no_con_PEN()
    {
        var pedido = new Order
        {
            OrderCode = "P-2026-0001", CustomerFullName = "x", CustomerEmail = "x@x", Status = OrderStatus.PendingPayment
        };

        Assert.Null(pedido.CurrencyCode);
    }

    // ------------------------------------------------------------------ modelo

    [Fact]
    public void La_moneda_del_pedido_es_anulable_de_tres_caracteres_en_colacion_C_y_sin_valor_por_defecto()
    {
        var moneda = Propiedad<Order>(nameof(Order.CurrencyCode));

        Assert.True(moneda.IsNullable);
        Assert.Equal("character(3)", moneda.GetColumnType());
        Assert.Equal("C", moneda.GetCollation());
        Assert.Null(moneda.GetDefaultValue());
        Assert.Null(moneda.GetDefaultValueSql());
    }

    [Fact]
    public void La_moneda_del_pago_es_anulable_y_sin_valor_por_defecto()
    {
        var moneda = Propiedad<OrderPayment>(nameof(OrderPayment.CurrencyCode));

        Assert.True(moneda.IsNullable);
        Assert.Equal("character(3)", moneda.GetColumnType());
        Assert.Null(moneda.GetDefaultValue());
        Assert.Null(moneda.GetDefaultValueSql());
    }

    [Fact]
    public void El_CHECK_de_moneda_usa_la_misma_forma_que_el_dominio_en_pedidos_y_pagos()
    {
        Assert.Equal($"currency_code ~ '{CurrencyCode.CheckPattern}'", Check<Order>("ck_orders_currency_code_iso"));
        Assert.Equal(
            $"currency_code ~ '{CurrencyCode.CheckPattern}'",
            Check<OrderPayment>("ck_order_payments_currency_code_iso"));
    }

    [Fact]
    public void El_origen_del_pago_es_obligatorio_sin_valor_por_defecto_y_distinto_del_medio()
    {
        var origen = Propiedad<OrderPayment>(nameof(OrderPayment.Source));

        Assert.False(origen.IsNullable);
        Assert.Equal("source", origen.GetColumnName());
        Assert.Null(origen.GetDefaultValue());
        Assert.NotEqual(origen.GetColumnName(), Propiedad<OrderPayment>(nameof(OrderPayment.Method)).GetColumnName());
    }

    [Fact]
    public void El_CHECK_de_origen_admite_solo_al_personal_y_no_al_proveedor()
    {
        var check = Check<OrderPayment>("ck_order_payments_source");

        Assert.Equal("source IN ('staff')", check);
        Assert.DoesNotContain(PaymentSource.Provider, check, StringComparison.Ordinal);
    }

    [Fact]
    public void La_atribucion_humana_del_pago_sigue_siendo_obligatoria()
    {
        // El origen no abre la puerta a un pago sin persona: hasta que haya proveedor,
        // cada pago lleva sus tres datos y ninguno es «Sistema».
        Assert.False(Propiedad<OrderPayment>(nameof(OrderPayment.RegisteredBy)).IsNullable);
        Assert.False(Propiedad<OrderPayment>(nameof(OrderPayment.RegisteredByAdminUserLocalId)).IsNullable);
        Assert.False(Propiedad<OrderPayment>(nameof(OrderPayment.RegisteredByAdminUserHomeNode)).IsNullable);
    }

    [Fact]
    public void Borrar_fisicamente_un_pedido_no_se_lleva_sus_pagos()
    {
        var fk = Modelo().FindEntityType(typeof(OrderPayment))!.GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(Order));

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    // ------------------------------------------------------------------ período

    [Fact]
    public void Un_periodo_cuyo_fin_no_es_posterior_al_inicio_se_rechaza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AgregadosMonetarios.Periodo(Inicio, Inicio));
        Assert.Throws<ArgumentOutOfRangeException>(() => AgregadosMonetarios.Periodo(Fin, Inicio));
    }

    [Fact]
    public void El_periodo_se_lleva_a_UTC_sin_mover_el_instante()
    {
        var lima = TimeSpan.FromHours(-5);
        var (inicio, fin) = AgregadosMonetarios.Periodo(
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, lima),
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, lima));

        Assert.Equal(TimeSpan.Zero, inicio.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero), inicio);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero), fin);
    }

    [Fact]
    public async Task Un_periodo_invertido_se_rechaza_antes_de_ir_a_la_base()
    {
        // La cadena apunta a un puerto cerrado: si el lector intentara leer, el fallo
        // sería de conexión y no de argumento.
        await using var contexto = Contexto();
        var valor = new LectorDeValorDePedidos(contexto, TimeProvider.System);
        var pagos = new LectorAgregadoDePagos(contexto, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => valor.ReadCreatedBetweenAsync(Fin, Inicio, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => pagos.ReadRegisteredBetweenAsync(Fin, Inicio, CancellationToken.None));
    }

    [Fact]
    public void La_consulta_de_valor_es_semiabierta_por_created_at_y_no_esconde_las_bajas()
    {
        using var contexto = Contexto();
        var sql = new LectorDeValorDePedidos(contexto, TimeProvider.System).Consulta(Inicio, Fin).ToQueryString();

        Assert.Contains("o.created_at >= ", sql, StringComparison.Ordinal);
        Assert.Contains("o.created_at < ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("<= ", sql, StringComparison.Ordinal);
        Assert.Contains("GROUP BY o.currency_code, o.is_active", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE o.is_active", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("AND o.is_active", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void La_consulta_de_pagos_es_semiabierta_por_registered_at()
    {
        using var contexto = Contexto();
        var sql = new LectorAgregadoDePagos(contexto, TimeProvider.System).Consulta(Inicio, Fin).ToQueryString();

        Assert.Contains("o.registered_at >= ", sql, StringComparison.Ordinal);
        Assert.Contains("o.registered_at < ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("created_at", sql, StringComparison.Ordinal);
        Assert.Contains("GROUP BY o.source, o.method, o.currency_code, o.was_late", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ valor de pedidos

    [Fact]
    public void Una_cohorte_vacia_es_cero_comprobado_y_sin_grupos()
    {
        var foto = AgregadosMonetarios.ReducirValor(Inicio, Fin, Observado, []);

        Assert.Equal(0, foto.CohortCount);
        Assert.Empty(foto.ByCurrency);
        Assert.Equal(new OrdersWithoutCurrency(0, 0), foto.WithoutCurrency);
        Assert.Equal((Inicio, Fin, Observado), (foto.StartInclusive, foto.EndExclusive, foto.ObservedAtUtc));
    }

    [Fact]
    public void Una_cohorte_solo_de_historicos_no_publica_ningun_importe()
    {
        var foto = AgregadosMonetarios.ReducirValor(Inicio, Fin, Observado,
        [
            new GrupoDeValor(null, true, 4, 400m),
            new GrupoDeValor(null, false, 1, 50m),
        ]);

        Assert.Equal(5, foto.CohortCount);
        Assert.Empty(foto.ByCurrency);
        Assert.Equal(new OrdersWithoutCurrency(4, 1), foto.WithoutCurrency);
    }

    [Fact]
    public void Las_monedas_no_se_suman_entre_si_y_activos_e_inactivos_van_aparte()
    {
        var foto = AgregadosMonetarios.ReducirValor(Inicio, Fin, Observado,
        [
            new GrupoDeValor("USD", true, 1, 30m),
            new GrupoDeValor("PEN", true, 2, 200m),
            new GrupoDeValor("PEN", false, 1, 15.50m),
            new GrupoDeValor(null, true, 3, 999m),
        ]);

        Assert.Equal(
            [
                new OrderValueByCurrency("PEN", 2, 200m, 1, 15.50m),
                new OrderValueByCurrency("USD", 1, 30m, 0, 0m),
            ],
            foto.ByCurrency);
        Assert.Equal(new OrdersWithoutCurrency(3, 0), foto.WithoutCurrency);
    }

    [Fact]
    public void El_recuento_de_la_cohorte_cuadra_con_grupos_y_sin_moneda()
    {
        var foto = AgregadosMonetarios.ReducirValor(Inicio, Fin, Observado,
        [
            new GrupoDeValor("PEN", true, 2, 1m),
            new GrupoDeValor("PEN", false, 3, 1m),
            new GrupoDeValor("USD", false, 5, 1m),
            new GrupoDeValor(null, true, 7, 1m),
            new GrupoDeValor(null, false, 11, 1m),
        ]);

        var enGrupos = foto.ByCurrency.Sum(g => g.ActiveCount + g.InactiveCount);
        Assert.Equal(28, foto.CohortCount);
        Assert.Equal(foto.CohortCount, enGrupos + foto.WithoutCurrency.Total);
    }

    // ------------------------------------------------------------------ guarda de moneda

    [Fact]
    public void Sin_autoridad_de_moneda_no_hay_moneda_de_venta()
        => Assert.Null(new SinAutoridadDeMoneda().MonedaDeVenta());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pen")]
    [InlineData("PE")]
    public async Task Sin_moneda_afirmada_no_nace_ningun_pedido_y_no_se_llega_a_la_base(string? moneda)
    {
        // El contexto apunta a un puerto cerrado: si la guarda no cortara, el pedido
        // fallaría por conexión, no con este motivo.
        await using var contexto = Contexto();
        var item = Guid.CreateVersion7();
        var creador = new CreadorDePedidos(
            contexto,
            new CongeladorDeCliente(new Dobles.M04Dice(Dobles.Cliente(Guid.CreateVersion7()))),
            new Dobles.CatalogoDice().Con(item, activo: true, precio: 8.50m),
            new OrderCodeAllocator(contexto, new Dobles.AjustesDice(), new NodeIdentity("prueba"), TimeProvider.System),
            new Dobles.AjustesDice(),
            new Dobles.MonedaDice(moneda),
            TimeProvider.System);

        var r = await creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(item, 1)], CancellationToken.None);

        Assert.False(r.Creado);
        Assert.Equal(MotivoDeNoCreacion.MonedaSinAutoridad, r.Motivo);
        Assert.Null(r.OrderCode);
    }

    // ------------------------------------------------------------------ pagos

    [Fact]
    public void Hoy_ningun_origen_tiene_escritor_acreditado()
    {
        Assert.Equal(PaymentSource.All, AgregadosMonetarios.Acreditaciones.Select(a => a.Origen));
        Assert.All(AgregadosMonetarios.Acreditaciones, a => Assert.Null(a.CubiertoDesdeUtc));
    }

    [Fact]
    public void Sin_escritor_acreditado_ningun_indicador_de_ningun_origen_es_medible_aunque_haya_filas()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
            [new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 3, 30m)], SinAcreditar);

        Assert.Equal(PaymentSource.All.Length * Enum.GetValues<PaymentIndicator>().Length, foto.Coverage.Count);
        Assert.All(foto.Coverage, c =>
        {
            Assert.Equal(MeasureState.NotMeasurable, c.State);
            Assert.Equal(NotMeasurableReason.WriterNotAccredited, c.Reason);
            Assert.Null(c.CoveredSinceUtc);
        });
    }

    [Fact]
    public void Una_ventana_vacia_sin_escritor_no_se_declara_cero()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado, [], SinAcreditar);

        Assert.Empty(foto.Groups);
        Assert.DoesNotContain(foto.Coverage, c => c.State == MeasureState.Measured);
    }

    [Fact]
    public void Los_grupos_separan_origen_medio_moneda_y_tardio()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
        [
            new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, "USD", false, 1, 10m),
            new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Cash, "PEN", false, 2, 20m),
            new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, "PEN", true, 1, 5m),
            new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 4, 40m),
            new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, null, false, 1, 999m),
        ], SinAcreditar);

        Assert.Equal(
            [
                new PaymentAggregateGroup("staff", "efectivo", "PEN", false, 2, 20m),
                new PaymentAggregateGroup("staff", "yape", "PEN", false, 4, 40m),
                new PaymentAggregateGroup("staff", "yape", "PEN", true, 1, 5m),
                new PaymentAggregateGroup("staff", "yape", "USD", false, 1, 10m),
                new PaymentAggregateGroup("staff", "yape", null, false, 1, null),
            ],
            foto.Groups);
    }

    [Fact]
    public void Un_grupo_sin_moneda_cuenta_pagos_pero_no_publica_importe()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
            [new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Cash, null, true, 2, 70m)], SinAcreditar);

        var grupo = Assert.Single(foto.Groups);
        Assert.Equal(2, grupo.Count);
        Assert.True(grupo.WasLate);
        Assert.Null(grupo.Amount);
    }

    [Fact]
    public void Con_escritor_acreditado_desde_antes_de_la_ventana_el_origen_es_medible()
    {
        // Acreditación hipotética construida en la prueba: fija la rama, no afirma escritor.
        var acreditado = Acreditar(PaymentSource.Staff, Inicio.AddDays(-1));

        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
            [new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 1, 10m)], acreditado);

        Assert.All(Cobertura(foto, PaymentSource.Staff), c =>
        {
            Assert.Equal(MeasureState.Measured, c.State);
            Assert.Equal(Inicio.AddDays(-1), c.CoveredSinceUtc);
        });
        Assert.All(Cobertura(foto, PaymentSource.Provider), c =>
            Assert.Equal(NotMeasurableReason.WriterNotAccredited, c.Reason));
    }

    [Fact]
    public void Un_escritor_acreditado_a_mitad_de_ventana_no_cubre_la_ventana()
    {
        var acreditado = Acreditar(PaymentSource.Staff, Inicio.AddDays(3));

        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado, [], acreditado);

        Assert.All(Cobertura(foto, PaymentSource.Staff), c =>
            Assert.Equal(NotMeasurableReason.WindowStartsBeforeCoverage, c.Reason));
    }

    [Fact]
    public void Un_solo_pago_sin_moneda_deja_no_medible_el_importe_de_su_origen_y_no_el_recuento()
    {
        var acreditado = Acreditar(PaymentSource.Staff, Inicio);

        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
        [
            new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 2, 20m),
            new GrupoDePagos(PaymentSource.Staff, PaymentMethod.Yape, null, false, 1, 9m),
        ], acreditado);

        var staff = Cobertura(foto, PaymentSource.Staff).ToDictionary(c => c.Indicator);
        Assert.Equal(MeasureState.Measured, staff[PaymentIndicator.Count].State);
        Assert.Equal(MeasureState.Measured, staff[PaymentIndicator.LateCount].State);
        Assert.Equal(NotMeasurableReason.CurrencyNotRecorded, staff[PaymentIndicator.AmountByCurrency].Reason);
    }

    // ------------------------------------------------------------------ apoyo

    private static AcreditacionDeEscritor[] Acreditar(string origen, DateTimeOffset desde)
        => PaymentSource.All
            .Select(o => new AcreditacionDeEscritor(o, o == origen ? desde : null))
            .ToArray();

    private static IEnumerable<PaymentCoverage> Cobertura(OrderPaymentAggregateSnapshot foto, string origen)
        => foto.Coverage.Where(c => c.Source == origen);

    private const string CadenaSinBase =
        "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";

    private static SalesDbContext Contexto() => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(CadenaSinBase)
            .Options,
        new NodeIdentity("prueba"),
        TimeProvider.System);

    private static IModel Modelo()
    {
        using var contexto = Contexto();
        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private static IProperty Propiedad<T>(string nombre)
        => Modelo().FindEntityType(typeof(T))!.FindProperty(nombre)!;

    private static string Check<T>(string nombre)
        => Modelo().FindEntityType(typeof(T))!.GetCheckConstraints().Single(c => c.Name == nombre).Sql;
}
