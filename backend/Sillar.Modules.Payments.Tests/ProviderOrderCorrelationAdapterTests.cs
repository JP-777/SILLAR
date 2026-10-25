using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

public sealed class ProviderOrderCorrelationAdapterTests
{
    private static readonly Guid InboxId =
        Guid.Parse("0193a2b4-0000-7000-8000-000000000011");

    [Fact]
    public async Task Declaracion_autenticada_y_resolucion_persistida_producen_correlacion()
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = Reference(evidence);
        var declaration = Declaration(
            evidenceReference,
            orderCode: " P-2026-0147 ");
        var port = new CorrelationPort(Resolved());
        var adapter = Adapter([declaration], [evidenceReference], port);

        var correlation = await adapter.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.NotNull(correlation);
        Assert.Equal(PaymentTestData.OrderId, correlation.OrderId);
        Assert.Equal("P-2026-0147", correlation.OrderCode);
        Assert.Equal("sales.current_database", correlation.AuthorityReference);
        Assert.Equal(" P-2026-0147 ", port.Request!.DeclaredOrderCode);
        Assert.Null(port.Request.DeclaredOrderId);
        Assert.Equal(1, port.Calls);
    }

    [Theory]
    [InlineData((int)M03OrderCorrelationOutcome.NotAuthoritative)]
    [InlineData((int)M03OrderCorrelationOutcome.MalformedReference)]
    [InlineData((int)M03OrderCorrelationOutcome.UnknownReference)]
    [InlineData((int)M03OrderCorrelationOutcome.DivergentDeclaration)]
    public async Task Referencia_no_resuelta_jamas_produce_order_id(
        int outcomeValue)
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = Reference(evidence);
        var adapter = Adapter(
            [Declaration(evidenceReference)],
            [evidenceReference],
            new CorrelationPort(new(
                (M03OrderCorrelationOutcome)outcomeValue,
                null,
                null,
                "sales.current_database")));

        var correlation = await adapter.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.Null(correlation);
    }

    [Fact]
    public async Task Declaraciones_duplicadas_se_rechazan_sin_consultar_m03()
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = Reference(evidence);
        var declaration = Declaration(evidenceReference);
        var port = new CorrelationPort(Resolved());
        var adapter = Adapter(
            [declaration, declaration],
            [evidenceReference],
            port);

        var correlation = await adapter.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.Null(correlation);
        Assert.Equal(0, port.Calls);
    }

    [Fact]
    public async Task Declaracion_de_otra_recepcion_no_es_autoridad_para_el_inbox()
    {
        var evidence = PaymentTestData.Evidence();
        var persistedReference = Reference(evidence);
        var otherReference = ProviderVerifiedEvidenceReference.Create(
            Guid.CreateVersion7(),
            evidence,
            persistedReference.Value);
        var port = new CorrelationPort(Resolved());
        var adapter = Adapter(
            [Declaration(otherReference)],
            [persistedReference],
            port);

        var correlation = await adapter.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.Null(correlation);
        Assert.Equal(0, port.Calls);
    }

    [Fact]
    public async Task Identidad_provider_incompatible_no_llega_a_m03()
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = Reference(evidence);
        var port = new CorrelationPort(Resolved());
        var adapter = Adapter(
            [Declaration(evidenceReference)],
            [evidenceReference],
            port);
        var otherIdentity = PaymentIdempotencyKeys.From(
            PaymentTestData.Evidence(eventId: "event-other"));

        var correlation = await adapter.LoadAsync(
            InboxId,
            otherIdentity,
            CancellationToken);

        Assert.Null(correlation);
        Assert.Equal(0, port.Calls);
    }

    [Theory]
    [InlineData("sales.ratified_rules", "P-2026-0147")]
    [InlineData("sales.current_database", "P-2026-9999")]
    [InlineData("sales.current_database", null)]
    public async Task Respuesta_sin_autoridad_o_incompatible_se_rechaza(
        string source,
        string? resolvedOrderCode)
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = Reference(evidence);
        var adapter = Adapter(
            [Declaration(evidenceReference)],
            [evidenceReference],
            new CorrelationPort(new(
                M03OrderCorrelationOutcome.Resolved,
                PaymentTestData.OrderId,
                resolvedOrderCode,
                source)));

        var correlation = await adapter.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.Null(correlation);
    }

    [Fact]
    public async Task Order_id_declarado_y_resuelto_deben_coincidir()
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = Reference(evidence);
        var declaration = Declaration(
            evidenceReference,
            orderCode: null,
            orderId: Guid.Parse("0193a2b4-0000-7000-8000-000000000099"));
        var adapter = Adapter(
            [declaration],
            [evidenceReference],
            new CorrelationPort(Resolved()));

        var correlation = await adapter.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.Null(correlation);
    }

    [Fact]
    public async Task Order_id_autenticado_sigue_siendo_autoritativo_si_codigo_es_blanco()
    {
        var evidence = PaymentTestData.Evidence();
        var evidenceReference = Reference(evidence);
        var adapter = Adapter(
            [Declaration(
                evidenceReference,
                orderCode: " ",
                orderId: PaymentTestData.OrderId)],
            [evidenceReference],
            new CorrelationPort(Resolved()));

        var correlation = await adapter.LoadAsync(
            InboxId,
            PaymentIdempotencyKeys.From(evidence),
            CancellationToken);

        Assert.NotNull(correlation);
        Assert.Equal(PaymentTestData.OrderId, correlation.OrderId);
    }

    [Fact]
    public void Declaracion_sin_dato_autoritativo_no_puede_construirse()
    {
        var evidenceReference = Reference(PaymentTestData.Evidence());

        Assert.Throws<ArgumentException>(() =>
            ProviderAuthenticatedOrderDeclaration.Create(
                InboxId,
                evidenceReference,
                null,
                null));
        Assert.Throws<ArgumentException>(() =>
            ProviderAuthenticatedOrderDeclaration.Create(
                InboxId,
                evidenceReference,
                null,
                Guid.Empty));
    }

    private static ProviderAuthorizedOrderCorrelationAdapter Adapter(
        IReadOnlyList<ProviderAuthenticatedOrderDeclaration> declarations,
        IReadOnlyList<ProviderVerifiedEvidenceReference> evidenceReferences,
        CorrelationPort port)
        => new(
            new DeclarationSource(declarations),
            new EvidenceReferenceSource(evidenceReferences),
            port);

    private static ProviderVerifiedEvidenceReference Reference(
        VerifiedExternalPaymentEvidence evidence)
        => ProviderVerifiedEvidenceReference.Create(
            Guid.Parse("0193a2b4-0000-7000-8000-000000000012"),
            evidence,
            "attestation/provider-a/event-001");

    private static ProviderAuthenticatedOrderDeclaration Declaration(
        ProviderVerifiedEvidenceReference evidenceReference,
        string? orderCode = "P-2026-0147",
        Guid? orderId = null)
        => ProviderAuthenticatedOrderDeclaration.Create(
            InboxId,
            evidenceReference,
            orderCode,
            orderId);

    private static M03OrderCorrelationResultSnapshot Resolved()
        => new(
            M03OrderCorrelationOutcome.Resolved,
            PaymentTestData.OrderId,
            "P-2026-0147",
            "sales.current_database");

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

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

    private sealed class CorrelationPort(
        M03OrderCorrelationResultSnapshot? result)
        : IM03OrderCorrelationCandidatePort
    {
        internal int Calls { get; private set; }
        internal M03OrderCorrelationRequestSnapshot? Request { get; private set; }

        public ValueTask<M03OrderCorrelationResultSnapshot?> ResolveAsync(
            M03OrderCorrelationRequestSnapshot request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            return ValueTask.FromResult(result);
        }
    }
}
