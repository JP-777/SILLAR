using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Invariantes de origen, atribucion e identidad externa de P1.</summary>
public sealed class PaymentSourceAndEvidenceTests
{
    [Fact]
    public void Staff_conserva_el_trio_humano_completo_y_normalizado()
    {
        var attribution = StaffPaymentAttribution.Create(
            "  Ana Quispe  ", 17, "  principal  ");
        var source = new PaymentSource.Staff(attribution);

        Assert.Equal(PaymentSource.Codes.Staff, source.Code);
        Assert.Equal("Ana Quispe", source.Attribution.Name);
        Assert.Equal(17, source.Attribution.LocalId);
        Assert.Equal("principal", source.Attribution.HomeNode);
    }

    [Theory]
    [InlineData(null, 1, "principal")]
    [InlineData("", 1, "principal")]
    [InlineData("Ana", 0, "principal")]
    [InlineData("Ana", -1, "principal")]
    [InlineData("Ana", 1, null)]
    [InlineData("Ana", 1, "  ")]
    public void Staff_rechaza_cualquier_atribucion_incompleta(
        string? name,
        int localId,
        string? homeNode)
        => Assert.Throws<ArgumentException>(() =>
            StaffPaymentAttribution.Create(name, localId, homeNode));

    [Fact]
    public void Provider_solo_transporta_evidencia_externa_verificada()
    {
        var evidence = PaymentTestData.Evidence();
        var source = new PaymentSource.Provider(evidence);

        Assert.Equal(PaymentSource.Codes.Provider, source.Code);
        Assert.Same(evidence, source.Evidence);
        Assert.DoesNotContain(
            source.GetType().GetProperties(),
            property => property.Name.Contains("Staff", StringComparison.Ordinal)
                || property.Name.Contains("Human", StringComparison.Ordinal));
    }

    [Fact]
    public void Evidencia_conserva_identidades_opacas_y_normaliza_solo_el_hash()
    {
        var evidence = VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
            " provider-a ",
            " event-001 ",
            " settlement-001 ",
            new string('A', 64),
            PaymentTestData.VerifiedAt);

        Assert.Equal(" provider-a ", evidence.Provider);
        Assert.Equal(" event-001 ", evidence.ProviderEventId);
        Assert.Equal(" settlement-001 ", evidence.ExternalSettlementId);
        Assert.Equal(new string('a', 64), evidence.BodySha256);
        Assert.DoesNotContain(
            evidence.GetType().GetProperties(),
            property => property.Name.Contains("Body", StringComparison.Ordinal)
                && property.Name != nameof(VerifiedExternalPaymentEvidence.BodySha256));
    }

    [Theory]
    [InlineData(null, "event", "settlement", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("provider", null, "settlement", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("provider", "event", null, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("provider", "event", "settlement", "abc")]
    [InlineData("provider", "event", "settlement", "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Evidencia_rechaza_identidades_o_hash_invalidos(
        string? provider,
        string? eventId,
        string? settlementId,
        string? hash)
        => Assert.Throws<ArgumentException>(() =>
            VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
                provider,
                eventId,
                settlementId,
                hash,
                PaymentTestData.VerifiedAt));

    [Fact]
    public void Evidencia_rechaza_instante_no_informado()
        => Assert.Throws<ArgumentException>(() =>
            VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
                "provider", "event", "settlement", new string('a', 64), default));

    [Fact]
    public void Claves_de_recepcion_y_liquidacion_permanecen_separadas()
    {
        var first = PaymentIdempotencyKeys.From(PaymentTestData.Evidence());
        var otherProvider = PaymentIdempotencyKeys.From(PaymentTestData.Evidence(
            provider: "provider-b"));

        Assert.NotEqual(first.Reception, otherProvider.Reception);
        Assert.Equal(first.Settlement, otherProvider.Settlement);
        Assert.Equal("provider-a", first.Reception.Provider);
        Assert.Equal("event-001", first.Reception.ProviderEventId);
        Assert.Equal("settlement-001", first.Settlement.Value);
    }

    [Fact]
    public void Claves_opacas_no_colapsan_con_sus_variantes_recortadas()
    {
        var exact = PaymentIdempotencyKeys.From(PaymentTestData.Evidence(
            provider: " provider-a ",
            eventId: " event-001 ",
            settlementId: " settlement-001 "));
        var trimmed = PaymentIdempotencyKeys.From(PaymentTestData.Evidence());

        Assert.NotEqual(exact.Reception, trimmed.Reception);
        Assert.NotEqual(exact.Settlement, trimmed.Settlement);
        Assert.Equal(" provider-a ", exact.Reception.Provider);
        Assert.Equal(" event-001 ", exact.Reception.ProviderEventId);
        Assert.Equal(" settlement-001 ", exact.Settlement.Value);
    }
}
