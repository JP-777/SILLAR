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
    public void Evento_opaco_con_espacios_no_es_el_evento_recortado()
    {
        var incoming = PaymentTestData.Evidence(eventId: " event-001 ");
        var previous = PaymentTestData.Evidence(eventId: "event-001");

        Assert.Equal(
            ProviderReplayDecision.RelatedSettlement,
            ProviderReplayClassifier.Classify(incoming, [previous]));
    }

    [Fact]
    public void Liquidacion_opaca_con_espacios_no_es_la_liquidacion_recortada()
    {
        var incoming = PaymentTestData.Evidence(
            eventId: "event-002",
            settlementId: " settlement-001 ");
        var previous = PaymentTestData.Evidence(
            eventId: "event-001",
            settlementId: "settlement-001");

        Assert.Equal(
            ProviderReplayDecision.NewEvent,
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

    [Fact]
    public void Lote_optimizado_conserva_la_clasificacion_de_referencia()
    {
        var incoming = Enumerable.Range(0, 240)
            .Select(index => PaymentTestData.Evidence(
                provider: $"provider-{index % 5}",
                eventId: $"event-{index % 120:D3}",
                settlementId: $"settlement-{index % 80:D3}",
                hashDigit: "0123456789abcdef"[index % 16]))
            .ToList();
        incoming.AddRange(incoming.Take(20));
        incoming.AddRange(incoming.Take(10).Select(item =>
            PaymentTestData.Evidence(
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256[0] == 'f' ? 'e' : 'f')));
        var known = Enumerable.Range(0, 90)
            .Select(index => PaymentTestData.Evidence(
                provider: $"provider-{index % 4}",
                eventId: $"event-{index % 70:D3}",
                settlementId: $"settlement-{index % 60:D3}",
                hashDigit: "fedcba9876543210"[index % 16]))
            .ToArray();

        var expected = AnalyzeByReference(incoming, known);
        var actual = ProviderReplayBatchAnalyzer.Analyze(incoming, known);

        Assert.Equal(expected.Items, actual.Items);
        Assert.Equal(expected.HasConflicts, actual.HasConflicts);
    }

    [Fact]
    public void Lote_de_volumen_es_reproducible_en_orden_inverso()
    {
        var incoming = Enumerable.Range(0, 4096)
            .Select(index => PaymentTestData.Evidence(
                eventId: $"event-{index:D5}",
                settlementId: $"settlement-{index:D5}",
                hashDigit: "0123456789abcdef"[index % 16]))
            .ToArray();
        var known = incoming
            .Where((_, index) => index % 4 == 0)
            .Take(512)
            .ToArray();

        var forward = ProviderReplayBatchAnalyzer.Analyze(incoming, known);
        var reversed = ProviderReplayBatchAnalyzer.Analyze(
            incoming.Reverse(),
            known.Reverse());

        Assert.Equal(4096, forward.Items.Count);
        Assert.Equal(forward.Items, reversed.Items);
        Assert.False(forward.HasConflicts);
        Assert.Equal(
            512,
            forward.Items.Count(item =>
                item.Decision == ProviderReplayDecision.DuplicateEvent));
        Assert.Equal(
            3584,
            forward.Items.Count(item =>
                item.Decision == ProviderReplayDecision.NewEvent));
    }

    private static ProviderReplayBatchResult AnalyzeByReference(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known)
    {
        var history = known.ToArray();
        var unique = incoming
            .GroupBy(PaymentIdempotencyKeys.From)
            .Select(group => new
            {
                Identity = group.Key,
                Representative = group.First(),
                Occurrences = group.Count(),
                ContradictoryBody = group
                    .Select(item => item.BodySha256)
                    .Distinct(StringComparer.Ordinal)
                    .Skip(1)
                    .Any()
            })
            .OrderBy(item => item.Identity.Reception.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.Identity.Reception.ProviderEventId, StringComparer.Ordinal)
            .ThenBy(item => item.Identity.Settlement.Value, StringComparer.Ordinal)
            .ToArray();
        var result = new List<ProviderReplayBatchItem>(unique.Length);

        for (var index = 0; index < unique.Length; index++)
        {
            var current = unique[index];
            var comparisons = history
                .Concat(unique
                    .Where((_, otherIndex) => otherIndex != index)
                    .Select(item => item.Representative))
                .ToList();
            if (current.Occurrences > 1)
            {
                comparisons.Add(current.Representative);
            }

            var decision = current.ContradictoryBody
                ? ProviderReplayDecision.ConflictingEvent
                : ProviderReplayClassifier.Classify(
                    current.Representative,
                    comparisons);
            result.Add(new(
                current.Identity,
                current.Occurrences,
                decision));
        }

        return new(
            result,
            result.Any(item => item.Decision is
                ProviderReplayDecision.ConflictingEvent
                    or ProviderReplayDecision.ConflictingSettlement));
    }
}
