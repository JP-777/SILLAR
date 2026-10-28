using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Determinismo, huellas y coherencia integral de P8-P16.</summary>
public sealed class ProviderPipelineIntegrityTests
{
    [Fact]
    public void Permutar_entradas_no_cambia_huellas_ni_traza_canonica()
    {
        var eventA = PaymentTestData.Evidence(eventId: "event-a", hashDigit: 'a');
        var eventB = PaymentTestData.Evidence(eventId: "event-b", hashDigit: 'b');
        var observations = new[]
        {
            PaymentTestData.Attested(eventA, "ref-a"),
            PaymentTestData.Attested(eventB, "ref-b")
        };

        var first = ProviderSettlementAnalysisPipeline.Analyze(
            [eventB, eventA], [], observations.Reverse());
        var second = ProviderSettlementAnalysisPipeline.Analyze(
            [eventA, eventB], [], observations);

        var firstIntegrity = ProviderSettlementAnalysisIntegrity.Create(first);
        var secondIntegrity = ProviderSettlementAnalysisIntegrity.Create(second);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(firstIntegrity, secondIntegrity);
        Assert.Equal(
            first.Trace.Select(item => item.Identity),
            second.Trace.Select(item => item.Identity));
        Assert.Equal(
            ["event-a", "event-b"],
            first.Trace.Select(item => item.Identity.Reception.ProviderEventId));
    }

    [Fact]
    public void Pipeline_cubre_inventario_conflictos_traza_auditoria_y_expediente()
    {
        var incoming = PaymentTestData.Evidence();

        var result = ProviderSettlementAnalysisPipeline.Analyze(
            [incoming], [], [PaymentTestData.Attested(incoming)]);
        var integrity = ProviderSettlementAnalysisIntegrity.Create(result);
        var casebook = ProviderSettlementReviewCasebook.Create(result);

        Assert.Equal(1, result.EvidenceInventory.IncomingOccurrences);
        Assert.Empty(result.ConflictReport.Cases);
        Assert.Single(result.Trace);
        Assert.Equal(result.Fingerprint.ContentSha256, integrity.ReviewAuditSha256);
        Assert.Equal(1, integrity.DistinctEvidenceItems);
        Assert.Equal(1, integrity.TraceItems);
        Assert.Equal(0, integrity.ConflictCases);
        Assert.Single(casebook.Cases);
        Assert.Equal(integrity.ContentSha256, casebook.SourceAnalysisSha256);
        Assert.True(Assert.Single(casebook.Cases).RequiresIndependentAuthentication);
    }

    [Fact]
    public void Fingerprint_de_auditoria_es_independiente_del_orden_de_filas()
    {
        var rowA = AuditRow("provider-a", "settlement-a");
        var rowB = AuditRow("provider-b", "settlement-b");
        var auditA = Audit([rowA, rowB]);
        var auditB = Audit([rowB, rowA]);

        Assert.Equal(
            ProviderSettlementReviewFingerprint.Create(auditA),
            ProviderSettlementReviewFingerprint.Create(auditB));
    }

    [Fact]
    public void Fingerprint_cambia_si_cambia_un_hecho_auditado()
    {
        var original = Audit([AuditRow("provider-a", "settlement-a")]);
        var changed = Audit([AuditRow(
            "provider-a", "settlement-a", incomingOccurrences: 2)]);

        Assert.NotEqual(
            ProviderSettlementReviewFingerprint.Create(original).ContentSha256,
            ProviderSettlementReviewFingerprint.Create(changed).ContentSha256);
    }

    [Fact]
    public void Fingerprint_rechaza_totales_que_no_corresponden_a_las_filas()
    {
        var coherent = Audit([AuditRow("provider-a", "settlement-a")]);
        var tampered = coherent with { TotalGroups = 2 };

        Assert.Throws<ArgumentException>(() =>
            ProviderSettlementReviewFingerprint.Create(tampered));
    }

    [Fact]
    public void Integridad_rechaza_huella_que_no_pertenece_al_analisis()
    {
        var incoming = PaymentTestData.Evidence();
        var result = ProviderSettlementAnalysisPipeline.Analyze(
            [incoming], [], [PaymentTestData.Attested(incoming)]);
        var tampered = result with
        {
            Fingerprint = result.Fingerprint with
            {
                ContentSha256 = new string('0', 64)
            }
        };

        Assert.Throws<ArgumentException>(() =>
            ProviderSettlementAnalysisIntegrity.Create(tampered));
    }

    [Fact]
    public void Integridad_rechaza_traza_que_libera_un_hold()
    {
        var incoming = PaymentTestData.Evidence(hashDigit: 'a');
        var conflicting = PaymentTestData.Evidence(hashDigit: 'b');
        var result = ProviderSettlementAnalysisPipeline.Analyze(
            [incoming],
            [conflicting],
            [PaymentTestData.Attested(incoming)]);
        var held = Assert.Single(result.Trace);
        var tampered = result with
        {
            Trace = [held with
            {
                ReviewLane = ProviderSettlementReviewLane.RequiresSeparateDurableGate,
                HoldReason = ProviderSettlementReviewHoldReason.None
            }]
        };

        Assert.Throws<ArgumentException>(() =>
            ProviderSettlementAnalysisIntegrity.Create(tampered));
    }

    [Fact]
    public void Comparar_instantaneas_equivalentes_no_inventa_cambios()
    {
        var eventA = PaymentTestData.Evidence(eventId: "event-a", hashDigit: 'a');
        var eventB = PaymentTestData.Evidence(eventId: "event-b", hashDigit: 'b');
        var first = ProviderSettlementAnalysisPipeline.Analyze(
            [eventB, eventA],
            [],
            [PaymentTestData.Attested(eventA, "ref-a"),
             PaymentTestData.Attested(eventB, "ref-b")]);
        var second = ProviderSettlementAnalysisPipeline.Analyze(
            [eventA, eventB],
            [],
            [PaymentTestData.Attested(eventB, "ref-b"),
             PaymentTestData.Attested(eventA, "ref-a")]);

        var comparison = ProviderSettlementReviewComparison.Compare(first, second);

        Assert.False(comparison.HasChanges);
        Assert.Empty(comparison.Changes);
        Assert.Equal(
            comparison.EarlierAnalysisSha256,
            comparison.LaterAnalysisSha256);
    }

    [Fact]
    public void Comparar_instantaneas_detecta_evento_y_evidencia_agregados()
    {
        var eventA = PaymentTestData.Evidence(eventId: "event-a", hashDigit: 'a');
        var eventB = PaymentTestData.Evidence(eventId: "event-b", hashDigit: 'b');
        var earlier = ProviderSettlementAnalysisPipeline.Analyze(
            [eventA], [], [PaymentTestData.Attested(eventA, "ref-a")]);
        var later = ProviderSettlementAnalysisPipeline.Analyze(
            [eventA, eventB],
            [],
            [PaymentTestData.Attested(eventA, "ref-a"),
             PaymentTestData.Attested(eventB, "ref-b")]);

        var comparison = ProviderSettlementReviewComparison.Compare(earlier, later);

        var change = Assert.Single(comparison.Changes);
        Assert.Equal(ProviderSettlementReviewDeltaKind.Changed, change.Kind);
        Assert.True(change.Dimensions.HasFlag(
            ProviderSettlementReviewChangeDimension.EventIdentities));
        Assert.True(change.Dimensions.HasFlag(
            ProviderSettlementReviewChangeDimension.Evidence));
        Assert.True(change.Dimensions.HasFlag(
            ProviderSettlementReviewChangeDimension.EvidenceInventory));
        Assert.Equal(1, comparison.ChangedCases);
    }

    [Fact]
    public void Pipeline_rechaza_entradas_nulas_en_cada_frontera()
    {
        var evidence = PaymentTestData.Evidence();

        Assert.Throws<ArgumentNullException>(() =>
            ProviderSettlementAnalysisPipeline.Analyze(null!, [], []));
        Assert.Throws<ArgumentNullException>(() =>
            ProviderSettlementAnalysisPipeline.Analyze([], null!, []));
        Assert.Throws<ArgumentNullException>(() =>
            ProviderSettlementAnalysisPipeline.Analyze([], [], null!));
        Assert.Throws<ArgumentException>(() =>
            ProviderSettlementAnalysisPipeline.Analyze([evidence, null!], [], []));
    }

    [Fact]
    public void Pipeline_con_volumen_conserva_todos_los_holds_interproveedor()
    {
        var incoming = Enumerable.Range(0, 512)
            .SelectMany(index => new[]
            {
                PaymentTestData.Evidence(
                    provider: "provider-a",
                    eventId: $"event-a-{index:D4}",
                    settlementId: $"settlement-{index:D4}",
                    hashDigit: 'a'),
                PaymentTestData.Evidence(
                    provider: "provider-b",
                    eventId: $"event-b-{index:D4}",
                    settlementId: $"settlement-{index:D4}",
                    hashDigit: 'b')
            })
            .ToArray();
        var observations = incoming
            .Select((item, index) =>
                PaymentTestData.Attested(item, $"ref-{index:D4}"))
            .ToArray();

        var result = ProviderSettlementAnalysisPipeline.Analyze(
            incoming,
            [],
            observations);

        Assert.Equal(1024, result.Trace.Count);
        Assert.Equal(512, result.ConflictReport.Cases.Count);
        Assert.Equal(512, result.ReviewPlan.CrossProviderSettlementIds.Count);
        Assert.All(result.Trace, item =>
        {
            Assert.True(item.IsHeld);
            Assert.Equal(
                ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved,
                item.HoldReason);
        });
    }

    private static ProviderReviewAuditRow AuditRow(
        string provider,
        string settlement,
        long incomingOccurrences = 1)
        => new(
            provider,
            settlement,
            ProviderReviewAuditSeverity.SeparateDurableReview,
            ProviderSettlementReviewLane.RequiresSeparateDurableGate,
            ProviderSettlementReviewHoldReason.None,
            1,
            incomingOccurrences);

    private static ProviderReviewAuditResult Audit(
        IReadOnlyList<ProviderReviewAuditRow> rows)
    {
        var providers = rows
            .GroupBy(row => row.Provider, StringComparer.Ordinal)
            .Select(group => new ProviderReviewAuditProviderSummary(
                group.Key,
                group.Count(),
                0,
                0,
                0,
                group.Count(),
                group.Sum(row => row.IncomingOccurrences)))
            .ToArray();

        return new ProviderReviewAuditResult(
            rows,
            providers,
            rows.Count,
            0,
            0,
            0,
            0,
            rows.Count);
    }
}
