using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// La evaluación comercial sin efectos: clasifica hechos y nunca autoriza una liquidación.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ninguna toca PostgreSQL.</b> El evaluador, las dos lecturas que compone y sus
/// consultas EF son los reales; lo único sustituido es el transporte.
/// <see cref="BaseComercialSimulada"/> no abre conexión, responde a cada <c>SELECT</c> con
/// filas propias y <b>falla y cuenta</b> cualquier escritura, comando escalar,
/// transacción o <c>SaveChanges</c>.
/// </para>
/// <para>
/// <b>Lo que estas pruebas NO prueban:</b> cómo compara PostgreSQL <c>order_code</c>, ni el
/// aislamiento real entre las dos lecturas. Las dos cosas siguen necesitando base.
/// </para>
/// </remarks>
public sealed class EvaluacionComercialSinEfectosTests
{
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-000000000001");
    private static readonly Guid Otro = Guid.Parse("01999f3a-0000-7000-8000-000000000002");

    private const string Codigo = "P-2026-0147";
    private const string CodigoOtro = "P-2026-0148";

    private const string CadenaSinBase =
        "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";

    private static PedidoSimulado Sano(
        string estado = OrderStatus.PendingPayment,
        decimal total = 100m,
        string? moneda = null,
        bool activo = true)
        => new(Pedido, Codigo, estado, total, moneda, activo);

    private static OrderCorrelationRequest PorCodigo => new(Codigo, null);

    // ---------- A · composición ----------

    [Fact]
    public void A1_Se_construye_sin_registrar_servicios_y_ni_SalesModule_ni_G5B_la_registran()
    {
        var servicios = ServiciosDeM03();
        var antes = servicios.Count;

        Assert.DoesNotContain(servicios, EsDelEvaluador);

        servicios.AddCandidateSalesOrderCorrelation();
        Assert.Equal(antes + 1, servicios.Count);
        Assert.DoesNotContain(servicios, EsDelEvaluador);

        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();

        Assert.Null(ambito.ServiceProvider.GetService<CandidateCommercialReadOnlyEvaluator>());

        // Quien la quiere la construye; el contenedor no sabe de ella.
        var evaluador = ActivatorUtilities.CreateInstance<CandidateCommercialReadOnlyEvaluator>(ambito.ServiceProvider);
        Assert.NotNull(evaluador);
        Assert.Equal(antes + 1, servicios.Count);
    }

    [Fact]
    public void A2_Su_unica_dependencia_es_el_contexto_de_M03_y_no_implementa_ningun_contrato()
    {
        var tipo = typeof(CandidateCommercialReadOnlyEvaluator);

        var constructor = Assert.Single(tipo.GetConstructors());
        var parametro = Assert.Single(constructor.GetParameters());
        Assert.Equal(typeof(SalesDbContext), parametro.ParameterType);

        Assert.Empty(tipo.GetInterfaces());
        Assert.False(tipo.IsPublic);
        Assert.DoesNotContain(
            tipo.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(c => c.GetParameters()),
            p => p.ParameterType == typeof(ISettlementCandidateJournal)
                || p.ParameterType == typeof(IExternalSettlementCandidate)
                || p.ParameterType == typeof(IPayableOrderCandidate));
    }

    [Fact]
    public async Task A3_D3_Con_journal_y_aplicador_en_el_contenedor_no_toca_ninguno()
    {
        var base_ = new BaseComercialSimulada();
        base_.Pedidos.Add(Sano());
        base_.Pagos.Add(new PagoSimulado(Pedido, 30m, PaymentSource.Staff, null));
        var journal = new JournalEspia();
        var aplicador = new AplicadorEspia();

        var servicios = new ServiceCollection();
        servicios.AddSingleton(Contexto(base_));
        servicios.AddSingleton<ISettlementCandidateJournal>(journal);
        servicios.AddSingleton<IExternalSettlementCandidate>(aplicador);
        using var proveedor = servicios.BuildServiceProvider();

        var evaluador = ActivatorUtilities.CreateInstance<CandidateCommercialReadOnlyEvaluator>(proveedor);

        foreach (var solicitud in SolicitudesVariadas())
        {
            await evaluador.EvaluateAsync(solicitud, CancellationToken.None);
        }

        Assert.Equal(0, journal.Busquedas);
        Assert.Equal(0, journal.Recordados);
        Assert.Equal(0, aplicador.Llamadas);
    }

    [Fact]
    public void A4_F2_La_extension_de_G5B_sigue_registrando_solo_la_correlacion()
    {
        var servicios = new ServiceCollection();
        servicios.AddCandidateSalesOrderCorrelation();
        servicios.AddCandidateSalesOrderCorrelation();

        var unico = Assert.Single(servicios);
        Assert.Equal(typeof(IOrderCorrelationCandidate), unico.ServiceType);
        Assert.Equal(typeof(CandidateOrderCorrelationReader), unico.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, unico.Lifetime);
    }

    // ---------- B · correlación ----------

    [Fact]
    public async Task B1_Un_pedido_bien_correlacionado_no_queda_autorizado_para_cobro()
    {
        var (evaluador, base_, _) = Componer(Sano());

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(OrderCorrelationOutcome.Resolved, r.Correlation.Outcome);
        Assert.Equal(CommercialEvaluationVerdict.PendingDecision, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.CurrencyAuthorityMissing, r.Reason);
        Assert.Equal(PayableOutcome.PendingDecision, r.Payability!.Outcome);
        Assert.False(r.AuthorizesSettlement);
        Assert.False(r.ProviderDeclarationAuthenticated);
        Assert.Equal(3, base_.Consultas.Count);
    }

    [Fact]
    public async Task B2_Una_referencia_desconocida_es_negativa_y_no_lee_hechos()
    {
        var (evaluador, base_, _) = Componer(Sano());

        var r = await evaluador.EvaluateAsync(new OrderCorrelationRequest(CodigoOtro, null), CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.Rejected, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.UnknownReference, r.Reason);
        Assert.Null(r.Correlation.OrderId);
        Assert.Null(r.Payability);
        Assert.Null(r.Facts);
        Assert.Single(base_.Consultas);
    }

    [Theory]
    [InlineData("P-2026-147")]
    [InlineData("P20260147")]
    [InlineData("P-2026-014X")]
    public async Task B3_Una_referencia_malformada_no_genera_identidad_ni_consulta(string codigo)
    {
        var (evaluador, base_, _) = Componer(Sano());

        var r = await evaluador.EvaluateAsync(new OrderCorrelationRequest(codigo, null), CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.Rejected, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.MalformedReference, r.Reason);
        Assert.Null(r.Correlation.OrderId);
        Assert.Null(r.Correlation.OrderCode);
        Assert.Equal(CandidateSources.RatifiedRules, r.Correlation.Source);
        Assert.Empty(base_.Consultas);
    }

    [Fact]
    public async Task B3_Guid_vacio_es_malformado_y_no_consulta()
    {
        var (evaluador, base_, _) = Componer(Sano());

        var r = await evaluador.EvaluateAsync(new OrderCorrelationRequest(null, Guid.Empty), CancellationToken.None);

        Assert.Equal(CommercialEvaluationReason.MalformedReference, r.Reason);
        Assert.Empty(base_.Consultas);
    }

    [Fact]
    public async Task B4_Una_declaracion_divergente_no_se_acepta_ni_se_desempata()
    {
        var (evaluador, base_, _) = Componer(Sano(), new PedidoSimulado(Otro, CodigoOtro, OrderStatus.PendingPayment, 50m, null, true));

        var r = await evaluador.EvaluateAsync(new OrderCorrelationRequest(Codigo, Otro), CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.Rejected, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.DivergentDeclaration, r.Reason);
        Assert.Null(r.Correlation.OrderId);
        Assert.Null(r.Facts);
        Assert.Single(base_.Consultas);
    }

    [Fact]
    public async Task B5_Una_referencia_sin_verificar_no_produce_pagabilidad()
    {
        var (evaluador, base_, _) = Componer(Sano());

        var r = await evaluador.EvaluateAsync(
            new OrderCorrelationRequest(null, null) { UnverifiedProviderReference = Codigo },
            CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.Rejected, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.NotAuthoritative, r.Reason);
        Assert.Null(r.Payability);
        Assert.Empty(base_.Consultas);
    }

    // ---------- C · hechos comerciales ----------

    [Fact]
    public async Task C1_Si_el_pedido_desaparece_entre_la_correlacion_y_los_hechos_queda_incoherente()
    {
        var (evaluador, base_, _) = Componer(Sano());
        base_.TrasConsulta = n => { if (n == 1) base_.Pedidos.Clear(); };

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.Inconsistent, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.OrderChangedBetweenReads, r.Reason);
        Assert.Null(r.Payability);
        Assert.Null(r.Facts);
        Assert.Equal(2, base_.Consultas.Count);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, true, "PendingDecision", "CurrencyAuthorityMissing")]
    [InlineData(OrderStatus.PaymentToVerify, true, "PendingDecision", "CurrencyAuthorityMissing")]
    [InlineData(OrderStatus.Preparing, true, "PendingDecision", "CurrencyAuthorityMissing")]
    [InlineData(OrderStatus.ReadyForPickup, true, "PendingDecision", "CurrencyAuthorityMissing")]
    [InlineData(OrderStatus.Expired, true, "PendingDecision", "CurrencyAuthorityMissing")]
    [InlineData(OrderStatus.Delivered, true, "PendingDecision", "OrderLifecycleUnratified")]
    [InlineData(OrderStatus.Cancelled, true, "PendingDecision", "OrderLifecycleUnratified")]
    [InlineData(OrderStatus.PendingPayment, false, "Rejected", "InactiveOrder")]
    public async Task C2_Ningun_estado_del_pedido_lo_vuelve_elegible(
        string estado, bool activo, string veredicto, string motivo)
    {
        var (evaluador, _, _) = Componer(Sano(estado, activo: activo));

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(veredicto, r.Verdict.ToString());
        Assert.Equal(motivo, r.Reason.ToString());
        Assert.NotEqual(PayableOutcome.Eligible, r.Payability!.Outcome);
        Assert.Equal(estado, r.Facts!.Status);
    }

    [Fact]
    public async Task C3_Sin_moneda_guardada_no_se_inventa_ninguna()
    {
        var (evaluador, _, _) = Componer(Sano(moneda: null));

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CurrencyObservation.Absent, r.Facts!.OrderCurrency);
        Assert.Null(r.Facts.OrderCurrencyCode);
        Assert.Null(r.Payability!.Currency);
        Assert.Equal(CommercialEvaluationReason.CurrencyAuthorityMissing, r.Reason);
    }

    [Theory]
    [InlineData("PEN")]
    [InlineData("USD")]
    public async Task C4_Una_moneda_presente_sin_autoridad_ratificada_no_se_acredita(string moneda)
    {
        var (evaluador, _, _) = Componer(Sano(moneda: moneda));

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CurrencyObservation.PresentWithoutRatifiedAuthority, r.Facts!.OrderCurrency);
        Assert.Equal(moneda, r.Facts.OrderCurrencyCode);
        Assert.Null(r.Payability!.Currency);
        Assert.Equal(CommercialEvaluationVerdict.PendingDecision, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.CurrencyAuthorityMissing, r.Reason);
    }

    [Fact]
    public async Task C5_Pagos_del_personal_que_cubren_el_total_no_son_una_liquidacion_de_proveedor()
    {
        var (evaluador, base_, _) = Componer(Sano(total: 100m));
        base_.Pagos.Add(new PagoSimulado(Pedido, 100m, PaymentSource.Staff, null));

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.PendingDecision, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.AlreadySettledManually, r.Reason);
        Assert.Equal(0, r.Facts!.UnratifiedSourceCount);
        Assert.Equal(1, r.Facts.StaffPaymentCount);
    }

    [Fact]
    public async Task C5_Un_pago_con_origen_proveedor_no_ratificado_vuelve_incoherente_la_lectura()
    {
        var (evaluador, base_, _) = Componer(Sano());
        base_.Pagos.Add(new PagoSimulado(Pedido, 10m, PaymentSource.Provider, null));

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.Inconsistent, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.ContradictoryFacts, r.Reason);
        Assert.Null(r.Payability);
        Assert.Equal(1, r.Facts!.UnratifiedSourceCount);
    }

    [Fact]
    public async Task C6_Un_pago_sin_origen_es_historico_y_no_se_cuenta_como_del_personal()
    {
        var (evaluador, base_, _) = Componer(Sano());
        base_.Pagos.Add(new PagoSimulado(Pedido, 40m, null, null));

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(1, r.Facts!.UnattributedHistoricalCount);
        Assert.Equal(40m, r.Facts.UnattributedHistoricalTotal);
        Assert.Equal(0, r.Facts.StaffPaymentCount);
        Assert.Equal(0m, r.Facts.StaffRecordedTotal);
        Assert.Equal(0, r.Facts.UnratifiedSourceCount);
    }

    [Fact]
    public async Task C7_Total_pagos_y_saldo_van_separados_y_el_saldo_no_se_deriva()
    {
        var (evaluador, base_, _) = Componer(Sano(total: 100m, moneda: "PEN"));
        base_.Pagos.Add(new PagoSimulado(Pedido, 30m, PaymentSource.Staff, "PEN"));
        base_.Pagos.Add(new PagoSimulado(Pedido, 20m, null, null));

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(100m, r.Facts!.FrozenTotal);
        Assert.Equal(30m, r.Facts.StaffRecordedTotal);
        Assert.Equal(20m, r.Facts.UnattributedHistoricalTotal);
        Assert.Equal(50m, r.Facts.RecordedPaymentsTotal);
        Assert.Equal(1, r.Facts.PaymentsWithoutCurrencyCount);
        Assert.Null(r.Facts.DerivableBalance);
        Assert.Equal(100m, r.Payability!.FrozenTotal);
        Assert.Equal(50m, r.Payability.ManuallySettledTotal);
    }

    private static readonly (string Caso, PedidoSimulado Pedido, PagoSimulado[] Pagos)[] CasosContradictorios =
    [
        ("estado fuera del vocabulario", Sano(estado: "pagado"), []),
        ("moneda del pedido sin forma ISO", Sano(moneda: "pen"), []),
        ("importe de pago negativo", Sano(), [new(Pedido, -1m, PaymentSource.Staff, null)]),
        ("moneda de pago sin forma ISO", Sano(), [new(Pedido, 10m, PaymentSource.Staff, "S/.")]),
        ("pago en otra moneda que el pedido", Sano(moneda: "PEN"), [new(Pedido, 10m, PaymentSource.Staff, "USD")]),
        (
            "pagos en monedas distintas", Sano(),
            [new(Pedido, 10m, PaymentSource.Staff, "PEN"), new(Pedido, 10m, PaymentSource.Staff, "USD")]),
        ("origen de pago desconocido", Sano(), [new(Pedido, 10m, "pasarela", null)]),
    ];

    public static TheoryData<string, PedidoSimulado, PagoSimulado[]> Contradicciones => Datos(CasosContradictorios);

    [Theory]
    [MemberData(nameof(Contradicciones))]
    public async Task C8_Hechos_contradictorios_quedan_incoherentes_sin_clasificar_la_pagabilidad(
        string caso, PedidoSimulado pedido, PagoSimulado[] pagos)
    {
        var (evaluador, base_, _) = Componer(pedido);
        base_.Pagos.AddRange(pagos);

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.True(r.Verdict == CommercialEvaluationVerdict.Inconsistent, caso);
        Assert.Equal(CommercialEvaluationReason.ContradictoryFacts, r.Reason);
        Assert.Null(r.Payability);
        Assert.NotNull(r.Facts);
    }

    private static readonly (string Caso, PedidoSimulado Pedido, PagoSimulado[] Pagos)[] CasosCoherentes =
    [
        ("sano sin pagos", Sano(), []),
        ("sano con moneda", Sano(moneda: "PEN"), []),
        ("cancelado", Sano(estado: OrderStatus.Cancelled), []),
        ("entregado", Sano(estado: OrderStatus.Delivered), []),
        ("vencido", Sano(estado: OrderStatus.Expired), []),
        ("de baja", Sano(activo: false), []),
        ("total negativo", Sano(total: -5m), []),
        ("parcial del personal", Sano(), [new(Pedido, 30m, PaymentSource.Staff, null)]),
        ("cubierto por el personal", Sano(), [new(Pedido, 100m, PaymentSource.Staff, null)]),
        ("excedente", Sano(), [new(Pedido, 150m, PaymentSource.Staff, null)]),
        ("cubierto por históricos", Sano(), [new(Pedido, 60m, null, null), new(Pedido, 40m, null, null)]),
    ];

    public static TheoryData<string, PedidoSimulado, PagoSimulado[]> HechosCoherentes => Datos(CasosCoherentes);

    private static TheoryData<string, PedidoSimulado, PagoSimulado[]> Datos(
        IEnumerable<(string Caso, PedidoSimulado Pedido, PagoSimulado[] Pagos)> casos)
    {
        var datos = new TheoryData<string, PedidoSimulado, PagoSimulado[]>();
        foreach (var (caso, pedido, pagos) in casos)
        {
            datos.Add(caso, pedido, pagos);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(HechosCoherentes))]
    public async Task F1_La_pagabilidad_coincide_con_la_del_lector_legado_sobre_los_mismos_hechos(
        string caso, PedidoSimulado pedido, PagoSimulado[] pagos)
    {
        var (evaluador, base_, _) = Componer(pedido);
        base_.Pagos.AddRange(pagos);

        var legadoBase = new BaseComercialSimulada();
        legadoBase.Pedidos.Add(pedido);
        legadoBase.Pagos.AddRange(pagos);
        var legado = await new CandidatePayableOrderReader(Contexto(legadoBase))
            .EvaluateAsync(Pedido, CancellationToken.None);

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.True(Equals(legado, r.Payability), caso);
        Assert.NotEqual(PayableOutcome.Eligible, legado.Outcome);
    }

    // ---------- D · ausencia de efectos ----------

    [Fact]
    public async Task D1_D2_D4_Ninguna_evaluacion_guarda_escribe_ni_deja_cambios_rastreados()
    {
        var (evaluador, base_, contexto) = Componer(Sano(moneda: "PEN"),
            new PedidoSimulado(Otro, CodigoOtro, OrderStatus.Cancelled, 50m, null, true));
        base_.Pagos.Add(new PagoSimulado(Pedido, 30m, PaymentSource.Staff, "PEN"));
        base_.Pagos.Add(new PagoSimulado(Otro, 10m, null, null));

        foreach (var solicitud in SolicitudesVariadas())
        {
            await evaluador.EvaluateAsync(solicitud, CancellationToken.None);
        }

        Assert.NotEmpty(base_.Consultas);
        Assert.All(base_.Consultas, sql => Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal));
        Assert.Equal(0, base_.Guardados);
        Assert.Equal(0, base_.ComandosProhibidos);
        Assert.Equal(0, base_.Transacciones);
        Assert.Empty(contexto.ChangeTracker.Entries());
        Assert.False(contexto.ChangeTracker.HasChanges());
    }

    [Fact]
    public void D1_D2_La_instrumentacion_si_detecta_un_guardado_y_un_comando_mutante()
    {
        // Sin esto, «cero escrituras» podría ser solo que el detector no ve nada.
        var base_ = new BaseComercialSimulada();
        using var contexto = Contexto(base_);

        Assert.ThrowsAny<Exception>(() => contexto.Database.ExecuteSqlRaw("UPDATE sales.orders SET is_active = false"));
        Assert.Equal(1, base_.ComandosProhibidos);

        contexto.OrderPayments.Add(new OrderPayment
        {
            Method = PaymentMethod.Cash,
            Source = PaymentSource.Staff,
            RegisteredBy = "Persona de prueba",
            RegisteredByAdminUserHomeNode = "prueba",
        });
        Assert.ThrowsAny<Exception>(() => contexto.SaveChanges());
        Assert.Equal(1, base_.Guardados);

        Assert.ThrowsAny<Exception>(() => contexto.Database.SqlQueryRaw<int>("SELECT nextval('x')").ToList());
        Assert.Equal(2, base_.ComandosProhibidos);
    }

    [Fact]
    public async Task D4_D5_No_modifica_entidades_rastreadas_ni_la_solicitud_entregada()
    {
        var (evaluador, _, contexto) = Componer(Sano());
        var rastreado = new Order
        {
            OrderId = Pedido,
            OrderCode = Codigo,
            CustomerFullName = "Cliente de prueba",
            CustomerEmail = "cliente@ejemplo.test",
            Status = OrderStatus.PendingPayment,
            TotalAmount = 100m,
        };
        contexto.Attach(rastreado);
        var solicitud = new OrderCorrelationRequest(Codigo, Pedido) { UnverifiedProviderReference = "ref" };
        var copia = solicitud with { };

        await evaluador.EvaluateAsync(solicitud, CancellationToken.None);

        var entrada = Assert.Single(contexto.ChangeTracker.Entries());
        Assert.Equal(EntityState.Unchanged, entrada.State);
        Assert.Equal(OrderStatus.PendingPayment, rastreado.Status);
        Assert.Equal(100m, rastreado.TotalAmount);
        Assert.Null(rastreado.CurrencyCode);
        Assert.True(rastreado.IsActive);
        Assert.False(contexto.ChangeTracker.HasChanges());
        Assert.Equal(copia, solicitud);
    }

    [Fact]
    public void D6_No_hay_estado_mutable_ni_estatico_que_sobreviva_entre_evaluaciones()
    {
        const BindingFlags Todos = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        var campos = typeof(CandidateCommercialReadOnlyEvaluator).GetFields(Todos);
        var campo = Assert.Single(campos);
        Assert.False(campo.IsStatic);
        Assert.True(campo.IsInitOnly);
        Assert.Equal(typeof(SalesDbContext), campo.FieldType);

        Assert.DoesNotContain(typeof(CommercialEvaluationPolicy).GetFields(Todos), f => !f.IsLiteral);
    }

    [Fact]
    public async Task D7_Dos_evaluaciones_seguidas_responden_igual_y_releen_sin_acumular()
    {
        var (evaluador, base_, contexto) = Componer(Sano());
        base_.Pagos.Add(new PagoSimulado(Pedido, 30m, PaymentSource.Staff, null));

        var primera = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);
        var trasPrimera = base_.Consultas.Count;
        var segunda = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(primera, segunda);
        Assert.Equal(3, trasPrimera);
        Assert.Equal(6, base_.Consultas.Count);
        Assert.Equal(30m, segunda.Facts!.StaffRecordedTotal);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Fact]
    public async Task D8_Cancelada_antes_de_empezar_no_consulta_ni_escribe()
    {
        var (evaluador, base_, contexto) = Componer(Sano());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => evaluador.EvaluateAsync(PorCodigo, new CancellationToken(canceled: true)));

        Assert.Empty(base_.Consultas);
        Assert.Equal(0, base_.ComandosProhibidos);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Fact]
    public async Task D8_Cancelada_entre_lecturas_conserva_la_cancelacion_y_no_deja_mutaciones()
    {
        var (evaluador, base_, contexto) = Componer(Sano());
        using var fuente = new CancellationTokenSource();
        base_.TrasConsulta = n => { if (n == 1) fuente.Cancel(); };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => evaluador.EvaluateAsync(PorCodigo, fuente.Token));

        Assert.Single(base_.Consultas);
        Assert.Equal(0, base_.ComandosProhibidos);
        Assert.Equal(0, base_.Guardados);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task D8_Un_fallo_de_lectura_se_propaga_y_no_se_vuelve_respuesta_comercial(int consulta)
    {
        var (evaluador, base_, contexto) = Componer(Sano());
        base_.FalloEnConsulta = consulta;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluador.EvaluateAsync(PorCodigo, CancellationToken.None));

        Assert.Contains("base simulada caída", error.Message, StringComparison.Ordinal);
        Assert.Equal(consulta, base_.Consultas.Count);
        Assert.Equal(0, base_.ComandosProhibidos);
        Assert.Equal(0, base_.Guardados);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Fact]
    public async Task D8_Dos_filas_para_una_clave_unica_lanzan_en_vez_de_elegir_una()
    {
        var (evaluador, base_, _) = Componer(Sano(), Sano());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluador.EvaluateAsync(PorCodigo, CancellationToken.None));

        Assert.Equal(0, base_.ComandosProhibidos);
    }

    // ---------- E · resultados y seguridad ----------

    [Fact]
    public void E1_E2_E3_El_vocabulario_no_tiene_ningun_desenlace_positivo()
    {
        Assert.Equal(
            new[] { "Rejected", "PendingDecision", "Inconsistent" },
            Enum.GetNames<CommercialEvaluationVerdict>());

        var prohibidas = new Regex("Applied|Eligible|Durable|Authorized|Settled$|Verified|Persisted", RegexOptions.CultureInvariant);
        Assert.DoesNotContain(Enum.GetNames<CommercialEvaluationReason>(), n => prohibidas.IsMatch(n));
        Assert.DoesNotContain(Enum.GetNames<CurrencyObservation>(), n => n.Contains("Authoritative", StringComparison.Ordinal)
            && !n.Contains("Without", StringComparison.Ordinal));
    }

    [Fact]
    public async Task E1_E2_E3_Ningun_escenario_produce_un_resultado_positivo()
    {
        foreach (var (caso, pedido, pagos) in CasosCoherentes.Concat(CasosContradictorios))
        {
            var (evaluador, base_, _) = Componer(pedido);
            base_.Pagos.AddRange(pagos);

            foreach (var solicitud in SolicitudesVariadas())
            {
                var r = await evaluador.EvaluateAsync(solicitud, CancellationToken.None);

                Assert.True(Enum.IsDefined(r.Verdict), caso);
                Assert.False(r.AuthorizesSettlement, caso);
                Assert.False(r.ProviderDeclarationAuthenticated, caso);
                Assert.NotEqual(PayableOutcome.Eligible, r.Payability?.Outcome);
            }
        }
    }

    [Fact]
    public void E4_El_resultado_no_lleva_identidad_durable_efecto_ni_idempotencia()
    {
        var nombres = typeof(CommercialEvaluationObservation).GetProperties()
            .Concat(typeof(CommercialFactsObservation).GetProperties())
            .Select(p => p.Name)
            .ToArray();

        var prohibidas = new Regex("Durable|Effect|Idempot|Token|Settlement(Id|Reference)|Event|Applied", RegexOptions.CultureInvariant);
        Assert.DoesNotContain(nombres, n => prohibidas.IsMatch(n));

        var tipos = typeof(CommercialEvaluationObservation).GetProperties().Select(p => p.PropertyType).ToArray();
        Assert.DoesNotContain(typeof(ExternalSettlementCandidateResult), tipos);
        Assert.DoesNotContain(typeof(ExternalSettlementOutcome), tipos);
        Assert.DoesNotContain(typeof(SettlementCandidateRecord), tipos);
    }

    [Fact]
    public async Task E5_Si_el_codigo_cambia_entre_lecturas_no_se_elige_cual_creer()
    {
        var (evaluador, base_, _) = Componer(Sano());
        base_.TrasConsulta = n =>
        {
            if (n == 1)
            {
                base_.Pedidos[0] = base_.Pedidos[0] with { OrderCode = CodigoOtro };
            }
        };

        var r = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialEvaluationVerdict.Inconsistent, r.Verdict);
        Assert.Equal(CommercialEvaluationReason.OrderChangedBetweenReads, r.Reason);
        Assert.Null(r.Payability);
    }

    [Fact]
    public async Task E6_Una_evaluacion_anterior_no_sirve_para_otra_posterior_ni_sobrevive_a_un_reinicio()
    {
        var (evaluador, base_, _) = Componer(Sano());

        var antes = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);
        base_.Pedidos[0] = base_.Pedidos[0] with { Status = OrderStatus.Cancelled };
        var despues = await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        // La segunda relee: no hereda nada de la primera.
        Assert.Equal(CommercialEvaluationReason.CurrencyAuthorityMissing, antes.Reason);
        Assert.Equal(CommercialEvaluationReason.OrderLifecycleUnratified, despues.Reason);
        Assert.Equal(6, base_.Consultas.Count);
        Assert.False(antes.AuthorizesSettlement);

        // «Reinicio»: otro contexto, otro evaluador. Responde por los hechos, no por memoria.
        var (reiniciado, baseReiniciada, _) = Componer(base_.Pedidos[0]);
        var tras = await reiniciado.EvaluateAsync(PorCodigo, CancellationToken.None);
        Assert.Equal(despues, tras);
        Assert.Equal(3, baseReiniciada.Consultas.Count);
    }

    [Fact]
    public async Task Traza_lee_identidad_pedido_y_pagos_y_solo_las_columnas_que_usa()
    {
        var (evaluador, base_, _) = Componer(Sano());
        base_.Pagos.Add(new PagoSimulado(Pedido, 30m, PaymentSource.Staff, null));

        await evaluador.EvaluateAsync(PorCodigo, CancellationToken.None);

        for (var i = 0; i < base_.Consultas.Count; i++)
        {
            TestContext.Current.TestOutputHelper?.WriteLine($"-- consulta {i + 1}\n{base_.Consultas[i]}");
        }

        Assert.Equal(3, base_.Consultas.Count);
        Assert.Equal(new[] { "order_id", "order_code" }, BaseComercialSimulada.Columnas(base_.Consultas[0]));
        Assert.Contains("FROM sales.orders", base_.Consultas[0], StringComparison.Ordinal);
        Assert.Equal(
            new[] { "order_id", "order_code", "status", "total_amount", "currency_code", "is_active" },
            BaseComercialSimulada.Columnas(base_.Consultas[1]));
        Assert.Contains("FROM sales.orders", base_.Consultas[1], StringComparison.Ordinal);
        Assert.Equal(new[] { "amount", "source", "currency_code" }, BaseComercialSimulada.Columnas(base_.Consultas[2]));
        Assert.Contains("FROM sales.order_payments", base_.Consultas[2], StringComparison.Ordinal);
        Assert.DoesNotContain(base_.Consultas, sql => sql.Contains("customer_", StringComparison.Ordinal)
            || sql.Contains("registered_by", StringComparison.Ordinal)
            || sql.Contains("reference", StringComparison.Ordinal));
    }

    // ---------- montaje ----------

    private static bool EsDelEvaluador(ServiceDescriptor d)
        => d.ServiceType == typeof(CandidateCommercialReadOnlyEvaluator)
            || d.ImplementationType == typeof(CandidateCommercialReadOnlyEvaluator);

    private static IEnumerable<OrderCorrelationRequest> SolicitudesVariadas() =>
    [
        new(Codigo, null),
        new(null, Pedido),
        new(Codigo, Pedido),
        new(CodigoOtro, null),
        new(Codigo, Otro),
        new("p-2026-0147", null),
        new("malformado", null),
        new(null, Guid.Empty),
        new(null, null) { UnverifiedProviderReference = Codigo },
    ];

    /// <summary>Lo que <c>SalesModule</c> registra en producción, sin pedir nada más.</summary>
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

    private static (CandidateCommercialReadOnlyEvaluator Evaluador, BaseComercialSimulada Base, SalesDbContext Contexto) Componer(
        params PedidoSimulado[] pedidos)
    {
        var base_ = new BaseComercialSimulada();
        base_.Pedidos.AddRange(pedidos);
        var contexto = Contexto(base_);
        return (new CandidateCommercialReadOnlyEvaluator(contexto), base_, contexto);
    }

    private static SalesDbContext Contexto(BaseComercialSimulada interceptor)
    {
        var opciones = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(CadenaSinBase)
            .AddInterceptors(interceptor);

        return new SalesDbContext(opciones.Options, new NodeIdentity("prueba"), TimeProvider.System);
    }

    /// <summary>Un pedido tal como lo vería la consulta.</summary>
    public sealed record PedidoSimulado(
        Guid OrderId, string OrderCode, string Status, decimal TotalAmount, string? CurrencyCode, bool IsActive);

    /// <summary>Un pago tal como lo vería la consulta, con su pedido.</summary>
    public sealed record PagoSimulado(Guid OrderId, decimal Amount, string? Source, string? CurrencyCode);

    private sealed class JournalEspia : ISettlementCandidateJournal
    {
        public int Busquedas { get; private set; }

        public int Recordados { get; private set; }

        public SettlementCandidateRecord? Find(string provider, string providerEventId)
        {
            Busquedas++;
            return null;
        }

        public void Remember(SettlementCandidateRecord record) => Recordados++;
    }

    private sealed class AplicadorEspia : IExternalSettlementCandidate
    {
        public int Llamadas { get; private set; }

        public Task<ExternalSettlementCandidateResult> TryApplyAsync(
            ExternalSettlementCandidateEvent candidate, CancellationToken cancellationToken)
        {
            Llamadas++;
            throw new InvalidOperationException("La evaluación no debe proponer liquidaciones.");
        }
    }

    /// <summary>
    /// El transporte sustituido. No abre conexión; responde a cada <c>SELECT</c> con las
    /// columnas que pide, en su orden, filtrando por el parámetro con igualdad
    /// <b>ordinal</b>. Todo lo demás —SQL mutante, comandos escalares, transacciones y
    /// <c>SaveChanges</c>— se cuenta y se rechaza.
    /// </summary>
    private sealed class BaseComercialSimulada
        : DbConnectionInterceptor, IDbCommandInterceptor, ISaveChangesInterceptor, IDbTransactionInterceptor
    {
        private static readonly Regex Mutante = new(
            @"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|REVOKE|COPY|CALL|DO|LOCK|VACUUM)\b"
                + @"|\bnextval\b|\bsetval\b|pg_advisory|\bFOR\s+(UPDATE|SHARE|NO\s+KEY|KEY)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, Type> Tipos = new(StringComparer.Ordinal)
        {
            ["order_id"] = typeof(Guid),
            ["order_code"] = typeof(string),
            ["status"] = typeof(string),
            ["total_amount"] = typeof(decimal),
            ["currency_code"] = typeof(string),
            ["is_active"] = typeof(bool),
            ["amount"] = typeof(decimal),
            ["source"] = typeof(string),
        };

        public List<PedidoSimulado> Pedidos { get; } = [];

        public List<PagoSimulado> Pagos { get; } = [];

        public List<string> Consultas { get; } = [];

        public int ComandosProhibidos { get; private set; }

        public int Guardados { get; private set; }

        public int Transacciones { get; private set; }

        /// <summary>Se llama tras responder la consulta n-ésima: simula un cambio entre lecturas.</summary>
        public Action<int>? TrasConsulta { get; set; }

        /// <summary>La consulta n-ésima falla como fallaría una base caída.</summary>
        public int? FalloEnConsulta { get; set; }

        /// <summary>Las columnas del <c>SELECT</c>, sin alias de tabla.</summary>
        public static string[] Columnas(string sql)
        {
            var inicio = sql.IndexOf("SELECT", StringComparison.Ordinal) + "SELECT".Length;
            var fin = sql.IndexOf("FROM", inicio, StringComparison.Ordinal);

            // El agregado del lector legado: una sola columna, la suma.
            if (sql[inicio..fin].Contains("sum(", StringComparison.OrdinalIgnoreCase))
            {
                return ["sum"];
            }

            return sql[inicio..fin]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Split(" AS ", StringSplitOptions.TrimEntries)[0])
                .Select(c => c[(c.LastIndexOf('.') + 1)..])
                .ToArray();
        }

        public override InterceptionResult ConnectionOpening(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public override InterceptionResult ConnectionClosing(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionClosingAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(Responder(command)));

        public InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            => InterceptionResult<DbDataReader>.SuppressWithResult(Responder(command));

        public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => throw Prohibido(command);

        public InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
            => throw Prohibido(command);

        public ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
            => throw Prohibido(command);

        public InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
            => throw Prohibido(command);

        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Guardados++;
            throw new InvalidOperationException("SaveChanges inesperado.");
        }

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Guardados++;
            throw new InvalidOperationException("SaveChanges inesperado.");
        }

        public InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result)
        {
            Transacciones++;
            throw new InvalidOperationException("Transacción inesperada.");
        }

        public ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            Transacciones++;
            throw new InvalidOperationException("Transacción inesperada.");
        }

        private InvalidOperationException Prohibido(DbCommand command)
        {
            ComandosProhibidos++;
            return new InvalidOperationException($"Comando no permitido: {command.CommandText}");
        }

        private DbDataReader Responder(DbCommand command)
        {
            var sql = command.CommandText;

            if (!sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal) || Mutante.IsMatch(sql))
            {
                throw Prohibido(command);
            }

            Consultas.Add(sql);

            if (FalloEnConsulta == Consultas.Count)
            {
                throw new InvalidOperationException("base simulada caída");
            }

            var valor = Assert.Single(command.Parameters.Cast<DbParameter>()).Value;
            var columnas = Columnas(sql);
            var tabla = new DataTable();

            if (sql.Contains("FROM sales.order_payments", StringComparison.Ordinal))
            {
                var pagos = Pagos.Where(p => valor is Guid id && p.OrderId == id).ToArray();

                if (columnas is ["sum"])
                {
                    tabla.Columns.Add("sum", typeof(decimal));
                    // Sin filas, SUM da NULL; con COALESCE alrededor, cero. Se imita lo que pida el SQL.
                    tabla.Rows.Add(pagos.Length > 0 ? pagos.Sum(p => p.Amount)
                        : sql.Contains("COALESCE", StringComparison.OrdinalIgnoreCase) ? 0m : DBNull.Value);
                }
                else
                {
                    Llenar(tabla, columnas, pagos.Select(p => new Dictionary<string, object?>
                    {
                        ["amount"] = p.Amount,
                        ["source"] = p.Source,
                        ["currency_code"] = p.CurrencyCode,
                    }));
                }
            }
            else if (sql.Contains("FROM sales.orders", StringComparison.Ordinal))
            {
                var pedidos = Pedidos.Where(p => valor switch
                {
                    string codigo => string.Equals(p.OrderCode, codigo, StringComparison.Ordinal),
                    Guid id => p.OrderId == id,
                    _ => false,
                });

                Llenar(tabla, columnas, pedidos.Select(p => new Dictionary<string, object?>
                {
                    ["order_id"] = p.OrderId,
                    ["order_code"] = p.OrderCode,
                    ["status"] = p.Status,
                    ["total_amount"] = p.TotalAmount,
                    ["currency_code"] = p.CurrencyCode,
                    ["is_active"] = p.IsActive,
                }));
            }
            else
            {
                throw Prohibido(command);
            }

            var lector = tabla.CreateDataReader();
            TrasConsulta?.Invoke(Consultas.Count);
            return lector;
        }

        private static void Llenar(DataTable tabla, string[] columnas, IEnumerable<Dictionary<string, object?>> filas)
        {
            foreach (var columna in columnas)
            {
                tabla.Columns.Add(columna, Tipos[columna]);
            }

            foreach (var fila in filas)
            {
                tabla.Rows.Add(columnas.Select(c => fila[c] ?? DBNull.Value).ToArray());
            }
        }
    }
}
