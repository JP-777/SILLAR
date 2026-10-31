using Microsoft.EntityFrameworkCore;
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
    public void Total_negativo_solo_es_coherente_con_invalid_amount()
    {
        var pending = ValidResult(
            CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing);

        var result = M03CommercialReadOnlyEvaluationMapper.Map(pending with
        {
            Facts = pending.Facts! with { FrozenTotal = -1m }
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
