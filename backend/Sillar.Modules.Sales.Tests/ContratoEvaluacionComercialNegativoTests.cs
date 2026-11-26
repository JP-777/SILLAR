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
using PagoSimulado = Sillar.Modules.Sales.Tests.EvaluacionComercialSinEfectosTests.PagoSimulado;
using PedidoSimulado = Sillar.Modules.Sales.Tests.EvaluacionComercialSinEfectosTests.PedidoSimulado;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// La frontera pública negativa de la evaluación comercial y su adaptador interno.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ninguna toca PostgreSQL.</b> El recorrido es el real: contrato de
/// <c>Sales.Contracts</c> → adaptador → <see cref="CandidateCommercialReadOnlyEvaluator"/>
/// → lector real de correlación y política real de pagabilidad → consultas EF reales.
/// Lo único sustituido es el transporte, con la misma instrumentación que G6-1A.
/// </para>
/// <para>
/// <b>Lo que estas pruebas NO prueban:</b> cómo compara PostgreSQL <c>order_code</c>, el
/// <c>SUM</c> real ni el aislamiento entre lecturas.
/// </para>
/// </remarks>
public sealed class ContratoEvaluacionComercialNegativoTests
{
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-000000000001");
    private static readonly Guid Otro = Guid.Parse("01999f3a-0000-7000-8000-000000000002");

    private const string Codigo = "P-2026-0147";
    private const string CodigoOtro = "P-2026-0148";

    private const string CadenaSinBase =
        "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";

    /// <summary>Los tipos públicos que añade este incremento.</summary>
    private static readonly Type[] TiposPublicos =
    [
        typeof(ICommercialReadOnlyEvaluationCandidate),
        typeof(CommercialReadOnlyEvaluationVerdict),
        typeof(CommercialReadOnlyEvaluationReason),
        typeof(CommercialReadOnlyCurrencyObservation),
        typeof(CommercialReadOnlyOrderFacts),
        typeof(CommercialReadOnlyEvaluationResult),
    ];

    /// <summary>Lo que esta composición no debe arrastrar nunca.</summary>
    private static readonly Type[] Prohibidos =
    [
        typeof(IPayableOrderCandidate),
        typeof(IExternalSettlementCandidate),
        typeof(ISettlementCandidateJournal),
        typeof(IOrderCorrelationCandidate),
        typeof(CandidatePayableOrderReader),
        typeof(CandidateExternalSettlementApplicator),
        typeof(InProcessSettlementCandidateJournal),
        typeof(CandidateCommercialReadOnlyEvaluator),
    ];

    private static PedidoSimulado Sano(
        string estado = OrderStatus.PendingPayment,
        decimal total = 100m,
        string? moneda = null,
        bool activo = true)
        => new(Pedido, Codigo, estado, total, moneda, activo);

    private static OrderCorrelationRequest PorCodigo => new(Codigo, null);

    // ---------- A · contrato ----------

    [Fact]
    public void A1_A2_La_interfaz_es_publica_vive_en_Sales_Contracts_y_reutiliza_OrderCorrelationRequest()
    {
        var tipo = typeof(ICommercialReadOnlyEvaluationCandidate);

        Assert.True(tipo.IsPublic);
        Assert.True(tipo.IsInterface);
        Assert.Equal(typeof(OrderCorrelationRequest).Assembly, tipo.Assembly);
        Assert.Equal("Sillar.Modules.Sales.Contracts", tipo.Assembly.GetName().Name);

        var metodo = Assert.Single(tipo.GetMethods());
        Assert.Equal("EvaluateAsync", metodo.Name);
        Assert.Equal(typeof(Task<CommercialReadOnlyEvaluationResult>), metodo.ReturnType);
        Assert.Equal(
            new[] { typeof(OrderCorrelationRequest), typeof(CancellationToken) },
            metodo.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [Fact]
    public void A3_El_veredicto_publico_tiene_exactamente_tres_valores_negativos()
    {
        Assert.Equal(
            new[] { "Rejected", "PendingDecision", "Inconsistent" },
            Enum.GetNames<CommercialReadOnlyEvaluationVerdict>());
        Assert.Equal(
            new[] { 0, 1, 2 },
            Enum.GetValues<CommercialReadOnlyEvaluationVerdict>().Select(v => (int)v).ToArray());
    }

    [Fact]
    public void A4_A6_D10_Ningun_tipo_publico_lleva_aplicacion_durable_ni_autorizacion_indirecta()
    {
        var prohibidas = new Regex(
            "Applied|Eligible|Durable|Effect|Authoriz|Token|Success|Confirm|Idempot|Settlement(Id|Reference)|Payable$",
            RegexOptions.CultureInvariant);

        var nombres = TiposPublicos.Select(t => t.Name)
            .Concat(TiposPublicos.SelectMany(t => t.GetProperties()).Select(p => p.Name))
            .Concat(TiposPublicos.Where(t => t.IsEnum).SelectMany(Enum.GetNames))
            .ToArray();
        Assert.DoesNotContain(nombres, n => prohibidas.IsMatch(n));

        // El único booleano es un hecho del pedido; ninguno puede leerse como permiso.
        var booleanos = TiposPublicos.SelectMany(t => t.GetProperties())
            .Where(p => p.PropertyType == typeof(bool))
            .Select(p => p.Name)
            .ToArray();
        Assert.Equal(new[] { nameof(CommercialReadOnlyOrderFacts.IsActive) }, booleanos);

        var tipos = TiposPublicos.SelectMany(t => t.GetProperties()).Select(p => p.PropertyType).ToArray();
        Assert.DoesNotContain(typeof(PayableOrderCandidate), tipos);
        Assert.DoesNotContain(typeof(PayableOutcome), tipos);
        Assert.DoesNotContain(typeof(ExternalSettlementOutcome), tipos);
        Assert.DoesNotContain(typeof(ExternalSettlementCandidateResult), tipos);
    }

    [Fact]
    public void A5_E4_Ningun_tipo_publico_exige_M03_EF_ni_Pagos()
    {
        var contratos = typeof(ICommercialReadOnlyEvaluationCandidate).Assembly;

        foreach (var propiedad in TiposPublicos.SelectMany(t => t.GetProperties()))
        {
            var tipo = Nullable.GetUnderlyingType(propiedad.PropertyType) ?? propiedad.PropertyType;
            Assert.True(
                tipo.Assembly == contratos || tipo.Namespace == "System",
                $"{propiedad.DeclaringType!.Name}.{propiedad.Name}: {tipo.FullName}");
        }

        var referencias = contratos.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain(referencias, n => n.Contains("EntityFramework", StringComparison.Ordinal));
        Assert.DoesNotContain("Sillar.Modules.Sales", referencias);
        Assert.DoesNotContain(referencias, n => n.Contains("Payments", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(SalesDbContext).Assembly.GetReferencedAssemblies(),
            a => a.Name!.Contains("Payments", StringComparison.Ordinal));
    }

    [Fact]
    public void A7_Los_resultados_son_inmutables()
    {
        foreach (var propiedad in new[] { typeof(CommercialReadOnlyEvaluationResult), typeof(CommercialReadOnlyOrderFacts) }
                     .SelectMany(t => t.GetProperties()))
        {
            var setter = propiedad.SetMethod;
            Assert.True(
                setter is null || setter.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"),
                $"{propiedad.Name} tiene un set público");
        }

        Assert.True(typeof(CommercialReadOnlyEvaluationResult).IsSealed);
        Assert.True(typeof(CommercialReadOnlyOrderFacts).IsSealed);

        var original = Proyectar(Interna(CommercialEvaluationVerdict.PendingDecision,
            CommercialEvaluationReason.CurrencyAuthorityMissing, Resuelta(), HechosDistintivos()));
        var copia = original with { Verdict = CommercialReadOnlyEvaluationVerdict.Rejected };
        Assert.Equal(CommercialReadOnlyEvaluationVerdict.PendingDecision, original.Verdict);
        Assert.NotSame(original, copia);
    }

    // ---------- B · proyección ----------

    [Fact]
    public void B1_Los_vocabularios_publicos_tienen_un_gemelo_por_cada_valor_interno()
    {
        Assert.Equal(Enum.GetNames<CommercialEvaluationVerdict>(), Enum.GetNames<CommercialReadOnlyEvaluationVerdict>());
        Assert.Equal(Enum.GetNames<CommercialEvaluationReason>(), Enum.GetNames<CommercialReadOnlyEvaluationReason>());
        Assert.Equal(Enum.GetNames<CurrencyObservation>(), Enum.GetNames<CommercialReadOnlyCurrencyObservation>());
    }

    /// <summary>Cada razón interna en la combinación que la evaluación produce.</summary>
    public static TheoryData<string, string, string, bool> CombinacionesValidas => new()
    {
        { "Rejected", "NotAuthoritative", "NotAuthoritative", false },
        { "Rejected", "MalformedReference", "MalformedReference", false },
        { "Rejected", "UnknownReference", "UnknownReference", false },
        { "Rejected", "DivergentDeclaration", "DivergentDeclaration", false },
        { "Rejected", "InactiveOrder", "Resolved", true },
        { "Rejected", "InvalidAmount", "Resolved", true },
        { "PendingDecision", "OrderLifecycleUnratified", "Resolved", true },
        { "PendingDecision", "AlreadySettledManually", "Resolved", true },
        { "PendingDecision", "CurrencyAuthorityMissing", "Resolved", true },
        { "Inconsistent", "OrderChangedBetweenReads", "Resolved", false },
        { "Inconsistent", "ContradictoryFacts", "Resolved", true },
        { "Inconsistent", "ContradictoryFacts", "Resolved", false },
        { "Inconsistent", "UnexpectedPolicyAnswer", "Resolved", true },
        { "Inconsistent", "UnexpectedPolicyAnswer", "Resolved", false },
        { "Inconsistent", "UnexpectedPolicyAnswer", "UnknownReference", false },
    };

    [Theory]
    [MemberData(nameof(CombinacionesValidas))]
    public void B1_B2_B3_B4_Cada_razon_se_proyecta_con_su_veredicto_y_su_nombre_exactos(
        string veredicto, string razon, string correlacion, bool conHechos)
    {
        var interna = Interna(
            Enum.Parse<CommercialEvaluationVerdict>(veredicto),
            Enum.Parse<CommercialEvaluationReason>(razon),
            Correlacion(Enum.Parse<OrderCorrelationOutcome>(correlacion)),
            conHechos ? HechosDistintivos() : null);

        var publica = Proyectar(interna);

        Assert.Equal(veredicto, publica.Verdict.ToString());
        Assert.Equal(razon, publica.Reason.ToString());
        Assert.Same(interna.Correlation, publica.Correlation);
        Assert.Equal(conHechos, publica.Facts is not null);
    }

    [Fact]
    public void B5_B7_B8_B9_Los_hechos_se_copian_campo_a_campo_sin_fundir_origenes_ni_acreditar_moneda()
    {
        var hechos = HechosDistintivos();
        var publica = Proyectar(Interna(CommercialEvaluationVerdict.Inconsistent,
            CommercialEvaluationReason.ContradictoryFacts, Resuelta(), hechos));

        var f = Assert.IsType<CommercialReadOnlyOrderFacts>(publica.Facts);
        Assert.Equal(hechos.Status, f.Status);
        Assert.Equal(hechos.FrozenTotal, f.FrozenTotal);
        Assert.Equal(hechos.IsActive, f.IsActive);
        Assert.Equal(hechos.OrderCurrencyCode, f.OrderCurrencyCode);
        Assert.Equal(CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority, f.OrderCurrency);
        Assert.Equal(30m, f.StaffRecordedTotal);
        Assert.Equal(1, f.StaffPaymentCount);
        Assert.Equal(20m, f.UnattributedHistoricalTotal);
        Assert.Equal(2, f.UnattributedHistoricalCount);
        Assert.Equal(7m, f.UnratifiedSourceTotal);
        Assert.Equal(3, f.UnratifiedSourceCount);
        Assert.Equal(4, f.PaymentsWithoutCurrencyCount);
        Assert.Equal(CommercialReadOnlyEvaluationVerdict.Inconsistent, publica.Verdict);
    }

    [Theory]
    [InlineData("Absent", "Absent")]
    [InlineData("MalformedValue", "MalformedValue")]
    [InlineData("PresentWithoutRatifiedAuthority", "PresentWithoutRatifiedAuthority")]
    public void B9_Cada_observacion_de_moneda_conserva_su_nombre(string interna, string publica)
    {
        var hechos = HechosDistintivos() with { OrderCurrency = Enum.Parse<CurrencyObservation>(interna) };

        var r = Proyectar(Interna(CommercialEvaluationVerdict.PendingDecision,
            CommercialEvaluationReason.CurrencyAuthorityMissing, Resuelta(), hechos));

        Assert.Equal(publica, r.Facts!.OrderCurrency.ToString());
    }

    [Fact]
    public void B10_Valores_internos_desconocidos_fallan_como_error_y_no_como_rechazo()
    {
        Assert.Throws<InvalidOperationException>(() => Proyectar(Interna(
            (CommercialEvaluationVerdict)99, CommercialEvaluationReason.UnknownReference,
            Correlacion(OrderCorrelationOutcome.UnknownReference), null)));

        Assert.Throws<InvalidOperationException>(() => Proyectar(Interna(
            CommercialEvaluationVerdict.Rejected, (CommercialEvaluationReason)99,
            Correlacion(OrderCorrelationOutcome.UnknownReference), null)));

        Assert.Throws<InvalidOperationException>(() => Proyectar(Interna(
            CommercialEvaluationVerdict.PendingDecision, CommercialEvaluationReason.CurrencyAuthorityMissing,
            Resuelta(), HechosDistintivos() with { OrderCurrency = (CurrencyObservation)99 })));
    }

    public static TheoryData<string, string, string, bool> CombinacionesImposibles => new()
    {
        { "Rejected", "CurrencyAuthorityMissing", "Resolved", true },
        { "PendingDecision", "InactiveOrder", "Resolved", true },
        { "PendingDecision", "ContradictoryFacts", "Resolved", true },
        { "Rejected", "ContradictoryFacts", "Resolved", true },
        { "PendingDecision", "OrderChangedBetweenReads", "Resolved", false },
        { "Inconsistent", "UnknownReference", "UnknownReference", false },
        { "Rejected", "UnknownReference", "Resolved", false },
        { "Rejected", "UnknownReference", "MalformedReference", false },
        { "Rejected", "NotAuthoritative", "NotAuthoritative", true },
        { "Rejected", "InactiveOrder", "Resolved", false },
        { "Rejected", "InvalidAmount", "UnknownReference", true },
        { "PendingDecision", "CurrencyAuthorityMissing", "Resolved", false },
        { "Inconsistent", "OrderChangedBetweenReads", "Resolved", true },
        { "Inconsistent", "ContradictoryFacts", "UnknownReference", false },
    };

    [Theory]
    [MemberData(nameof(CombinacionesImposibles))]
    public void B10_Combinaciones_imposibles_fallan_cerradas(
        string veredicto, string razon, string correlacion, bool conHechos)
    {
        var interna = Interna(
            Enum.Parse<CommercialEvaluationVerdict>(veredicto),
            Enum.Parse<CommercialEvaluationReason>(razon),
            Correlacion(Enum.Parse<OrderCorrelationOutcome>(correlacion)),
            conHechos ? HechosDistintivos() : null);

        var error = Assert.Throws<InvalidOperationException>(() => Proyectar(interna));
        Assert.Contains("imposible", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task B6_Sin_hechos_no_es_lo_mismo_que_hechos_en_cero()
    {
        var (contrato, _, _) = Componer(Sano());

        var desconocido = await contrato.EvaluateAsync(new OrderCorrelationRequest(CodigoOtro, null), CancellationToken.None);
        var sinPagos = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Null(desconocido.Facts);
        var f = Assert.IsType<CommercialReadOnlyOrderFacts>(sinPagos.Facts);
        Assert.Equal(0m, f.StaffRecordedTotal);
        Assert.Equal(0, f.StaffPaymentCount);
        Assert.Equal(0m, f.UnattributedHistoricalTotal);
        Assert.Equal(0m, f.UnratifiedSourceTotal);
    }

    [Fact]
    public async Task B_Lo_omitido_es_redundante_en_toda_la_ejecucion_real()
    {
        // La pagabilidad interna no se publica. Se demuestra aquí que, fuera de
        // UnexpectedPolicyAnswer, no dice nada que el veredicto, la razón y los hechos
        // no digan ya; y que la identidad de los hechos es la de la correlación.
        foreach (var (pedido, pagos) in Escenarios())
        {
            foreach (var solicitud in SolicitudesVariadas())
            {
                var base_ = Base(pedido, pagos);
                var interna = await new CandidateCommercialReadOnlyEvaluator(Contexto(base_))
                    .EvaluateAsync(solicitud, CancellationToken.None);

                if (interna.Payability is { } p)
                {
                    Assert.Equal(p.Outcome is PayableOutcome.NotEligible ? "Rejected" : "PendingDecision", interna.Verdict.ToString());
                    Assert.Equal(p.Reason.ToString(), interna.Reason.ToString());
                    Assert.Equal(p.FrozenTotal, interna.Facts!.FrozenTotal);
                    Assert.Equal(p.Status, interna.Facts.Status);
                    Assert.Equal(p.ManuallySettledTotal, interna.Facts.RecordedPaymentsTotal);
                }

                if (interna.Facts is { } h)
                {
                    Assert.Equal(interna.Correlation.OrderId, h.OrderId);
                    Assert.Equal(interna.Correlation.OrderCode, h.OrderCode);
                    Assert.Null(h.DerivableBalance);
                }
            }
        }
    }

    // ---------- C · ejecución real por el contrato ----------

    [Fact]
    public async Task C1_Una_correlacion_resuelta_no_autoriza_el_pago()
    {
        var (contrato, base_, _) = Componer(Sano());

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(OrderCorrelationOutcome.Resolved, r.Correlation.Outcome);
        Assert.Equal(Pedido, r.Correlation.OrderId);
        Assert.Equal(CommercialReadOnlyEvaluationVerdict.PendingDecision, r.Verdict);
        Assert.Equal(CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing, r.Reason);
        Assert.Equal(3, base_.Consultas.Count);
    }

    [Theory]
    [InlineData("P-2026-0148", null, "UnknownReference", 1)]
    [InlineData("P-2026-147", null, "MalformedReference", 0)]
    [InlineData(null, "00000000-0000-0000-0000-000000000000", "MalformedReference", 0)]
    [InlineData("P-2026-0147", "01999f3a-0000-7000-8000-000000000002", "DivergentDeclaration", 1)]
    public async Task C2_C3_C4_Los_rechazos_de_correlacion_conservan_su_razon_y_no_leen_hechos(
        string? codigo, string? id, string razon, int consultas)
    {
        var (contrato, base_, _) = Componer(Sano(), new PedidoSimulado(Otro, CodigoOtro + "9", OrderStatus.PendingPayment, 5m, null, true));

        var r = await contrato.EvaluateAsync(
            new OrderCorrelationRequest(codigo, id is null ? null : Guid.Parse(id)), CancellationToken.None);

        Assert.Equal(CommercialReadOnlyEvaluationVerdict.Rejected, r.Verdict);
        Assert.Equal(razon, r.Reason.ToString());
        Assert.Null(r.Correlation.OrderId);
        Assert.Null(r.Facts);
        Assert.Equal(consultas, base_.Consultas.Count);
    }

    [Fact]
    public async Task C3_Una_referencia_sin_verificar_queda_no_autoritativa()
    {
        var (contrato, base_, _) = Componer(Sano());

        var r = await contrato.EvaluateAsync(
            new OrderCorrelationRequest(null, null) { UnverifiedProviderReference = Codigo }, CancellationToken.None);

        Assert.Equal(CommercialReadOnlyEvaluationReason.NotAuthoritative, r.Reason);
        Assert.Empty(base_.Consultas);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, false, 100, "Rejected", "InactiveOrder")]
    [InlineData(OrderStatus.PendingPayment, true, -5, "Rejected", "InvalidAmount")]
    [InlineData(OrderStatus.Cancelled, true, -5, "Rejected", "InvalidAmount")]
    [InlineData(OrderStatus.Cancelled, false, -5, "Rejected", "InactiveOrder")]
    [InlineData(OrderStatus.Delivered, true, 100, "PendingDecision", "OrderLifecycleUnratified")]
    [InlineData(OrderStatus.Expired, true, 100, "PendingDecision", "CurrencyAuthorityMissing")]
    public async Task C5_C6_Inactivo_y_total_negativo_siguen_la_precedencia_real(
        string estado, bool activo, int total, string veredicto, string razon)
    {
        var (contrato, _, _) = Componer(Sano(estado, total, activo: activo));

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(veredicto, r.Verdict.ToString());
        Assert.Equal(razon, r.Reason.ToString());
        Assert.Equal(activo, r.Facts!.IsActive);
        Assert.Equal((decimal)total, r.Facts.FrozenTotal);
    }

    [Theory]
    [InlineData("PEN")]
    [InlineData("USD")]
    public async Task C7_La_moneda_sin_autoridad_queda_pendiente(string moneda)
    {
        var (contrato, _, _) = Componer(Sano(moneda: moneda));

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialReadOnlyEvaluationVerdict.PendingDecision, r.Verdict);
        Assert.Equal(CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing, r.Reason);
        Assert.Equal(moneda, r.Facts!.OrderCurrencyCode);
        Assert.Equal(CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority, r.Facts.OrderCurrency);
    }

    [Fact]
    public async Task C8_Historicos_que_cubren_el_total_conservan_la_razon_heredada_sin_atribuirlos_al_personal()
    {
        var (contrato, base_, _) = Componer(Sano(total: 100m));
        base_.Pagos.Add(new PagoSimulado(Pedido, 60m, null, null));
        base_.Pagos.Add(new PagoSimulado(Pedido, 40m, null, null));

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialReadOnlyEvaluationVerdict.PendingDecision, r.Verdict);
        Assert.Equal(CommercialReadOnlyEvaluationReason.AlreadySettledManually, r.Reason);
        Assert.Equal(100m, r.Facts!.UnattributedHistoricalTotal);
        Assert.Equal(2, r.Facts.UnattributedHistoricalCount);
        Assert.Equal(0m, r.Facts.StaffRecordedTotal);
        Assert.Equal(0, r.Facts.StaffPaymentCount);
        Assert.Equal(0, r.Facts.UnratifiedSourceCount);
    }

    [Fact]
    public async Task C8_B7_Personal_e_historicos_mezclados_van_cada_uno_en_su_sitio()
    {
        var (contrato, base_, _) = Componer(Sano(total: 100m));
        base_.Pagos.Add(new PagoSimulado(Pedido, 30m, PaymentSource.Staff, null));
        base_.Pagos.Add(new PagoSimulado(Pedido, 20m, null, null));

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(30m, r.Facts!.StaffRecordedTotal);
        Assert.Equal(1, r.Facts.StaffPaymentCount);
        Assert.Equal(20m, r.Facts.UnattributedHistoricalTotal);
        Assert.Equal(1, r.Facts.UnattributedHistoricalCount);
        Assert.Equal(2, r.Facts.PaymentsWithoutCurrencyCount);
    }

    public static TheoryData<string, PedidoSimulado, PagoSimulado[]> Contradicciones
    {
        get
        {
            var datos = new TheoryData<string, PedidoSimulado, PagoSimulado[]>();
            foreach (var (caso, pedido, pagos) in CasosContradictorios())
            {
                datos.Add(caso, pedido, pagos);
            }

            return datos;
        }
    }

    [Theory]
    [MemberData(nameof(Contradicciones))]
    public async Task C9_B5_B8_Los_hechos_contradictorios_quedan_incoherentes_y_se_publican(
        string caso, PedidoSimulado pedido, PagoSimulado[] pagos)
    {
        var (contrato, base_, _) = Componer(pedido);
        base_.Pagos.AddRange(pagos);

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.True(r.Verdict == CommercialReadOnlyEvaluationVerdict.Inconsistent, caso);
        Assert.Equal(CommercialReadOnlyEvaluationReason.ContradictoryFacts, r.Reason);
        Assert.NotNull(r.Facts);
        Assert.Equal(pedido.Status, r.Facts.Status);
        Assert.Equal(pagos.Count(p => p.Source is not null && p.Source != PaymentSource.Staff), r.Facts.UnratifiedSourceCount);
        Assert.Equal(pagos.Count(p => p.Source == PaymentSource.Staff), r.Facts.StaffPaymentCount);
    }

    [Fact]
    public async Task C10_Un_cambio_de_identidad_entre_lecturas_queda_incoherente_sin_hechos()
    {
        var (contrato, base_, _) = Componer(Sano());
        base_.TrasConsulta = n =>
        {
            if (n == 1)
            {
                base_.Pedidos[0] = base_.Pedidos[0] with { OrderCode = CodigoOtro };
            }
        };

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialReadOnlyEvaluationVerdict.Inconsistent, r.Verdict);
        Assert.Equal(CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads, r.Reason);
        Assert.Null(r.Facts);
    }

    [Fact]
    public async Task C10_Si_el_pedido_desaparece_entre_lecturas_queda_incoherente()
    {
        var (contrato, base_, _) = Componer(Sano());
        base_.TrasConsulta = n => { if (n == 1) base_.Pedidos.Clear(); };

        var r = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads, r.Reason);
        Assert.Equal(2, base_.Consultas.Count);
    }

    [Fact]
    public async Task C_F5_El_contrato_responde_exactamente_lo_que_el_evaluador_real_observa()
    {
        // Prueba de invocación real: mismo resultado, campo a campo, que el evaluador
        // ejecutado directamente sobre los mismos hechos, y las mismas consultas.
        foreach (var (pedido, pagos) in Escenarios())
        {
            foreach (var solicitud in SolicitudesVariadas())
            {
                var baseDirecta = Base(pedido, pagos);
                var interna = await new CandidateCommercialReadOnlyEvaluator(Contexto(baseDirecta))
                    .EvaluateAsync(solicitud, CancellationToken.None);

                var (contrato, baseContrato, _) = Componer(pedido);
                baseContrato.Pagos.AddRange(pagos);
                var publica = await contrato.EvaluateAsync(solicitud, CancellationToken.None);

                Assert.Equal(interna.Verdict.ToString(), publica.Verdict.ToString());
                Assert.Equal(interna.Reason.ToString(), publica.Reason.ToString());
                Assert.Equal(interna.Correlation, publica.Correlation);
                Assert.Equal(interna.Facts is null, publica.Facts is null);
                Assert.Equal(baseDirecta.Consultas, baseContrato.Consultas);
                Assert.True(Enum.IsDefined(publica.Verdict));
            }
        }
    }

    // ---------- D · ausencia de efectos ----------

    [Fact]
    public async Task D1_D2_D3_D4_D5_Ninguna_evaluacion_guarda_escribe_toca_journal_ni_aplicador()
    {
        var base_ = Base(Sano(moneda: "PEN"), [new PagoSimulado(Pedido, 30m, PaymentSource.Staff, "PEN")]);
        base_.Pedidos.Add(new PedidoSimulado(Otro, CodigoOtro, OrderStatus.Cancelled, 50m, null, true));
        var contexto = Contexto(base_);
        var rastreado = new Order
        {
            OrderId = Guid.Parse("01999f3a-0000-7000-8000-0000000000ff"),
            OrderCode = "P-2026-0999",
            CustomerFullName = "Cliente de prueba",
            CustomerEmail = "cliente@ejemplo.test",
            Status = OrderStatus.PendingPayment,
            TotalAmount = 10m,
        };
        contexto.Attach(rastreado);
        var journal = new JournalEspia();
        var aplicador = new AplicadorEspia();

        using var proveedor = Proveedor(contexto, journal, aplicador);
        using var ambito = proveedor.CreateScope();
        var contrato = ambito.ServiceProvider.GetRequiredService<ICommercialReadOnlyEvaluationCandidate>();

        foreach (var solicitud in SolicitudesVariadas())
        {
            await contrato.EvaluateAsync(solicitud, CancellationToken.None);
        }

        Assert.NotEmpty(base_.Consultas);
        Assert.All(base_.Consultas, sql => Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal));
        Assert.Equal(0, base_.Guardados);
        Assert.Equal(0, base_.ComandosProhibidos);
        Assert.Equal(0, base_.Transacciones);
        Assert.Equal(0, journal.Busquedas);
        Assert.Equal(0, journal.Recordados);
        Assert.Equal(0, aplicador.Llamadas);
        var entrada = Assert.Single(contexto.ChangeTracker.Entries());
        Assert.Equal(EntityState.Unchanged, entrada.State);
        Assert.Equal(OrderStatus.PendingPayment, rastreado.Status);
        Assert.False(contexto.ChangeTracker.HasChanges());
    }

    [Fact]
    public void D2_La_instrumentacion_si_detecta_escrituras()
    {
        var base_ = new BaseComercialSimulada();
        using var contexto = Contexto(base_);

        Assert.ThrowsAny<Exception>(() => contexto.Database.ExecuteSqlRaw("DELETE FROM sales.order_payments"));
        Assert.Equal(1, base_.ComandosProhibidos);
    }

    [Fact]
    public void D6_El_adaptador_y_la_proyeccion_no_guardan_estado()
    {
        const BindingFlags Todos = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        var campo = Assert.Single(typeof(CandidateCommercialReadOnlyEvaluationAdapter).GetFields(Todos));
        Assert.False(campo.IsStatic);
        Assert.True(campo.IsInitOnly);
        Assert.Equal(typeof(CandidateCommercialReadOnlyEvaluator), campo.FieldType);

        Assert.DoesNotContain(typeof(CommercialReadOnlyEvaluationProjection).GetFields(Todos), f => !f.IsLiteral);
        Assert.DoesNotContain(typeof(CandidateSalesReadOnlyEvaluationRegistration).GetFields(Todos), f => !f.IsLiteral);

        var constructor = Assert.Single(typeof(CandidateCommercialReadOnlyEvaluationAdapter).GetConstructors());
        Assert.Equal(
            new[] { typeof(CandidateCommercialReadOnlyEvaluator) },
            constructor.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task D7_Un_fallo_de_lectura_se_propaga_sin_efectos_ni_respuesta_comercial(int consulta)
    {
        var (contrato, base_, contexto) = Componer(Sano());
        base_.FalloEnConsulta = consulta;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => contrato.EvaluateAsync(PorCodigo, CancellationToken.None));

        Assert.Contains("base simulada caída", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, base_.ComandosProhibidos);
        Assert.Equal(0, base_.Guardados);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Fact]
    public async Task D8_La_cancelacion_se_propaga_antes_y_entre_lecturas_sin_efectos()
    {
        var (contrato, base_, contexto) = Componer(Sano());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => contrato.EvaluateAsync(PorCodigo, new CancellationToken(canceled: true)));
        Assert.Empty(base_.Consultas);

        using var fuente = new CancellationTokenSource();
        base_.TrasConsulta = n => { if (n == 1) fuente.Cancel(); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => contrato.EvaluateAsync(PorCodigo, fuente.Token));

        Assert.Single(base_.Consultas);
        Assert.Equal(0, base_.ComandosProhibidos);
        Assert.Equal(0, base_.Guardados);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Fact]
    public async Task D6_D9_Repetir_responde_igual_relee_y_no_crea_identidad()
    {
        var (contrato, base_, contexto) = Componer(Sano());
        base_.Pagos.Add(new PagoSimulado(Pedido, 30m, PaymentSource.Staff, null));

        var primera = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);
        var segunda = await contrato.EvaluateAsync(PorCodigo, CancellationToken.None);

        Assert.Equal(primera, segunda);
        Assert.NotSame(primera, segunda);
        Assert.Equal(6, base_.Consultas.Count);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    // ---------- E · composición y DI ----------

    [Fact]
    public void E1_E7_SalesModule_no_registra_el_contrato()
    {
        var servicios = ServiciosDeM03();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(ICommercialReadOnlyEvaluationCandidate));

        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();
        Assert.Null(ambito.ServiceProvider.GetService<ICommercialReadOnlyEvaluationCandidate>());
    }

    [Fact]
    public void E2_E3_E5_El_registro_opt_in_anade_solo_el_contrato_scoped_e_idempotente()
    {
        var servicios = ServiciosDeM03();
        var antes = servicios.ToArray();

        servicios.AddCandidateSalesReadOnlyEvaluation();
        servicios.AddCandidateSalesReadOnlyEvaluation();

        var nuevos = servicios.Except(antes).ToArray();
        var unico = Assert.Single(nuevos);
        Assert.Equal(typeof(ICommercialReadOnlyEvaluationCandidate), unico.ServiceType);
        Assert.Equal(ServiceLifetime.Scoped, unico.Lifetime);
        Assert.NotNull(unico.ImplementationFactory);

        Assert.DoesNotContain(servicios, d => Prohibidos.Contains(d.ServiceType)
            || (d.ImplementationType is { } t && Prohibidos.Contains(t)));

        using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var ambito = proveedor.CreateScope();
        var contrato = ambito.ServiceProvider.GetRequiredService<ICommercialReadOnlyEvaluationCandidate>();
        Assert.IsType<CandidateCommercialReadOnlyEvaluationAdapter>(contrato);
        Assert.Throws<InvalidOperationException>(() => proveedor.GetRequiredService<ICommercialReadOnlyEvaluationCandidate>());

        foreach (var prohibido in Prohibidos)
        {
            Assert.Null(ambito.ServiceProvider.GetService(prohibido));
        }
    }

    [Fact]
    public void E3_Sin_M03_registrado_el_contrato_no_inventa_una_base()
    {
        var servicios = new ServiceCollection().AddCandidateSalesReadOnlyEvaluation();

        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();

        Assert.Throws<InvalidOperationException>(
            () => ambito.ServiceProvider.GetRequiredService<ICommercialReadOnlyEvaluationCandidate>());
        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(SalesDbContext));
    }

    [Fact]
    public void E3_F2_El_opt_in_no_altera_la_extension_de_correlacion_de_G5B()
    {
        var servicios = new ServiceCollection();
        servicios.AddCandidateSalesOrderCorrelation();
        servicios.AddCandidateSalesReadOnlyEvaluation();

        Assert.Equal(2, servicios.Count);
        var correlacion = Assert.Single(servicios, d => d.ServiceType == typeof(IOrderCorrelationCandidate));
        Assert.Equal(typeof(CandidateOrderCorrelationReader), correlacion.ImplementationType);
        Assert.Single(servicios, d => d.ServiceType == typeof(ICommercialReadOnlyEvaluationCandidate));
    }

    // ---------- montaje ----------

    private static CommercialReadOnlyEvaluationResult Proyectar(CommercialEvaluationObservation interna)
        => CommercialReadOnlyEvaluationProjection.Proyectar(interna);

    private static CommercialEvaluationObservation Interna(
        CommercialEvaluationVerdict veredicto,
        CommercialEvaluationReason razon,
        OrderCorrelationCandidate correlacion,
        CommercialFactsObservation? hechos)
        => new(veredicto, razon, correlacion, null, hechos);

    private static OrderCorrelationCandidate Resuelta()
        => new(OrderCorrelationOutcome.Resolved, Pedido, Codigo, CandidateSources.CurrentDatabase);

    private static OrderCorrelationCandidate Correlacion(OrderCorrelationOutcome desenlace)
        => desenlace is OrderCorrelationOutcome.Resolved
            ? Resuelta()
            : new(desenlace, null, null, CandidateSources.CurrentDatabase);

    /// <summary>Valores distintos en cada campo: una fusión o un cruce se nota.</summary>
    private static CommercialFactsObservation HechosDistintivos() => new(
        Pedido, Codigo, OrderStatus.Preparing, 123.45m, true, "PEN",
        CurrencyObservation.PresentWithoutRatifiedAuthority,
        30m, 1, 20m, 2, 7m, 3, 4);

    private static IEnumerable<(string Caso, PedidoSimulado Pedido, PagoSimulado[] Pagos)> CasosContradictorios() =>
    [
        ("estado fuera del vocabulario", Sano(estado: "pagado"), []),
        ("moneda del pedido sin forma ISO", Sano(moneda: "pen"), []),
        ("importe de pago negativo", Sano(), [new(Pedido, -1m, PaymentSource.Staff, null)]),
        ("moneda de pago sin forma ISO", Sano(), [new(Pedido, 10m, PaymentSource.Staff, "S/.")]),
        ("pago en otra moneda que el pedido", Sano(moneda: "PEN"), [new(Pedido, 10m, PaymentSource.Staff, "USD")]),
        ("pagos en monedas distintas", Sano(),
            [new(Pedido, 10m, PaymentSource.Staff, "PEN"), new(Pedido, 10m, PaymentSource.Staff, "USD")]),
        ("origen proveedor no ratificado", Sano(), [new(Pedido, 10m, PaymentSource.Provider, null)]),
        ("origen desconocido", Sano(), [new(Pedido, 10m, "pasarela", null)]),
    ];

    private static IEnumerable<(PedidoSimulado Pedido, PagoSimulado[] Pagos)> Escenarios() =>
    [
        (Sano(), []),
        (Sano(moneda: "PEN"), []),
        (Sano(estado: OrderStatus.Cancelled), []),
        (Sano(estado: OrderStatus.Delivered), []),
        (Sano(activo: false), []),
        (Sano(total: -5m), []),
        (Sano(), [new(Pedido, 30m, PaymentSource.Staff, null)]),
        (Sano(), [new(Pedido, 100m, PaymentSource.Staff, null)]),
        (Sano(), [new(Pedido, 60m, null, null), new(Pedido, 40m, null, null)]),
        .. CasosContradictorios().Select(c => (c.Pedido, c.Pagos)),
    ];

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

    private static BaseComercialSimulada Base(PedidoSimulado pedido, PagoSimulado[] pagos)
    {
        var base_ = new BaseComercialSimulada();
        base_.Pedidos.Add(pedido);
        base_.Pagos.AddRange(pagos);
        return base_;
    }

    /// <summary>
    /// Consumidor de prueba: resuelve el contrato público por el registro opt-in, sobre un
    /// <see cref="SalesDbContext"/> cuyo transporte es la base simulada.
    /// </summary>
    private static (ICommercialReadOnlyEvaluationCandidate Contrato, BaseComercialSimulada Base, SalesDbContext Contexto) Componer(
        params PedidoSimulado[] pedidos)
    {
        var base_ = new BaseComercialSimulada();
        base_.Pedidos.AddRange(pedidos);
        var contexto = Contexto(base_);
        var proveedor = Proveedor(contexto, new JournalEspia(), new AplicadorEspia());
        var contrato = proveedor.CreateScope().ServiceProvider.GetRequiredService<ICommercialReadOnlyEvaluationCandidate>();
        return (contrato, base_, contexto);
    }

    private static ServiceProvider Proveedor(SalesDbContext contexto, JournalEspia journal, AplicadorEspia aplicador)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton(contexto);
        servicios.AddSingleton<ISettlementCandidateJournal>(journal);
        servicios.AddSingleton<IExternalSettlementCandidate>(aplicador);
        servicios.AddCandidateSalesReadOnlyEvaluation();
        return servicios.BuildServiceProvider();
    }

    private static SalesDbContext Contexto(BaseComercialSimulada interceptor)
    {
        var opciones = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(CadenaSinBase)
            .AddInterceptors(interceptor);

        return new SalesDbContext(opciones.Options, new NodeIdentity("prueba"), TimeProvider.System);
    }

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
    /// El transporte sustituido, igual que en <see cref="EvaluacionComercialSinEfectosTests"/>. No abre conexión; responde a cada <c>SELECT</c> con las
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
