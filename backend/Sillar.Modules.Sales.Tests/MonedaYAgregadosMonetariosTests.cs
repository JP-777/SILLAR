using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
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

    private static readonly OrderPaymentAggregateCapabilities SinEscritorProveedor =
        AgregadosMonetarios.Capacidades;

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
        Assert.Contains("GROUP BY o.source, o.currency_code, o.was_late", sql, StringComparison.Ordinal);
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

    // ------------------------------------------------------------------ pagos

    [Fact]
    public void Hoy_no_hay_escritor_de_pagos_de_proveedor()
        => Assert.False(AgregadosMonetarios.Capacidades.ProviderWriterPresent);

    [Fact]
    public void Sin_escritor_los_pagos_de_proveedor_son_no_medibles_y_no_cero()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado, [], SinEscritorProveedor);

        Assert.Equal(MeasureState.NotMeasurable, foto.Provider.State);
        Assert.Null(foto.Provider.Count);
        Assert.Equal(NotMeasurableReason.ProviderWriterAbsent, foto.Provider.Reason);
    }

    [Fact]
    public void Una_cohorte_de_pagos_vacia_es_cero_comprobado_para_el_personal_y_los_tardios()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado, [], SinEscritorProveedor);

        Assert.Equal(PaymentCountMeasure.Measured(0), foto.Staff);
        Assert.Equal(PaymentCountMeasure.Measured(0), foto.Late);
        Assert.Empty(foto.AmountsByCurrency);
        Assert.Equal(0, foto.PaymentsWithoutCurrency);
        Assert.Equal(MeasureState.Measured, foto.Amounts.State);
    }

    [Fact]
    public void Los_tardios_salen_del_hecho_guardado()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
        [
            new GrupoDePagos(PaymentSource.Staff, "PEN", true, 2, 20m),
            new GrupoDePagos(PaymentSource.Staff, "PEN", false, 5, 50m),
            new GrupoDePagos(PaymentSource.Staff, null, true, 1, 10m),
        ], SinEscritorProveedor);

        Assert.Equal(PaymentCountMeasure.Measured(8), foto.Staff);
        Assert.Equal(PaymentCountMeasure.Measured(3), foto.Late);
    }

    [Fact]
    public void Un_solo_pago_sin_moneda_deja_el_importe_cobrado_como_no_medible_sin_borrar_los_grupos()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
        [
            new GrupoDePagos(PaymentSource.Staff, "PEN", false, 2, 70m),
            new GrupoDePagos(PaymentSource.Staff, null, false, 1, 999m),
        ], SinEscritorProveedor);

        Assert.Equal([new PaymentAmountByCurrency("PEN", 2, 70m)], foto.AmountsByCurrency);
        Assert.Equal(1, foto.PaymentsWithoutCurrency);
        Assert.Equal(
            new PaymentAmountsCoverage(MeasureState.NotMeasurable, NotMeasurableReason.CurrencyNotRecorded),
            foto.Amounts);
    }

    [Fact]
    public void Si_todos_los_pagos_tienen_moneda_el_importe_es_medido_por_moneda()
    {
        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
        [
            new GrupoDePagos(PaymentSource.Staff, "USD", false, 1, 10m),
            new GrupoDePagos(PaymentSource.Staff, "PEN", true, 1, 5m),
            new GrupoDePagos(PaymentSource.Staff, "PEN", false, 2, 15m),
        ], SinEscritorProveedor);

        Assert.Equal(
            [new PaymentAmountByCurrency("PEN", 3, 20m), new PaymentAmountByCurrency("USD", 1, 10m)],
            foto.AmountsByCurrency);
        Assert.Equal(new PaymentAmountsCoverage(MeasureState.Measured, NotMeasurableReason.None), foto.Amounts);
    }

    [Fact]
    public void Con_escritor_de_proveedor_su_recuento_pasaria_a_ser_medible()
    {
        // Capacidad hipotética construida en la prueba: no hay escritor, solo se fija
        // que la rama medible existe y no confunde orígenes.
        var conEscritor = SinEscritorProveedor with { ProviderWriterPresent = true };

        var foto = AgregadosMonetarios.ReducirPagos(Inicio, Fin, Observado,
            [new GrupoDePagos(PaymentSource.Staff, "PEN", false, 2, 20m)], conEscritor);

        Assert.Equal(PaymentCountMeasure.Measured(0), foto.Provider);
        Assert.Equal(PaymentCountMeasure.Measured(2), foto.Staff);
    }

    // ------------------------------------------------------------------ apoyo

    private static SalesDbContext Contexto() => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1")
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
