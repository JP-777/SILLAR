using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Payments.Tests;

public sealed class M03CommercialReadOnlyEvaluationConsumerTests
{
    private static readonly Guid OrderId =
        Guid.Parse("0193a2b4-0000-7000-8000-0000000000b1");

    [Theory]
    [InlineData(CommercialReadOnlyEvaluationReason.NotAuthoritative, (int)M03CommercialReadOnlyEvaluationOutcome.Rejected)]
    [InlineData(CommercialReadOnlyEvaluationReason.MalformedReference, (int)M03CommercialReadOnlyEvaluationOutcome.Rejected)]
    [InlineData(CommercialReadOnlyEvaluationReason.UnknownReference, (int)M03CommercialReadOnlyEvaluationOutcome.Rejected)]
    [InlineData(CommercialReadOnlyEvaluationReason.DivergentDeclaration, (int)M03CommercialReadOnlyEvaluationOutcome.Rejected)]
    [InlineData(CommercialReadOnlyEvaluationReason.InactiveOrder, (int)M03CommercialReadOnlyEvaluationOutcome.Rejected)]
    [InlineData(CommercialReadOnlyEvaluationReason.InvalidAmount, (int)M03CommercialReadOnlyEvaluationOutcome.Rejected)]
    [InlineData(CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified, (int)M03CommercialReadOnlyEvaluationOutcome.DecisionRequired)]
    [InlineData(CommercialReadOnlyEvaluationReason.AlreadySettledManually, (int)M03CommercialReadOnlyEvaluationOutcome.DecisionRequired)]
    [InlineData(CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing, (int)M03CommercialReadOnlyEvaluationOutcome.DecisionRequired)]
    [InlineData(CommercialReadOnlyEvaluationReason.ContradictoryFacts, (int)M03CommercialReadOnlyEvaluationOutcome.Conflict)]
    [InlineData(CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads, (int)M03CommercialReadOnlyEvaluationOutcome.Indeterminate)]
    [InlineData(CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer, (int)M03CommercialReadOnlyEvaluationOutcome.Indeterminate)]
    public void Doce_razones_se_mapean_por_pareja_y_preservan_origen(
        CommercialReadOnlyEvaluationReason reason,
        int expectedOutcome)
    {
        var source = ValidResult(reason);

        var result = M03CommercialReadOnlyEvaluationMapper.Map(source);

        Assert.Equal((M03CommercialReadOnlyEvaluationOutcome)expectedOutcome, result.Outcome);
        Assert.Equal(source.Verdict, result.SourceVerdict);
        Assert.Equal(reason, result.SourceReason);
        Assert.Null(result.TechnicalReason);
        Assert.Null(typeof(M03CommercialReadOnlyEvaluationResult)
            .GetProperty("PaymentId"));
    }

    [Fact]
    public async Task Adaptador_implementa_puerto_y_preserva_solicitud_controlada()
    {
        var producer = new Candidate(_ => Task.FromResult(
            ValidResult(CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing)));
        IM03CommercialReadOnlyEvaluationPort port =
            new M03CommercialReadOnlyEvaluationPortAdapter(producer);

        var result = await port.EvaluateAsync(
            new(" P-2026-0147 ", OrderId),
            CancellationToken.None);

        Assert.Equal(
            M03CommercialReadOnlyEvaluationOutcome.DecisionRequired,
            result.Outcome);
        Assert.Equal(" P-2026-0147 ", producer.Request!.DeclaredOrderCode);
        Assert.Equal(OrderId, producer.Request.DeclaredOrderId);
        Assert.Null(producer.Request.UnverifiedProviderReference);
        Assert.False(port is IProviderCommercialEffectPort);
    }

    [Theory]
    [InlineData(999, (int)CommercialReadOnlyEvaluationReason.NotAuthoritative, "m03_readonly_evaluation_unknown_verdict")]
    [InlineData((int)CommercialReadOnlyEvaluationVerdict.Rejected, 999, "m03_readonly_evaluation_unknown_reason")]
    public void Enums_desconocidos_fallan_cerrados(
        int verdict,
        int reason,
        string expected)
    {
        var result = M03CommercialReadOnlyEvaluationMapper.Map(new(
            (CommercialReadOnlyEvaluationVerdict)verdict,
            (CommercialReadOnlyEvaluationReason)reason,
            NegativeCorrelation(OrderCorrelationOutcome.NotAuthoritative),
            null));

        AssertTechnical(result, expected);
    }

    [Fact]
    public void Combinacion_veredicto_razon_imposible_no_simula_rechazo()
    {
        var result = M03CommercialReadOnlyEvaluationMapper.Map(
            ValidResult(CommercialReadOnlyEvaluationReason.ContradictoryFacts)
                with { Verdict = CommercialReadOnlyEvaluationVerdict.Rejected });

        AssertTechnical(
            result,
            "m03_readonly_evaluation_invalid_verdict_reason_pair");
    }

    [Fact]
    public void Correlacion_resuelta_sin_identidad_falla_cerrada()
    {
        var source = ValidResult(
            CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing);

        var result = M03CommercialReadOnlyEvaluationMapper.Map(source with
        {
            Correlation = new(
                OrderCorrelationOutcome.Resolved,
                null,
                null,
                CandidateSources.CurrentDatabase)
        });

        AssertTechnical(result, "m03_readonly_evaluation_invalid_correlation");
    }

    [Fact]
    public void Correlacion_negativa_con_identidad_inventada_falla_cerrada()
    {
        var source = ValidResult(
            CommercialReadOnlyEvaluationReason.UnknownReference);

        var result = M03CommercialReadOnlyEvaluationMapper.Map(source with
        {
            Correlation = source.Correlation with
            {
                OrderId = OrderId,
                OrderCode = "P-2026-0147"
            }
        });

        AssertTechnical(result, "m03_readonly_evaluation_invalid_correlation");
    }

    [Fact]
    public void Fuente_de_correlacion_inesperada_falla_cerrada()
    {
        var source = ValidResult(
            CommercialReadOnlyEvaluationReason.MalformedReference);

        var result = M03CommercialReadOnlyEvaluationMapper.Map(source with
        {
            Correlation = source.Correlation with
            {
                Source = CandidateSources.CurrentDatabase
            }
        });

        AssertTechnical(result, "m03_readonly_evaluation_invalid_correlation");
    }

    [Fact]
    public void Hechos_ausentes_para_observacion_comercial_fallan_cerrados()
    {
        var result = M03CommercialReadOnlyEvaluationMapper.Map(
            ValidResult(CommercialReadOnlyEvaluationReason.InactiveOrder)
                with { Facts = null });

        AssertTechnical(
            result,
            "m03_readonly_evaluation_invalid_verdict_reason_pair");
    }

    [Fact]
    public void Hechos_presentes_en_rechazo_de_correlacion_fallan_cerrados()
    {
        var result = M03CommercialReadOnlyEvaluationMapper.Map(
            ValidResult(CommercialReadOnlyEvaluationReason.MalformedReference)
                with { Facts = Facts() });

        AssertTechnical(
            result,
            "m03_readonly_evaluation_invalid_verdict_reason_pair");
    }

    [Fact]
    public void Estado_de_moneda_desconocido_falla_cerrado()
    {
        var source = ValidResult(
            CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing);

        var result = M03CommercialReadOnlyEvaluationMapper.Map(source with
        {
            Facts = source.Facts! with
            {
                OrderCurrency = (CommercialReadOnlyCurrencyObservation)999
            }
        });

        AssertTechnical(result, "m03_readonly_evaluation_invalid_facts");
    }

    [Theory]
    [InlineData(CommercialReadOnlyCurrencyObservation.Absent, "PEN")]
    [InlineData(CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority, "pen")]
    [InlineData(CommercialReadOnlyCurrencyObservation.MalformedValue, "PEN")]
    public void Forma_de_moneda_incoherente_falla_cerrada(
        CommercialReadOnlyCurrencyObservation observation,
        string code)
    {
        var source = ValidResult(
            CommercialReadOnlyEvaluationReason.ContradictoryFacts);

        var result = M03CommercialReadOnlyEvaluationMapper.Map(source with
        {
            Facts = source.Facts! with
            {
                OrderCurrency = observation,
                OrderCurrencyCode = code
            }
        });

        AssertTechnical(result, "m03_readonly_evaluation_invalid_facts");
    }

    [Fact]
    public void Totales_sin_filas_y_conteos_negativos_fallan_cerrados()
    {
        var source = ValidResult(
            CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing);

        var totalWithoutRows = M03CommercialReadOnlyEvaluationMapper.Map(source with
        {
            Facts = source.Facts! with { StaffRecordedTotal = 1m }
        });
        var negativeCount = M03CommercialReadOnlyEvaluationMapper.Map(source with
        {
            Facts = source.Facts! with { StaffPaymentCount = -1 }
        });

        AssertTechnical(totalWithoutRows, "m03_readonly_evaluation_invalid_facts");
        AssertTechnical(negativeCount, "m03_readonly_evaluation_invalid_facts");
    }

    [Fact]
    public void Fuente_no_ratificada_solo_es_admisible_como_contradiccion()
    {
        var pending = ValidResult(
            CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing);
        var result = M03CommercialReadOnlyEvaluationMapper.Map(pending with
        {
            Facts = pending.Facts! with
            {
                UnratifiedSourceTotal = 10m,
                UnratifiedSourceCount = 1
            }
        });

        AssertTechnical(result, "m03_readonly_evaluation_invalid_facts");

        var conflict = M03CommercialReadOnlyEvaluationMapper.Map(
            ValidResult(CommercialReadOnlyEvaluationReason.ContradictoryFacts));
        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Conflict, conflict.Outcome);
        Assert.Equal(1, conflict.Facts!.UnratifiedSourceCount);
    }

    [Fact]
    public void Pagos_historicos_staff_y_origen_no_ratificado_permanecen_separados()
    {
        var source = ValidResult(
            CommercialReadOnlyEvaluationReason.ContradictoryFacts);
        source = source with
        {
            Facts = source.Facts! with
            {
                StaffRecordedTotal = 30m,
                StaffPaymentCount = 1,
                UnattributedHistoricalTotal = 20m,
                UnattributedHistoricalCount = 2,
                UnratifiedSourceTotal = 7m,
                UnratifiedSourceCount = 3
            }
        };

        var result = M03CommercialReadOnlyEvaluationMapper.Map(source);

        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Conflict, result.Outcome);
        Assert.Equal(30m, result.Facts!.StaffRecordedTotal);
        Assert.Equal(1, result.Facts.StaffPaymentCount);
        Assert.Equal(20m, result.Facts.UnattributedHistoricalTotal);
        Assert.Equal(2, result.Facts.UnattributedHistoricalCount);
        Assert.Equal(7m, result.Facts.UnratifiedSourceTotal);
        Assert.Equal(3, result.Facts.UnratifiedSourceCount);
    }

    [Fact]
    public void Total_negativo_no_elige_la_razon_pero_invalid_amount_exige_su_total()
    {
        // H-1: la precedencia es de M03. Un total negativo no obliga a InvalidAmount.
        var inactive = ValidResult(CommercialReadOnlyEvaluationReason.InactiveOrder);
        var preserved = M03CommercialReadOnlyEvaluationMapper.Map(inactive with
        {
            Facts = inactive.Facts! with { FrozenTotal = -1m }
        });

        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Rejected, preserved.Outcome);
        Assert.Equal(CommercialReadOnlyEvaluationReason.InactiveOrder, preserved.SourceReason);
        Assert.Equal(-1m, preserved.Facts!.FrozenTotal);

        // La implicación inversa sí es de la razón misma: InvalidAmount sin total imposible.
        var invalidAmount = ValidResult(CommercialReadOnlyEvaluationReason.InvalidAmount);
        var result = M03CommercialReadOnlyEvaluationMapper.Map(invalidAmount with
        {
            Facts = invalidAmount.Facts! with { FrozenTotal = 0m }
        });

        AssertTechnical(result, "m03_readonly_evaluation_invalid_facts");
    }

    [Fact]
    public void Nulo_runtime_falla_cerrado_sin_razon_comercial_fabricada()
    {
        var result = M03CommercialReadOnlyEvaluationMapper.Map(null);

        AssertTechnical(result, "m03_readonly_evaluation_null_result");
        Assert.Null(result.SourceReason);
        Assert.Null(result.SourceVerdict);
    }

    [Fact]
    public async Task Fallo_tecnico_es_indeterminado_y_sanea_excepcion()
    {
        var adapter = new M03CommercialReadOnlyEvaluationPortAdapter(
            new Candidate(_ => throw new InvalidOperationException(
                "secret connection value")));

        var result = await adapter.EvaluateAsync(
            new("P-2026-0147", null),
            CancellationToken.None);

        AssertTechnical(result, "m03_readonly_evaluation_call_failed");
        Assert.DoesNotContain("secret", result.TechnicalReason);
    }

    [Fact]
    public async Task Cancelacion_se_propaga_y_no_se_convierte_en_negocio()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var adapter = new M03CommercialReadOnlyEvaluationPortAdapter(
            new Candidate(request => Task.FromCanceled<CommercialReadOnlyEvaluationResult>(
                request.CancellationToken)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await adapter.EvaluateAsync(
                new("P-2026-0147", null),
                cancellation.Token));
    }

    [Fact]
    public async Task Repetir_observacion_no_acumula_estado_ni_crea_equivalencia_durable()
    {
        var producer = new Candidate(_ => Task.FromResult(
            ValidResult(CommercialReadOnlyEvaluationReason.AlreadySettledManually)));
        var adapter = new M03CommercialReadOnlyEvaluationPortAdapter(producer);

        var first = await adapter.EvaluateAsync(
            new("P-2026-0147", null), CancellationToken.None);
        var second = await adapter.EvaluateAsync(
            new("P-2026-0147", null), CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(2, producer.Calls);
        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.DecisionRequired, second.Outcome);
        Assert.DoesNotContain(
            "Applied",
            Enum.GetNames<M03CommercialReadOnlyEvaluationOutcome>());
    }

    [Fact]
    public void Consumidor_no_tiene_dependencia_productiva_en_implementacion_sales()
    {
        var references = typeof(M03CommercialReadOnlyEvaluationPortAdapter)
            .Assembly.GetReferencedAssemblies().Select(reference => reference.Name);
        var constructor = Assert.Single(
            typeof(M03CommercialReadOnlyEvaluationPortAdapter)
                .GetConstructors(
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic));

        Assert.DoesNotContain("Sillar.Modules.Sales", references);
        Assert.Contains("Sillar.Modules.Sales.Contracts", references);
        Assert.Equal(
            typeof(ICommercialReadOnlyEvaluationCandidate),
            Assert.Single(constructor.GetParameters()).ParameterType);
    }

    [Fact]
    public void PaymentsModule_no_registra_puerto_nuevo_ni_contrato_m03()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    "Host=localhost;Database=unused;Username=unused;Password=unused"
            })
            .Build();

        new PaymentsModule().RegisterServices(services, configuration);

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IM03CommercialReadOnlyEvaluationPort)
            || descriptor.ServiceType == typeof(ICommercialReadOnlyEvaluationCandidate));
        Assert.DoesNotContain(
            typeof(ProviderInboxProcessor).GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType
                == typeof(IM03CommercialReadOnlyEvaluationPort));
    }

    [Fact]
    public async Task Composicion_real_alcanza_adaptador_y_evaluador_m03_sin_base()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateSalesContext());
        services.AddCandidateSalesReadOnlyEvaluation();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contract = scope.ServiceProvider
            .GetRequiredService<ICommercialReadOnlyEvaluationCandidate>();
        Assert.Equal(
            "Sillar.Modules.Sales.Commercial.CandidateCommercialReadOnlyEvaluationAdapter",
            contract.GetType().FullName);

        var adapter = new M03CommercialReadOnlyEvaluationPortAdapter(contract);
        var result = await adapter.EvaluateAsync(
            new("not-a-ratified-order-code", null),
            CancellationToken.None);

        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Rejected, result.Outcome);
        Assert.Equal(
            CommercialReadOnlyEvaluationReason.MalformedReference,
            result.SourceReason);
        Assert.Null(result.Facts);
    }

    [Fact]
    public async Task Descriptor_preexistente_no_se_confunde_con_adaptador_acreditado()
    {
        var unexpected = new Candidate(_ => Task.FromResult(
            ValidResult(CommercialReadOnlyEvaluationReason.NotAuthoritative)));
        var services = new ServiceCollection();
        services.AddScoped<ICommercialReadOnlyEvaluationCandidate>(_ => unexpected);
        services.AddCandidateSalesReadOnlyEvaluation();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var effective = scope.ServiceProvider
            .GetRequiredService<ICommercialReadOnlyEvaluationCandidate>();

        Assert.Same(unexpected, effective);
        Assert.Throws<InvalidOperationException>(() =>
            RequireAccreditedM03Adapter(effective));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(ICommercialReadOnlyEvaluationCandidate));
    }

    private static ICommercialReadOnlyEvaluationCandidate RequireAccreditedM03Adapter(
        ICommercialReadOnlyEvaluationCandidate candidate)
        => candidate.GetType().FullName ==
            "Sillar.Modules.Sales.Commercial.CandidateCommercialReadOnlyEvaluationAdapter"
                ? candidate
                : throw new InvalidOperationException(
                    "La composición no resolvió el adaptador acreditado de M03.");

    private static CommercialReadOnlyEvaluationResult ValidResult(
        CommercialReadOnlyEvaluationReason reason)
    {
        var verdict = reason switch
        {
            <= CommercialReadOnlyEvaluationReason.InvalidAmount =>
                CommercialReadOnlyEvaluationVerdict.Rejected,
            <= CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing =>
                CommercialReadOnlyEvaluationVerdict.PendingDecision,
            _ => CommercialReadOnlyEvaluationVerdict.Inconsistent
        };

        var correlation = reason switch
        {
            CommercialReadOnlyEvaluationReason.NotAuthoritative =>
                NegativeCorrelation(OrderCorrelationOutcome.NotAuthoritative),
            CommercialReadOnlyEvaluationReason.MalformedReference =>
                NegativeCorrelation(OrderCorrelationOutcome.MalformedReference),
            CommercialReadOnlyEvaluationReason.UnknownReference =>
                NegativeCorrelation(OrderCorrelationOutcome.UnknownReference),
            CommercialReadOnlyEvaluationReason.DivergentDeclaration =>
                NegativeCorrelation(OrderCorrelationOutcome.DivergentDeclaration),
            _ => ResolvedCorrelation()
        };

        CommercialReadOnlyOrderFacts? facts = reason switch
        {
            <= CommercialReadOnlyEvaluationReason.DivergentDeclaration => null,
            CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads => null,
            CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer => null,
            CommercialReadOnlyEvaluationReason.ContradictoryFacts => Facts(
                unratifiedTotal: 7m,
                unratifiedCount: 1),
            CommercialReadOnlyEvaluationReason.InvalidAmount => Facts(frozenTotal: -1m),
            CommercialReadOnlyEvaluationReason.InactiveOrder => Facts(isActive: false),
            _ => Facts()
        };

        return new(verdict, reason, correlation, facts);
    }

    private static OrderCorrelationCandidate NegativeCorrelation(
        OrderCorrelationOutcome outcome) => new(
            outcome,
            null,
            null,
            outcome is OrderCorrelationOutcome.NotAuthoritative
                or OrderCorrelationOutcome.MalformedReference
                    ? CandidateSources.RatifiedRules
                    : CandidateSources.CurrentDatabase);

    private static OrderCorrelationCandidate ResolvedCorrelation() => new(
        OrderCorrelationOutcome.Resolved,
        OrderId,
        "P-2026-0147",
        CandidateSources.CurrentDatabase);

    private static CommercialReadOnlyOrderFacts Facts(
        decimal frozenTotal = 100m,
        bool isActive = true,
        decimal unratifiedTotal = 0m,
        int unratifiedCount = 0) => new(
            "preparing",
            frozenTotal,
            isActive,
            null,
            CommercialReadOnlyCurrencyObservation.Absent,
            0m,
            0,
            0m,
            0,
            unratifiedTotal,
            unratifiedCount,
            0);

    private static SalesDbContext CreateSalesContext()
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=not_used;Username=not_used;Password=not_used;Timeout=1")
            .Options;
        return new SalesDbContext(
            options,
            new NodeIdentity("prueba"),
            TimeProvider.System);
    }

    private static void AssertTechnical(
        M03CommercialReadOnlyEvaluationResult result,
        string expectedReason)
    {
        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Indeterminate, result.Outcome);
        Assert.Equal(expectedReason, result.TechnicalReason);
        Assert.Null(result.Correlation);
        Assert.Null(result.Facts);
    }

    private sealed class Candidate(
        Func<Call, Task<CommercialReadOnlyEvaluationResult>> evaluate)
        : ICommercialReadOnlyEvaluationCandidate
    {
        public int Calls { get; private set; }
        public OrderCorrelationRequest? Request { get; private set; }

        public Task<CommercialReadOnlyEvaluationResult> EvaluateAsync(
            OrderCorrelationRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            return evaluate(new(request, cancellationToken));
        }
    }

    private sealed record Call(
        OrderCorrelationRequest Request,
        CancellationToken CancellationToken);
}

/// <summary>
/// P1-G6-1B-B-F1 · H-1. M03 es dueño de la política comercial y de su precedencia; M11
/// solo valida el transporte. Recorrido real: puerto M11 → contrato público M03 →
/// adaptador M03 → evaluador G6-1A → transporte EF simulado, sin PostgreSQL.
/// </summary>
public sealed class M03CommercialReadOnlyEvaluationFidelityTests
{
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-0000000000c1");
    private static readonly Guid Otro = Guid.Parse("01999f3a-0000-7000-8000-0000000000c2");
    private const string Codigo = "P-2026-0201";
    private const string CodigoOtro = "P-2026-0202";
    private const string AdaptadorM03 =
        "Sillar.Modules.Sales.Commercial.CandidateCommercialReadOnlyEvaluationAdapter";

    private const string Facts_ = "m03_readonly_evaluation_invalid_facts";
    private const string Pair = "m03_readonly_evaluation_invalid_verdict_reason_pair";
    private const string Corr = "m03_readonly_evaluation_invalid_correlation";

    // ---------------------------------------------------------------------
    // La tabla del mandato, escrita a mano y no derivada del mapper.
    // ---------------------------------------------------------------------

    private static M03CommercialReadOnlyEvaluationOutcome Desenlace(CommercialReadOnlyEvaluationReason razon) => razon switch
    {
        CommercialReadOnlyEvaluationReason.NotAuthoritative => M03CommercialReadOnlyEvaluationOutcome.Rejected,
        CommercialReadOnlyEvaluationReason.MalformedReference => M03CommercialReadOnlyEvaluationOutcome.Rejected,
        CommercialReadOnlyEvaluationReason.UnknownReference => M03CommercialReadOnlyEvaluationOutcome.Rejected,
        CommercialReadOnlyEvaluationReason.DivergentDeclaration => M03CommercialReadOnlyEvaluationOutcome.Rejected,
        CommercialReadOnlyEvaluationReason.InactiveOrder => M03CommercialReadOnlyEvaluationOutcome.Rejected,
        CommercialReadOnlyEvaluationReason.InvalidAmount => M03CommercialReadOnlyEvaluationOutcome.Rejected,
        CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified => M03CommercialReadOnlyEvaluationOutcome.DecisionRequired,
        CommercialReadOnlyEvaluationReason.AlreadySettledManually => M03CommercialReadOnlyEvaluationOutcome.DecisionRequired,
        CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing => M03CommercialReadOnlyEvaluationOutcome.DecisionRequired,
        CommercialReadOnlyEvaluationReason.ContradictoryFacts => M03CommercialReadOnlyEvaluationOutcome.Conflict,
        CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads => M03CommercialReadOnlyEvaluationOutcome.Indeterminate,
        CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer => M03CommercialReadOnlyEvaluationOutcome.Indeterminate,
        _ => throw new ArgumentOutOfRangeException(nameof(razon)),
    };

    private static M03OrderCorrelationOutcome Correlacion(OrderCorrelationOutcome o) => o switch
    {
        OrderCorrelationOutcome.NotAuthoritative => M03OrderCorrelationOutcome.NotAuthoritative,
        OrderCorrelationOutcome.MalformedReference => M03OrderCorrelationOutcome.MalformedReference,
        OrderCorrelationOutcome.UnknownReference => M03OrderCorrelationOutcome.UnknownReference,
        OrderCorrelationOutcome.DivergentDeclaration => M03OrderCorrelationOutcome.DivergentDeclaration,
        OrderCorrelationOutcome.Resolved => M03OrderCorrelationOutcome.Resolved,
        _ => throw new ArgumentOutOfRangeException(nameof(o)),
    };

    private static M03CommercialReadOnlyCurrencyObservation Moneda(CommercialReadOnlyCurrencyObservation o) => o switch
    {
        CommercialReadOnlyCurrencyObservation.Absent => M03CommercialReadOnlyCurrencyObservation.Absent,
        CommercialReadOnlyCurrencyObservation.MalformedValue => M03CommercialReadOnlyCurrencyObservation.MalformedValue,
        CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority => M03CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority,
        _ => throw new ArgumentOutOfRangeException(nameof(o)),
    };

    /// <summary>Lo que M11 debe devolver ante una respuesta válida de M03: lo mismo, traducido.</summary>
    private static M03CommercialReadOnlyEvaluationResult Esperado(CommercialReadOnlyEvaluationResult r)
    {
        var f = r.Facts;
        return new(
            Desenlace(r.Reason),
            r.Verdict,
            r.Reason,
            new(Correlacion(r.Correlation.Outcome), r.Correlation.OrderId, r.Correlation.OrderCode, r.Correlation.Source),
            f is null ? null : new(f.Status, f.FrozenTotal, f.IsActive, f.OrderCurrencyCode, Moneda(f.OrderCurrency),
                f.StaffRecordedTotal, f.StaffPaymentCount, f.UnattributedHistoricalTotal, f.UnattributedHistoricalCount,
                f.UnratifiedSourceTotal, f.UnratifiedSourceCount, f.PaymentsWithoutCurrencyCount),
            null);
    }

    // ---------------------------------------------------------------------
    // Composición por el recorrido real
    // ---------------------------------------------------------------------

    private sealed class EfectoEspia : IProviderCommercialEffectPort
    {
        public int Llamadas { get; private set; }

        public ValueTask<ProviderCommercialEffectResult> TryApplyAsync(
            ProviderCommercialEffectCandidate candidate, CancellationToken cancellationToken)
        {
            Llamadas++;
            throw new InvalidOperationException("El consumidor negativo no debe aplicar efectos.");
        }
    }

    private sealed record Composicion(
        IM03CommercialReadOnlyEvaluationPort Puerto,
        ICommercialReadOnlyEvaluationCandidate Contrato,
        BaseComercialSimulada Base,
        SalesDbContext Contexto,
        JournalEspia Journal,
        AplicadorEspia Aplicador,
        EfectoEspia Efecto,
        PedidoSimulado[] PedidosAntes,
        PagoSimulado[] PagosAntes);

    /// <summary>
    /// M03 por su extensión opt-in real, con el comercio legado sustituido por espías que
    /// cuentan cualquier uso, y el puerto M11 construido sobre el mismo ámbito. Nada de esto
    /// es registro productivo: vive solo en la prueba.
    /// </summary>
    private static Composicion Componer(PedidoSimulado[] pedidos, PagoSimulado[] pagos)
    {
        var b = new BaseComercialSimulada();
        b.Pedidos.AddRange(pedidos);
        b.Pagos.AddRange(pagos);
        var c = new SalesDbContext(
            new DbContextOptionsBuilder<SalesDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1")
                .AddInterceptors(b)
                .Options,
            new NodeIdentity("prueba"),
            TimeProvider.System);
        var j = new JournalEspia();
        var a = new AplicadorEspia();
        var e = new EfectoEspia();
        var s = new ServiceCollection();
        s.AddSingleton(c);
        s.AddCandidateSalesCommerce();
        s.AddSingleton<ISettlementCandidateJournal>(j);
        s.AddScoped<IExternalSettlementCandidate>(_ => a);
        s.AddSingleton<IProviderCommercialEffectPort>(e);
        s.AddCandidateSalesReadOnlyEvaluation();
        var ambito = s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .CreateScope().ServiceProvider;
        var contrato = ambito.GetRequiredService<ICommercialReadOnlyEvaluationCandidate>();
        Assert.Equal(AdaptadorM03, contrato.GetType().FullName);
        IM03CommercialReadOnlyEvaluationPort puerto =
            ActivatorUtilities.CreateInstance<M03CommercialReadOnlyEvaluationPortAdapter>(ambito);
        return new(puerto, contrato, b, c, j, a, e, [.. pedidos], [.. pagos]);
    }

    private static void SinEfectos(Composicion k)
    {
        Assert.Equal(0, k.Base.Guardados);
        Assert.Equal(0, k.Base.ComandosProhibidos);
        Assert.Equal(0, k.Base.Transacciones);
        Assert.All(k.Base.Consultas, sql => Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal));
        Assert.Empty(k.Contexto.ChangeTracker.Entries());
        Assert.Equal(0, k.Journal.Busquedas);
        Assert.Equal(0, k.Journal.Recordados);
        Assert.Equal(0, k.Aplicador.Llamadas);
        Assert.Equal(0, k.Efecto.Llamadas);
        Assert.Equal(k.PedidosAntes, k.Base.Pedidos);
        Assert.Equal(k.PagosAntes, k.Base.Pagos);
    }

    /// <summary>La respuesta de M03 por el contrato solo, y la de M11 por el puerto, sobre datos gemelos.</summary>
    private static async Task<(CommercialReadOnlyEvaluationResult M03, M03CommercialReadOnlyEvaluationResult M11, Composicion K)>
        Recorrer(PedidoSimulado pedido, PagoSimulado[] pagos)
    {
        var ka = Componer([pedido, ElOtro], pagos);
        var m03 = await ka.Contrato.EvaluateAsync(new(Codigo, null), CancellationToken.None);
        SinEfectos(ka);

        var kb = Componer([pedido, ElOtro], pagos);
        var m11 = await kb.Puerto.EvaluateAsync(new(Codigo, null), CancellationToken.None);
        SinEfectos(kb);
        Assert.Equal(ka.Base.Consultas, kb.Base.Consultas);
        return (m03, m11, kb);
    }

    private static PedidoSimulado ElOtro => new(Otro, CodigoOtro, OrderStatus.PendingPayment, 50m, "PEN", true);

    private static PedidoSimulado P(string estado = OrderStatus.PendingPayment, decimal total = 100m,
        string? moneda = null, bool activo = true) => new(Pedido, Codigo, estado, total, moneda, activo);

    private static PagoSimulado Staff(decimal m, string? moneda = null) => new(Pedido, m, PaymentSource.Staff, moneda);

    private static PagoSimulado Historico(decimal m, string? moneda = null) => new(Pedido, m, null, moneda);

    private static PagoSimulado Proveedor(decimal m) => new(Pedido, m, PaymentSource.Provider, null);

    // ---------------------------------------------------------------------
    // H1-A y H1-B · precedencia de M03 y preservación, por el recorrido real
    // ---------------------------------------------------------------------

    /// <summary>
    /// Cada escenario declara, a mano, la razón que M03 da por su propia precedencia. Si M11
    /// reconstruyera otra precedencia, o si M03 cambiara la suya, la prueba lo dice.
    /// </summary>
    private static readonly Dictionary<string, (PedidoSimulado Pedido, PagoSimulado[] Pagos, CommercialReadOnlyEvaluationReason Razon)> Escenarios = new()
    {
        // A1 · inactivo con total negativo: la baja lógica va antes que el importe.
        ["A1 inactivo total -5"] = (P(total: -5m, activo: false), [], CommercialReadOnlyEvaluationReason.InactiveOrder),
        ["A1 inactivo total -5 con pagos staff"] = (P(total: -5m, activo: false), [Staff(30m)], CommercialReadOnlyEvaluationReason.InactiveOrder),
        // A2 · inactivo con un estado que, activo, pediría otra razón.
        ["A2 inactivo cancelado"] = (P(estado: OrderStatus.Cancelled, activo: false), [], CommercialReadOnlyEvaluationReason.InactiveOrder),
        ["A2 inactivo entregado"] = (P(estado: OrderStatus.Delivered, activo: false), [], CommercialReadOnlyEvaluationReason.InactiveOrder),
        // A3 · inactivo con moneda presente sin autoridad.
        ["A3 inactivo moneda PEN"] = (P(moneda: "PEN", activo: false), [Staff(10m, "PEN")], CommercialReadOnlyEvaluationReason.InactiveOrder),
        // A4 · inactivo con todos los defectos secundarios que la precedencia ignora.
        ["A4 inactivo total -5 cancelado cubierto PEN"] = (P(OrderStatus.Cancelled, -5m, "PEN", false),
            [Staff(30m, "PEN"), Historico(60m), Historico(40m)], CommercialReadOnlyEvaluationReason.InactiveOrder),
        // A5 · InvalidAmount cuando es la razón auténtica, también antes que el ciclo de vida.
        ["A5 activo total -5"] = (P(total: -5m), [], CommercialReadOnlyEvaluationReason.InvalidAmount),
        ["A5 activo total -5 cancelado"] = (P(OrderStatus.Cancelled, -5m), [Historico(10m)], CommercialReadOnlyEvaluationReason.InvalidAmount),
        // A6 · hechos contradictorios, con total negativo incluido.
        ["A6 origen proveedor"] = (P(), [Proveedor(10m)], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A6 pago negativo"] = (P(), [Staff(-1m)], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A6 total -5 y origen proveedor"] = (P(total: -5m), [Proveedor(10m)], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A6 inactivo total -5 monedas mezcladas"] = (P(total: -5m, activo: false), [Staff(10m, "PEN"), Historico(10m, "USD")],
            CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        // A7 · estado vacío o en blanco.
        ["A7 estado vacío"] = (P(estado: ""), [], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A7 estado en blanco"] = (P(estado: "  "), [Staff(10m)], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A7 estado vacío inactivo"] = (P(estado: "", activo: false), [], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        // A8 · moneda malformada, también vacía.
        ["A8 moneda vacía"] = (P(moneda: ""), [], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A8 moneda en blanco"] = (P(moneda: " "), [], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A8 moneda minúscula"] = (P(moneda: "pen"), [], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A8 moneda inactivo S/."] = (P(moneda: "S/.", activo: false), [], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        // A9 · varias anomalías a la vez.
        ["A9 todo roto"] = (P("", -5m, "", false), [Proveedor(10m), Staff(-1m), Historico(5m)], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        ["A9 blanco y negativo activo"] = (P("  ", -5m, " "), [Staff(30m), Historico(20m), Proveedor(7m)], CommercialReadOnlyEvaluationReason.ContradictoryFacts),
        // A10 · referencias sanas: la tabla completa de razones con hechos sigue intacta.
        ["A10 ciclo de vida"] = (P(estado: OrderStatus.Delivered), [], CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified),
        ["A10 ya cobrado con históricos"] = (P(), [Staff(30m), Historico(60m), Historico(40m)], CommercialReadOnlyEvaluationReason.AlreadySettledManually),
        ["A10 sin autoridad de moneda"] = (P(moneda: "PEN"), [Staff(30m, "PEN")], CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing),
    };

    public static TheoryData<string> NombresDeEscenarios => new(Escenarios.Keys);

    [Theory]
    [MemberData(nameof(NombresDeEscenarios))]
    public async Task H1_A_y_B_M11_conserva_la_respuesta_de_M03_con_su_propia_precedencia(string escenario)
    {
        var (pedido, pagos, razon) = Escenarios[escenario];

        var (m03, m11, k) = await Recorrer(pedido, pagos);

        // A10: la razón es la del evaluador real, y es la que se escribió a mano.
        Assert.Equal(razon, m03.Reason);
        Assert.Equal(Esperado(m03), m11);

        // B1–B3
        Assert.Equal(Desenlace(razon), m11.Outcome);
        Assert.Equal(m03.Verdict, m11.SourceVerdict);
        Assert.Equal(razon, m11.SourceReason);
        Assert.Null(m11.TechnicalReason);

        // B4: la correlación es la del pedido, leída de la base.
        Assert.Equal(M03OrderCorrelationOutcome.Resolved, m11.Correlation!.Outcome);
        Assert.Equal(Pedido, m11.Correlation.OrderId);
        Assert.Equal(Codigo, m11.Correlation.OrderCode);
        Assert.Equal(CandidateSources.CurrentDatabase, m11.Correlation.Source);

        // B5–B9: los hechos son los de la base, calculados aquí a mano.
        var h = Assert.IsType<M03CommercialReadOnlyOrderFactsSnapshot>(m11.Facts);
        Assert.Equal(pedido.Status, h.Status);
        Assert.Equal(pedido.TotalAmount, h.FrozenTotal);
        Assert.Equal(pedido.IsActive, h.IsActive);
        Assert.Equal(pedido.CurrencyCode, h.OrderCurrencyCode);
        var staff = pagos.Where(x => x.Source == PaymentSource.Staff).ToArray();
        var historicos = pagos.Where(x => x.Source is null).ToArray();
        var otros = pagos.Where(x => x.Source is not null && x.Source != PaymentSource.Staff).ToArray();
        Assert.Equal((staff.Sum(x => x.Amount), staff.Length), (h.StaffRecordedTotal, h.StaffPaymentCount));
        Assert.Equal((historicos.Sum(x => x.Amount), historicos.Length), (h.UnattributedHistoricalTotal, h.UnattributedHistoricalCount));
        Assert.Equal((otros.Sum(x => x.Amount), otros.Length), (h.UnratifiedSourceTotal, h.UnratifiedSourceCount));
        Assert.Equal(pagos.Count(x => x.CurrencyCode is null), h.PaymentsWithoutCurrencyCount);

        // Recorrido completo: correlación, pedido y pagos.
        Assert.Equal(3, k.Base.Consultas.Count);
        Assert.Contains("FROM sales.orders", k.Base.Consultas[1], StringComparison.Ordinal);
        Assert.Contains("FROM sales.order_payments", k.Base.Consultas[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task H1_B7_B8_Historicos_sin_origen_no_se_atribuyen_al_personal_bajo_InactiveOrder_ni_Conflict()
    {
        var (_, inactivo, _) = await Recorrer(P(total: -5m, activo: false), [Staff(30m), Historico(60m), Historico(40m)]);
        Assert.Equal(CommercialReadOnlyEvaluationReason.InactiveOrder, inactivo.SourceReason);
        Assert.Equal((30m, 1, 100m, 2, 0m, 0),
            (inactivo.Facts!.StaffRecordedTotal, inactivo.Facts.StaffPaymentCount,
             inactivo.Facts.UnattributedHistoricalTotal, inactivo.Facts.UnattributedHistoricalCount,
             inactivo.Facts.UnratifiedSourceTotal, inactivo.Facts.UnratifiedSourceCount));

        var (_, conflicto, _) = await Recorrer(P(estado: ""), [Staff(30m), Historico(20m), Proveedor(10m)]);
        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Conflict, conflicto.Outcome);
        Assert.Equal((30m, 1, 20m, 1, 10m, 1),
            (conflicto.Facts!.StaffRecordedTotal, conflicto.Facts.StaffPaymentCount,
             conflicto.Facts.UnattributedHistoricalTotal, conflicto.Facts.UnattributedHistoricalCount,
             conflicto.Facts.UnratifiedSourceTotal, conflicto.Facts.UnratifiedSourceCount));
    }

    [Fact]
    public void H1_B10_Ningun_tipo_del_consumidor_transporta_saldo_autorizacion_ni_identidad_durable()
    {
        Type[] tipos =
        [
            typeof(M03CommercialReadOnlyEvaluationResult),
            typeof(M03CommercialReadOnlyOrderFactsSnapshot),
            typeof(M03OrderCorrelationResultSnapshot),
        ];
        string[] prohibidos = ["Balance", "Saldo", "Authoriz", "Settle", "PaymentId", "Applied", "Durable", "Authenticated"];

        foreach (var tipo in tipos)
        {
            foreach (var propiedad in tipo.GetProperties())
            {
                Assert.DoesNotContain(prohibidos, p => propiedad.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
            }
        }

        Assert.Equal(
            ["Rejected", "DecisionRequired", "Conflict", "Indeterminate"],
            Enum.GetNames<M03CommercialReadOnlyEvaluationOutcome>());
    }

    // ---------------------------------------------------------------------
    // H1-C · DTO hostiles: lo anómalo pero válido pasa; lo imposible falla cerrado
    // ---------------------------------------------------------------------

    private sealed class Productor(CommercialReadOnlyEvaluationResult? respuesta) : ICommercialReadOnlyEvaluationCandidate
    {
        public int Llamadas { get; private set; }

        public Task<CommercialReadOnlyEvaluationResult> EvaluateAsync(OrderCorrelationRequest request, CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(respuesta!);
        }
    }

    private static OrderCorrelationCandidate Resuelta() =>
        new(OrderCorrelationOutcome.Resolved, Pedido, Codigo, CandidateSources.CurrentDatabase);

    private static CommercialReadOnlyOrderFacts Hechos(string estado = OrderStatus.Preparing, decimal total = 100m, bool activo = true) =>
        new(estado, total, activo, null, CommercialReadOnlyCurrencyObservation.Absent, 0m, 0, 0m, 0, 0m, 0, 0);

    private static CommercialReadOnlyEvaluationResult Inactivo(CommercialReadOnlyOrderFacts? f = null) =>
        new(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.InactiveOrder, Resuelta(), f ?? Hechos(activo: false));

    private static CommercialReadOnlyEvaluationResult Contradictorio(CommercialReadOnlyOrderFacts? f = null) =>
        new(CommercialReadOnlyEvaluationVerdict.Inconsistent, CommercialReadOnlyEvaluationReason.ContradictoryFacts, Resuelta(), f ?? Hechos());

    private static CommercialReadOnlyEvaluationResult SinMoneda(CommercialReadOnlyOrderFacts? f = null) =>
        new(CommercialReadOnlyEvaluationVerdict.PendingDecision, CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing, Resuelta(), f ?? Hechos());

    private static CommercialReadOnlyOrderFacts Moneda(CommercialReadOnlyOrderFacts f, CommercialReadOnlyCurrencyObservation o, string? codigo)
        => f with { OrderCurrency = o, OrderCurrencyCode = codigo };

    /// <summary>Respuestas que M03 puede dar —o que no contradicen su contrato— y que M11 debe conservar.</summary>
    private static readonly Dictionary<string, CommercialReadOnlyEvaluationResult> Validas = new()
    {
        ["total negativo bajo InactiveOrder"] = Inactivo(Hechos(total: -5m, activo: false)),
        ["total negativo bajo ContradictoryFacts"] = Contradictorio(Hechos(total: -5m)),
        ["total negativo bajo InvalidAmount"] = new(CommercialReadOnlyEvaluationVerdict.Rejected,
            CommercialReadOnlyEvaluationReason.InvalidAmount, Resuelta(), Hechos(total: -5m)),
        ["estado vacío bajo ContradictoryFacts"] = Contradictorio(Hechos(estado: "")),
        ["estado en blanco bajo ContradictoryFacts"] = Contradictorio(Hechos(estado: "   ")),
        ["moneda vacía malformada bajo ContradictoryFacts"] = Contradictorio(Moneda(Hechos(), CommercialReadOnlyCurrencyObservation.MalformedValue, "")),
        ["moneda en blanco malformada bajo ContradictoryFacts"] = Contradictorio(Moneda(Hechos(), CommercialReadOnlyCurrencyObservation.MalformedValue, " ")),
        ["todo roto bajo ContradictoryFacts"] = Contradictorio(Moneda(Hechos("", -5m, false), CommercialReadOnlyCurrencyObservation.MalformedValue, "")
            with { StaffRecordedTotal = -1m, StaffPaymentCount = 1, UnratifiedSourceTotal = 10m, UnratifiedSourceCount = 1 }),
        ["estado fuera de vocabulario bajo InactiveOrder"] = Inactivo(Hechos(estado: "pagado", activo: false)),
    };

    public static TheoryData<string> NombresValidas => new(Validas.Keys);

    [Theory]
    [MemberData(nameof(NombresValidas))]
    public async Task H1_C_Respuesta_anomala_pero_valida_no_se_degrada_a_tecnica(string caso)
    {
        var dto = Validas[caso];
        var productor = new Productor(dto);

        var r = await new M03CommercialReadOnlyEvaluationPortAdapter(productor)
            .EvaluateAsync(new(Codigo, null), CancellationToken.None);

        Assert.Equal(Esperado(dto), r);
        Assert.Equal(Desenlace(dto.Reason), r.Outcome);
        Assert.Equal(dto.Reason, r.SourceReason);
        Assert.Null(r.TechnicalReason);
        Assert.Equal(1, productor.Llamadas);
    }

    /// <summary>DTO que ningún productor fiel del contrato puede emitir. Siguen fallando cerrados.</summary>
    private static readonly Dictionary<string, (CommercialReadOnlyEvaluationResult? Dto, string Causa)> Imposibles = new()
    {
        // Total negativo, pero con una estructura imposible.
        ["total negativo bajo InactiveOrder sin hechos"] = (Inactivo() with { Facts = null }, Pair),
        ["total negativo bajo InactiveOrder con correlación negativa"] = (Inactivo(Hechos(total: -5m, activo: false)) with
            { Correlation = new(OrderCorrelationOutcome.UnknownReference, null, null, CandidateSources.CurrentDatabase) }, Pair),
        ["total negativo bajo InactiveOrder con total de staff sin filas"] = (Inactivo(Hechos(total: -5m, activo: false) with { StaffRecordedTotal = 5m }), Facts_),
        ["total negativo bajo InactiveOrder con veredicto PendingDecision"] = (Inactivo(Hechos(total: -5m, activo: false)) with
            { Verdict = CommercialReadOnlyEvaluationVerdict.PendingDecision }, Pair),
        // InvalidAmount sin el total que lo define.
        ["InvalidAmount con total cero"] = (new(CommercialReadOnlyEvaluationVerdict.Rejected,
            CommercialReadOnlyEvaluationReason.InvalidAmount, Resuelta(), Hechos(total: 0m)), Facts_),
        // Estado: nulo nunca; en blanco solo dentro de Conflict.
        ["estado nulo bajo ContradictoryFacts"] = (Contradictorio(Hechos() with { Status = null! }), Facts_),
        ["estado en blanco bajo CurrencyAuthorityMissing"] = (SinMoneda(Hechos(estado: "  ")), Facts_),
        ["estado vacío bajo InactiveOrder"] = (Inactivo(Hechos(estado: "", activo: false)), Facts_),
        // Moneda: la forma que el contrato atribuye a cada observación.
        ["Malformed con código nulo bajo ContradictoryFacts"] = (Contradictorio(Moneda(Hechos(), CommercialReadOnlyCurrencyObservation.MalformedValue, null)), Facts_),
        ["Malformed con código ISO bajo ContradictoryFacts"] = (Contradictorio(Moneda(Hechos(), CommercialReadOnlyCurrencyObservation.MalformedValue, "PEN")), Facts_),
        ["Absent con código bajo ContradictoryFacts"] = (Contradictorio(Moneda(Hechos(), CommercialReadOnlyCurrencyObservation.Absent, "")), Facts_),
        ["Present con código vacío bajo ContradictoryFacts"] = (Contradictorio(Moneda(Hechos(), CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority, "")), Facts_),
        ["moneda desconocida bajo ContradictoryFacts"] = (Contradictorio(Hechos() with { OrderCurrency = (CommercialReadOnlyCurrencyObservation)7 }), Facts_),
        ["Malformed vacía bajo InactiveOrder"] = (Inactivo(Moneda(Hechos(activo: false), CommercialReadOnlyCurrencyObservation.MalformedValue, "")), Facts_),
        // Correlación inventada por un productor falso.
        ["Conflict con identidad vacía"] = (Contradictorio() with { Correlation = Resuelta() with { OrderId = Guid.Empty } }, Corr),
        ["Conflict con código en blanco"] = (Contradictorio() with { Correlation = Resuelta() with { OrderCode = " " } }, Corr),
        ["Conflict con fuente ajena"] = (Contradictorio() with { Correlation = Resuelta() with { Source = "proveedor" } }, Corr),
        ["rechazo de correlación con identidad fabricada"] = (new(CommercialReadOnlyEvaluationVerdict.Rejected,
            CommercialReadOnlyEvaluationReason.UnknownReference,
            new(OrderCorrelationOutcome.UnknownReference, Pedido, Codigo, CandidateSources.CurrentDatabase), null), Corr),
        ["Conflict con correlación negativa"] = (Contradictorio() with
            { Correlation = new(OrderCorrelationOutcome.UnknownReference, null, null, CandidateSources.CurrentDatabase) }, Pair),
        // Agregados contradictorios entre sí, también dentro de Conflict.
        ["Conflict con pagos sin moneda por encima de los pagos"] = (Contradictorio(Hechos() with { StaffRecordedTotal = 5m, StaffPaymentCount = 1, PaymentsWithoutCurrencyCount = 2 }), Facts_),
        ["Conflict con total histórico sin filas"] = (Contradictorio(Hechos() with { UnattributedHistoricalTotal = 1m }), Facts_),
        ["Conflict con contador negativo"] = (Contradictorio(Hechos() with { UnratifiedSourceCount = -1 }), Facts_),
        // Atribución falsa demostrable: un total de staff sin ningún pago de staff.
        ["staff con total y sin pagos bajo AlreadySettledManually"] = (new(CommercialReadOnlyEvaluationVerdict.PendingDecision,
            CommercialReadOnlyEvaluationReason.AlreadySettledManually, Resuelta(),
            Hechos() with { StaffRecordedTotal = 100m, StaffPaymentCount = 0, UnattributedHistoricalTotal = 100m, UnattributedHistoricalCount = 2 }), Facts_),
        // Enums y nulos.
        ["veredicto desconocido"] = (Contradictorio() with { Verdict = (CommercialReadOnlyEvaluationVerdict)3 }, "m03_readonly_evaluation_unknown_verdict"),
        ["razón desconocida"] = (Contradictorio() with { Reason = (CommercialReadOnlyEvaluationReason)12 }, "m03_readonly_evaluation_unknown_reason"),
        ["ContradictoryFacts con veredicto Rejected"] = (Contradictorio() with { Verdict = CommercialReadOnlyEvaluationVerdict.Rejected }, Pair),
        ["resultado nulo"] = (null, "m03_readonly_evaluation_null_result"),
    };

    public static TheoryData<string> NombresImposibles => new(Imposibles.Keys);

    [Theory]
    [MemberData(nameof(NombresImposibles))]
    public async Task H1_C_DTO_imposible_sigue_fallando_cerrado_sin_reintento(string caso)
    {
        var (dto, causa) = Imposibles[caso];
        var productor = new Productor(dto);

        var r = await new M03CommercialReadOnlyEvaluationPortAdapter(productor)
            .EvaluateAsync(new(Codigo, null), CancellationToken.None);

        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Indeterminate, r.Outcome);
        Assert.Equal(causa, r.TechnicalReason);
        Assert.Null(r.SourceVerdict);
        Assert.Null(r.SourceReason);
        Assert.Null(r.Correlation);
        Assert.Null(r.Facts);
        Assert.Equal(1, productor.Llamadas);
    }

    // ---------------------------------------------------------------------
    // H1-D · excepciones y efectos por el recorrido real
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task H1_D_Fallo_de_lectura_en_datos_rotos_es_tecnico_saneado_sin_efectos(int consulta)
    {
        var k = Componer([P("", -5m, "", false), ElOtro], [Proveedor(10m)]);
        k.Base.FalloEnConsulta = consulta;

        var r = await k.Puerto.EvaluateAsync(new(Codigo, null), CancellationToken.None);

        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Indeterminate, r.Outcome);
        Assert.Equal("m03_readonly_evaluation_call_failed", r.TechnicalReason);
        Assert.DoesNotContain("caída", r.TechnicalReason, StringComparison.Ordinal);
        Assert.Null(r.SourceReason);
        Assert.Null(r.Facts);
        Assert.Equal(consulta, k.Base.Consultas.Count);
        SinEfectos(k);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task H1_D_Cancelacion_entre_lecturas_se_propaga_sin_respuesta_inventada(int trasConsulta)
    {
        using var cancelacion = new CancellationTokenSource();
        var k = Componer([P(total: -5m, activo: false), ElOtro], []);
        k.Base.TrasConsulta = n => { if (n == trasConsulta) { cancelacion.Cancel(); } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await k.Puerto.EvaluateAsync(new(Codigo, null), cancelacion.Token));

        Assert.Equal(trasConsulta, k.Base.Consultas.Count);
        SinEfectos(k);
    }

    [Fact]
    public async Task H1_D_Evaluaciones_repetidas_de_datos_rotos_no_acumulan_estado()
    {
        var k = Componer([P("", -5m, "", false), ElOtro], [Proveedor(10m)]);

        var primera = await k.Puerto.EvaluateAsync(new(Codigo, null), CancellationToken.None);
        var segunda = await k.Puerto.EvaluateAsync(new(Codigo, null), CancellationToken.None);

        Assert.Equal(M03CommercialReadOnlyEvaluationOutcome.Conflict, primera.Outcome);
        Assert.Equal(primera, segunda);
        Assert.Equal(6, k.Base.Consultas.Count);
        SinEfectos(k);
    }

    // ---------------------------------------------------------------------
    // Transporte EF simulado. Copia de ContratoEvaluacionComercialNegativoTests
    // (Sales.Tests @ b9f28037), que no es visible desde aquí y no se puede tocar.
    // ---------------------------------------------------------------------

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
    /// No abre conexión; responde a cada <c>SELECT</c> con las columnas que pide, filtrando
    /// por el parámetro con igualdad ordinal. SQL mutante, comandos escalares,
    /// transacciones y <c>SaveChanges</c> se cuentan y se rechazan.
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

        public Action<int>? TrasConsulta { get; set; }

        public int? FalloEnConsulta { get; set; }

        private static string[] Columnas(string sql)
        {
            var inicio = sql.IndexOf("SELECT", StringComparison.Ordinal) + "SELECT".Length;
            var fin = sql.IndexOf("FROM", inicio, StringComparison.Ordinal);

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
