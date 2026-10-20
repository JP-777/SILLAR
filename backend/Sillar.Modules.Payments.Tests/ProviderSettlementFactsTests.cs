using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

public sealed class ProviderSettlementFactsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PEN ")]
    [InlineData("pen")]
    [InlineData("PE1")]
    [InlineData("ÑEN")]
    public void Moneda_invalida_no_se_normaliza_ni_se_infiere(string? currency)
    {
        Assert.Throws<ArgumentException>(() => Facts(currencyCode: currency));
    }

    [Theory]
    [InlineData("PEN")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Moneda_iso_se_conserva_explicita(string currency)
    {
        var facts = Facts(currencyCode: currency);

        Assert.Equal(currency, facts.Currency.Value);
        Assert.Equal(
            ProviderSettlementAmountScope.FullOutstandingAmount,
            facts.AmountScope);
        Assert.Equal(0m, facts.AdditionalCustomerFee);
    }

    [Fact]
    public void Misma_identidad_y_hechos_es_duplicado_aunque_cambie_verified_at()
    {
        var known = Facts(verifiedAtOffset: 0);
        var replay = Facts(verifiedAtOffset: 1);

        Assert.Equal(
            ProviderSettlementFactDecision.Duplicate,
            ProviderSettlementFactClassifier.Classify(replay, [known]));
    }

    [Fact]
    public void Mismo_evento_con_importe_o_moneda_distintos_es_conflicto()
    {
        var known = Facts(amount: 125m, currencyCode: "PEN");
        var amountConflict = Facts(amount: 126m, currencyCode: "PEN");
        var currencyConflict = Facts(amount: 125m, currencyCode: "USD");

        Assert.Equal(
            ProviderSettlementFactDecision.Conflict,
            ProviderSettlementFactClassifier.Classify(amountConflict, [known]));
        Assert.Equal(
            ProviderSettlementFactDecision.Conflict,
            ProviderSettlementFactClassifier.Classify(currencyConflict, [known]));
    }

    [Fact]
    public void Conflicto_posterior_prevalece_sobre_duplicado_temprano()
    {
        var incoming = Facts();
        var duplicate = Facts(verifiedAtOffset: 1);
        var conflict = Facts(amount: 999m);

        Assert.Equal(
            ProviderSettlementFactDecision.Conflict,
            ProviderSettlementFactClassifier.Classify(
                incoming,
                [duplicate, conflict]));
    }

    [Fact]
    public void Misma_liquidacion_en_otro_proveedor_requiere_decision()
    {
        var known = Facts(provider: "provider-a");
        var incoming = Facts(provider: "provider-b");

        Assert.Equal(
            ProviderSettlementFactDecision.DecisionRequired,
            ProviderSettlementFactClassifier.Classify(incoming, [known]));
    }

    [Fact]
    public void Identidad_y_liquidacion_nuevas_no_se_presentan_como_aplicadas()
    {
        var incoming = Facts(eventId: "event-new", settlementId: "settlement-new");

        Assert.Equal(
            ProviderSettlementFactDecision.New,
            ProviderSettlementFactClassifier.Classify(incoming, []));
    }

    private static ProviderSettlementFacts Facts(
        string provider = "provider-a",
        string eventId = "event-001",
        string settlementId = "settlement-001",
        decimal amount = 125m,
        string? currencyCode = "PEN",
        int verifiedAtOffset = 0)
    {
        var evidence = VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
            provider,
            eventId,
            settlementId,
            new string('a', 64),
            PaymentTestData.VerifiedAt.AddSeconds(verifiedAtOffset));
        return ProviderSettlementFacts.Create(
            evidence,
            amount,
            currencyCode,
            PaymentTestData.VerifiedAt.AddMinutes(1));
    }
}
