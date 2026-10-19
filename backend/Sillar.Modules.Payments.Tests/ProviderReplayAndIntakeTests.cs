using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Replay, contradicciones y seleccion determinista de P2-P4.</summary>
public sealed class ProviderReplayAndIntakeTests
{
    [Fact]
    public void Reentrega_exacta_es_duplicado()
    {
        var incoming = PaymentTestData.Evidence();

        var result = ProviderReplayClassifier.Classify(incoming, [incoming]);

        Assert.Equal(ProviderReplayDecision.DuplicateEvent, result);
    }

    [Fact]
    public void Mismo_evento_con_otro_body_es_conflicto()
    {
        var incoming = PaymentTestData.Evidence(hashDigit: 'a');
        var previous = PaymentTestData.Evidence(hashDigit: 'b');

        var result = ProviderReplayClassifier.Classify(incoming, [previous]);

        Assert.Equal(ProviderReplayDecision.ConflictingEvent, result);
    }

    [Fact]
    public void Mismo_evento_con_otra_liquidacion_es_conflicto()
    {
        var incoming = PaymentTestData.Evidence(settlementId: "settlement-001");
        var previous = PaymentTestData.Evidence(settlementId: "settlement-002");

        Assert.Equal(
            ProviderReplayDecision.ConflictingEvent,
            ProviderReplayClassifier.Classify(incoming, [previous]));
    }

    [Fact]
    public void Eventos_distintos_de_una_liquidacion_quedan_relacionados()
    {
        var incoming = PaymentTestData.Evidence(eventId: "event-002");
        var previous = PaymentTestData.Evidence(eventId: "event-001");

        Assert.Equal(
            ProviderReplayDecision.RelatedSettlement,
            ProviderReplayClassifier.Classify(incoming, [previous]));
    }

    [Fact]
    public void Misma_liquidacion_en_otro_proveedor_falla_cerrado()
    {
        var incoming = PaymentTestData.Evidence(provider: "provider-a");
        var previous = PaymentTestData.Evidence(provider: "provider-b");

        Assert.Equal(
            ProviderReplayDecision.ConflictingSettlement,
            ProviderReplayClassifier.Classify(incoming, [previous]));
    }

    [Fact]
    public void Conflicto_posterior_prevalece_sobre_duplicado_temprano()
    {
        var incoming = PaymentTestData.Evidence();
        var conflict = PaymentTestData.Evidence(hashDigit: 'b');

        Assert.Equal(
            ProviderReplayDecision.ConflictingEvent,
            ProviderReplayClassifier.Classify(incoming, [incoming, conflict]));
    }

    [Fact]
    public void Lote_detecta_cuerpos_contradictorios_bajo_las_mismas_claves()
    {
        var first = PaymentTestData.Evidence(hashDigit: 'a');
        var second = PaymentTestData.Evidence(hashDigit: 'b');

        var result = ProviderReplayBatchAnalyzer.Analyze([first, second], []);

        var item = Assert.Single(result.Items);
        Assert.Equal(2, item.Occurrences);
        Assert.Equal(ProviderReplayDecision.ConflictingEvent, item.Decision);
        Assert.True(result.HasConflicts);
    }

    [Fact]
    public void Plan_elije_un_solo_candidato_por_proveedor_y_liquidacion()
    {
        var eventB = PaymentTestData.Evidence(eventId: "event-b", hashDigit: 'b');
        var eventA = PaymentTestData.Evidence(eventId: "event-a", hashDigit: 'a');

        var result = ProviderIntakePlanner.CreatePlan([eventB, eventA], []);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.CandidatesAwaitingAuthentication);
        Assert.Equal("event-a", result.Items[0].Identity.Reception.ProviderEventId);
        Assert.Equal(
            ProviderIntakeDisposition.CandidateRequiringIndependentAuthentication,
            result.Items[0].Disposition);
        Assert.Equal(
            ProviderIntakeDisposition.RelatesToCandidateInBatch,
            result.Items[1].Disposition);
        Assert.All(result.Items, item => Assert.True(item.RequiresIndependentAuthentication));
    }

    [Fact]
    public void Un_conflicto_retiene_todo_el_grupo_de_liquidacion()
    {
        var eventA = PaymentTestData.Evidence(eventId: "event-a", hashDigit: 'a');
        var contradictoryA = PaymentTestData.Evidence(eventId: "event-a", hashDigit: 'b');
        var eventB = PaymentTestData.Evidence(eventId: "event-b", hashDigit: 'c');

        var result = ProviderIntakePlanner.CreatePlan(
            [eventA, contradictoryA, eventB],
            []);

        Assert.True(result.HasConflicts);
        Assert.All(
            result.Items,
            item => Assert.Equal(
                ProviderIntakeDisposition.HoldForConflict,
                item.Disposition));
    }

    [Fact]
    public void Clasificadores_rechazan_colecciones_con_entradas_nulas()
    {
        var incoming = PaymentTestData.Evidence();

        Assert.Throws<ArgumentNullException>(() =>
            ProviderReplayClassifier.Classify(incoming, [null!]));
        Assert.Throws<ArgumentNullException>(() =>
            ProviderReplayBatchAnalyzer.Analyze([incoming, null!], []));
    }
}
