using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Payments.Tests;

public sealed class M03NegativeCandidateAdaptersTests
{
    private static readonly Guid InboxId =
        Guid.Parse("0193a2b4-0000-7000-8000-0000000000a1");

    [Fact]
    public void Adaptadores_implementan_puertos_m11_y_consumen_contratos_publicos_m03()
    {
        IOrderCorrelationCandidate correlationCandidate =
            new CorrelationCandidate(null);
        IExternalSettlementCandidate settlementCandidate =
            SettlementCandidate.Returning(null);

        IM03OrderCorrelationCandidatePort correlationPort =
            new M03OrderCorrelationCandidatePortAdapter(correlationCandidate);
        IProviderCommercialEffectPort commercialPort =
            new M03ExternalSettlementCandidatePortAdapter(settlementCandidate);

        Assert.IsType<M03OrderCorrelationCandidatePortAdapter>(correlationPort);
        Assert.IsType<M03ExternalSettlementCandidatePortAdapter>(commercialPort);
    }

    [Fact]
    public async Task Correlacion_preserva_declaracion_exacta_sin_usar_referencia_no_verificada()
    {
        const string declaredCode = " P-2026-0147 ";
        var declaredId = PaymentTestData.OrderId;
        var producer = new CorrelationCandidate(new(
            OrderCorrelationOutcome.DivergentDeclaration,
            null,
            null,
            CandidateSources.CurrentDatabase));
        var adapter = new M03OrderCorrelationCandidatePortAdapter(producer);

        var result = await adapter.ResolveAsync(
            new(declaredCode, declaredId),
            CancellationToken);

        Assert.Equal(
            M03OrderCorrelationOutcome.DivergentDeclaration,
            result!.Outcome);
        Assert.Equal(declaredCode, producer.Request!.DeclaredOrderCode);
        Assert.Equal(declaredId, producer.Request.DeclaredOrderId);
        Assert.Null(producer.Request.UnverifiedProviderReference);
    }

    [Theory]
    [InlineData(
        OrderCorrelationOutcome.NotAuthoritative,
        (int)M03OrderCorrelationOutcome.NotAuthoritative)]
    [InlineData(
        OrderCorrelationOutcome.MalformedReference,
        (int)M03OrderCorrelationOutcome.MalformedReference)]
    [InlineData(
        OrderCorrelationOutcome.UnknownReference,
        (int)M03OrderCorrelationOutcome.UnknownReference)]
    [InlineData(
        OrderCorrelationOutcome.DivergentDeclaration,
        (int)M03OrderCorrelationOutcome.DivergentDeclaration)]
    public async Task Correlacion_negativa_conserva_clasificacion(
        OrderCorrelationOutcome publicOutcome,
        int localOutcomeValue)
    {
        var adapter = new M03OrderCorrelationCandidatePortAdapter(
            new CorrelationCandidate(new(
                publicOutcome,
                null,
                null,
                CandidateSources.RatifiedRules)));

        var result = await adapter.ResolveAsync(
            new("bad-or-unknown-reference", null),
            CancellationToken);

        Assert.Equal(
            (M03OrderCorrelationOutcome)localOutcomeValue,
            result!.Outcome);
        Assert.Null(result.OrderId);
        Assert.Null(result.OrderCode);
        Assert.Equal(CandidateSources.RatifiedRules, result.Source);
    }

    [Fact]
    public async Task Sin_declaracion_autenticada_no_se_consulta_ni_autoriza_correlacion()
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = ProviderVerifiedEvidenceReference.Create(
            Guid.CreateVersion7(),
            evidence,
            "attestation/provider-a/event-001");
        var producer = new CorrelationCandidate(new(
            OrderCorrelationOutcome.Resolved,
            PaymentTestData.OrderId,
            "P-2026-0147",
            CandidateSources.CurrentDatabase));
        var source = new ProviderAuthorizedOrderCorrelationAdapter(
            new DeclarationSource([]),
            new EvidenceReferenceSource([evidenceReference]),
            new M03OrderCorrelationCandidatePortAdapter(producer));

        var correlation = await source.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.Null(correlation);
        Assert.Equal(0, producer.Calls);
    }

    [Fact]
    public async Task Liquidacion_rechazada_conserva_semantica_terminal()
    {
        var producer = SettlementCandidate.Returning(new(
            ExternalSettlementOutcome.Rejected,
            ExternalSettlementReason.InvalidAmount));
        var adapter = new M03ExternalSettlementCandidatePortAdapter(producer);

        var result = await adapter.TryApplyAsync(
            PaymentTestData.CommercialCandidate(),
            CancellationToken);

        Assert.Equal(ProviderCommercialEffectOutcome.Rejected, result.Outcome);
        Assert.Equal("InvalidAmount", result.Reason);
        Assert.Null(result.Reference);
        Assert.Equal(1, producer.Calls);
    }

    [Theory]
    [InlineData(ExternalSettlementReason.MissingCurrency)]
    [InlineData(ExternalSettlementReason.CommercialPolicyUnratified)]
    [InlineData(ExternalSettlementReason.DurableIdentityPolicyUnratified)]
    public async Task Decision_pendiente_no_se_convierte_en_liquidacion_aceptada(
        ExternalSettlementReason reason)
    {
        var adapter = new M03ExternalSettlementCandidatePortAdapter(
            SettlementCandidate.Returning(new(
                ExternalSettlementOutcome.PendingDecision,
                reason)));

        var result = await adapter.TryApplyAsync(
            PaymentTestData.CommercialCandidate(),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialEffectOutcome.DecisionRequired,
            result.Outcome);
        Assert.Equal(reason.ToString(), result.Reason);
        Assert.Null(result.Reference);
    }

    [Theory]
    [InlineData(ExternalSettlementOutcome.AppliedDurable)]
    [InlineData(ExternalSettlementOutcome.AlreadyAppliedEquivalent)]
    public async Task Respuesta_positiva_candidata_permanece_bloqueada(
        ExternalSettlementOutcome outcome)
    {
        var adapter = new M03ExternalSettlementCandidatePortAdapter(
            SettlementCandidate.Returning(new(
                outcome,
                ExternalSettlementReason.RatifiedAndPersisted,
                "forbidden-durable-reference")));

        var result = await adapter.TryApplyAsync(
            PaymentTestData.CommercialCandidate(),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialEffectOutcome.DecisionRequired,
            result.Outcome);
        Assert.Equal(
            "m03_candidate_positive_outcome_not_authorized",
            result.Reason);
        Assert.Null(result.Reference);
        Assert.Null(result.ConfirmedFacts);
    }

    [Fact]
    public async Task Moneda_y_tres_identidades_se_proyectan_sin_normalizarse()
    {
        var evidence = PaymentTestData.Evidence(
            provider: " Provider-A ",
            eventId: " Event/01 ",
            settlementId: " Settlement/01 ");
        var facts = PaymentTestData.Facts(
            evidence,
            amount: 91.25m,
            currencyCode: "USD");
        var producer = SettlementCandidate.Returning(new(
            ExternalSettlementOutcome.PendingDecision,
            ExternalSettlementReason.CommercialPolicyUnratified));
        var adapter = new M03ExternalSettlementCandidatePortAdapter(producer);

        var result = await adapter.TryApplyAsync(
            PaymentTestData.CommercialCandidate(
                evidence: evidence,
                facts: facts,
                orderId: PaymentTestData.OrderId),
            CancellationToken);

        var request = producer.Request!;
        Assert.Equal(" Provider-A ", request.Provider);
        Assert.Equal(" Event/01 ", request.ProviderEventId);
        Assert.Equal(" Settlement/01 ", request.ExternalSettlementId);
        Assert.Equal(PaymentTestData.OrderId, request.OrderId);
        Assert.NotEqual(request.ProviderEventId, request.ExternalSettlementId);
        Assert.Equal(91.25m, request.Amount);
        Assert.Equal("USD", request.Currency);
        Assert.Equal(
            ProviderCommercialEffectOutcome.DecisionRequired,
            result.Outcome);
    }

    [Fact]
    public async Task Fallo_del_productor_es_indeterminado_y_no_acredita_efecto()
    {
        var adapter = new M03ExternalSettlementCandidatePortAdapter(
            SettlementCandidate.Throwing(new InvalidOperationException(
                "simulated-commercial-authority-outage")));

        var result = await adapter.TryApplyAsync(
            PaymentTestData.CommercialCandidate(),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            result.Outcome);
        Assert.Equal("m03_candidate_call_failed", result.Reason);
        Assert.Null(result.Reference);
    }

    [Fact]
    public void Evidencia_ausente_no_construye_candidato_ni_invoca_m03()
    {
        var producer = SettlementCandidate.Returning(new(
            ExternalSettlementOutcome.PendingDecision,
            ExternalSettlementReason.UnauthenticatedEvent));
        var adapter = new M03ExternalSettlementCandidatePortAdapter(producer);

        Assert.Throws<ArgumentException>(() =>
            PaymentTestData.CommercialCandidate(evidenceReference: " "));
        Assert.Equal(0, producer.Calls);
        Assert.NotNull(adapter);
    }

    [Fact]
    public void Adaptadores_no_se_registran_en_payments_module()
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

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType ==
                    typeof(IM03OrderCorrelationCandidatePort)
                || descriptor.ServiceType ==
                    typeof(IProviderCommercialEffectPort)
                || descriptor.ImplementationType ==
                    typeof(M03OrderCorrelationCandidatePortAdapter)
                || descriptor.ImplementationType ==
                    typeof(M03ExternalSettlementCandidatePortAdapter));
    }

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    private sealed class CorrelationCandidate(
        OrderCorrelationCandidate? result)
        : IOrderCorrelationCandidate
    {
        internal int Calls { get; private set; }
        internal OrderCorrelationRequest? Request { get; private set; }

        public Task<OrderCorrelationCandidate> ResolveAsync(
            OrderCorrelationRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            return Task.FromResult(result!);
        }
    }

    private sealed class SettlementCandidate : IExternalSettlementCandidate
    {
        private readonly ExternalSettlementCandidateResult? _result;
        private readonly Exception? _exception;

        private SettlementCandidate(
            ExternalSettlementCandidateResult? result,
            Exception? exception)
        {
            _result = result;
            _exception = exception;
        }

        internal static SettlementCandidate Returning(
            ExternalSettlementCandidateResult? result)
            => new(result, null);

        internal static SettlementCandidate Throwing(Exception exception)
            => new(null, exception);

        internal int Calls { get; private set; }
        internal ExternalSettlementCandidateEvent? Request { get; private set; }

        public Task<ExternalSettlementCandidateResult> TryApplyAsync(
            ExternalSettlementCandidateEvent candidate,
            CancellationToken cancellationToken)
        {
            Calls++;
            Request = candidate;
            return _exception is null
                ? Task.FromResult(_result!)
                : Task.FromException<ExternalSettlementCandidateResult>(
                    _exception);
        }
    }

    private sealed class DeclarationSource(
        IReadOnlyList<ProviderAuthenticatedOrderDeclaration> declarations)
        : IProviderAuthenticatedOrderDeclarationSource
    {
        public ValueTask<IReadOnlyList<ProviderAuthenticatedOrderDeclaration>>
            LoadAsync(
                Guid inboxEventId,
                CancellationToken cancellationToken)
            => ValueTask.FromResult(declarations);
    }

    private sealed class EvidenceReferenceSource(
        IReadOnlyList<ProviderVerifiedEvidenceReference> references)
        : IProviderVerifiedEvidenceReferenceSource
    {
        public ValueTask<IReadOnlyList<ProviderVerifiedEvidenceReference>>
            LoadAsync(
                Guid inboxEventId,
                CancellationToken cancellationToken)
            => ValueTask.FromResult(references);
    }
}
