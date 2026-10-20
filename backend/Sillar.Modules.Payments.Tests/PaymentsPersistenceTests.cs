using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Infrastructure.Persistence;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Invariantes del modelo local de inbox, sin abrir una conexión.</summary>
public sealed class PaymentsPersistenceTests
{
    [Fact]
    public void Contexto_usa_schema_payments_y_solo_tablas_tecnicas_propias()
    {
        using var context = Context();
        var model = context.Model;

        Assert.Equal("payments", model.GetDefaultSchema());
        Assert.Equal(
            [
                "provider_evidence_receipts",
                "provider_inbox_events",
                "provider_processing_attempts"
            ],
            model.GetEntityTypes()
                .Select(type => type.GetTableName())
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            model.GetEntityTypes(),
            type => type.GetSchema() == "sales");
    }

    [Fact]
    public void Inbox_deduplica_por_proveedor_y_evento_pero_no_libera_liquidaciones_cruzadas()
    {
        using var context = Context();
        var entity = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(ProviderInboxEvent))!;
        var indexes = entity.GetIndexes().ToArray();
        var eventIdentity = Assert.Single(indexes, index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(ProviderInboxEvent.Provider),
                    nameof(ProviderInboxEvent.ProviderEventId)
                ]));
        var settlement = Assert.Single(indexes, index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ProviderInboxEvent.ExternalSettlementId)]));

        Assert.True(eventIdentity.IsUnique);
        Assert.False(settlement.IsUnique);
        Assert.Equal(
            "uq_payment_inbox_provider_event",
            eventIdentity.GetDatabaseName());
        Assert.All(
            eventIdentity.Properties,
            property => Assert.Equal(
                PaymentsDbContext.OpaqueIdentifierCollation,
                property.GetCollation()));
    }

    [Fact]
    public void Identificadores_externos_declaran_colacion_determinista_explicita()
    {
        using var context = Context();
        var model = context.GetService<IDesignTimeModel>().Model;
        var inbox = model.FindEntityType(typeof(ProviderInboxEvent))!;
        var receipt = model.FindEntityType(typeof(ProviderEvidenceReceipt))!;

        AssertOpaqueCollation(
            inbox,
            nameof(ProviderInboxEvent.Provider),
            nameof(ProviderInboxEvent.ProviderEventId),
            nameof(ProviderInboxEvent.ExternalSettlementId),
            nameof(ProviderInboxEvent.CommercialEffectReference));
        AssertOpaqueCollation(
            receipt,
            nameof(ProviderEvidenceReceipt.Provider),
            nameof(ProviderEvidenceReceipt.ProviderEventId),
            nameof(ProviderEvidenceReceipt.ExternalSettlementId),
            nameof(ProviderEvidenceReceipt.AttestationReference));
    }

    [Theory]
    [InlineData("Provider-A", "Event-ABC", "Settlement-XYZ")]
    [InlineData("provider-a", "event-abc", "settlement-xyz")]
    [InlineData(" provider:#/Á ", " event:?&=ñ ", " settlement:+%@ü ")]
    public void Persistencia_conserva_identidades_opacas_sin_normalizarlas(
        string provider,
        string providerEventId,
        string externalSettlementId)
    {
        var evidence = VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
            provider,
            providerEventId,
            externalSettlementId,
            new string('a', 64),
            PaymentTestData.VerifiedAt);
        var inbox = ProviderInboxEvent.Create(
            evidence,
            PaymentTestData.VerifiedAt);
        var receipt = ProviderEvidenceReceipt.Receive(
            Guid.CreateVersion7(),
            provider,
            providerEventId,
            externalSettlementId,
            new string('b', 64),
            PaymentTestData.VerifiedAt);

        Assert.Equal(provider, inbox.Provider);
        Assert.Equal(providerEventId, inbox.ProviderEventId);
        Assert.Equal(externalSettlementId, inbox.ExternalSettlementId);
        Assert.Equal(provider, receipt.Provider);
        Assert.Equal(providerEventId, receipt.ProviderEventId);
        Assert.Equal(externalSettlementId, receipt.ExternalSettlementId);
    }

    [Fact]
    public void Modelo_no_contiene_body_crudo_secreto_ni_atribucion_humana()
    {
        using var context = Context();
        var forbidden = new[]
        {
            "RawBody", "Payload", "Secret", "Pan", "Cvv",
            "StaffName", "AdminUser", "HomeNode"
        };

        Assert.DoesNotContain(
            context.Model.GetEntityTypes().SelectMany(type => type.GetProperties()),
            property => forbidden.Any(part => property.Name.Contains(
                part,
                StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Checks_separan_recepcion_autenticacion_lease_y_efecto_comercial()
    {
        using var context = Context();
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var receipt = designModel.FindEntityType(typeof(ProviderEvidenceReceipt))!;
        var inbox = designModel.FindEntityType(typeof(ProviderInboxEvent))!;

        Assert.Contains(
            receipt.GetCheckConstraints(),
            check => check.Name == "ck_payment_receipts_authentication"
                && check.Sql.Contains("attestation_reference", StringComparison.Ordinal)
                && check.Sql.Contains("inbox_event_id", StringComparison.Ordinal));
        Assert.Contains(
            inbox.GetCheckConstraints(),
            check => check.Name == "ck_payment_inbox_lease"
                && check.Sql.Contains("lease_token", StringComparison.Ordinal));
        Assert.Contains(
            inbox.GetCheckConstraints(),
            check => check.Name == "ck_payment_inbox_commercial_reference"
                && check.Sql.Contains("already_applied", StringComparison.Ordinal));
        Assert.Contains(
            receipt.GetCheckConstraints(),
            check => check.Name == "ck_payment_receipts_state_alignment"
                && check.Sql.Contains(
                    "verification_state = 'authenticated'",
                    StringComparison.Ordinal));
        Assert.Contains(
            receipt.GetCheckConstraints(),
            check => check.Name == "ck_payment_receipts_analysis_sha256"
                && check.Sql.Contains("COLLATE \"C\"", StringComparison.Ordinal));
        Assert.Contains(
            inbox.GetCheckConstraints(),
            check => check.Name == "ck_payment_inbox_retry"
                && check.Sql.Contains(
                    "next_attempt_at > updated_at",
                    StringComparison.Ordinal));
        Assert.Contains(
            inbox.GetCheckConstraints(),
            check => check.Name == "ck_payment_inbox_commercial_hold"
                && check.Sql.Contains(
                    "'conflict','decision_required'",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Modelo_impide_mas_de_un_intento_running_por_inbox()
    {
        using var context = Context();
        var attempt = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(ProviderProcessingAttempt))!;
        var running = Assert.Single(attempt.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ProviderProcessingAttempt.InboxEventId)])
            && index.GetFilter() is not null);

        Assert.True(running.IsUnique);
        Assert.Equal("outcome = 'running'", running.GetFilter());
        Assert.Equal(
            "uq_payment_attempt_running_inbox",
            running.GetDatabaseName());
    }

    [Fact]
    public void Recepcion_conserva_identidades_opacas_y_no_puede_reclasificarse()
    {
        var receipt = ProviderEvidenceReceipt.Receive(
            Guid.CreateVersion7(),
            " provider ",
            " event ",
            " settlement ",
            new string('A', 64),
            PaymentTestData.VerifiedAt);
        var inboxId = Guid.CreateVersion7();

        receipt.MarkAuthenticated(
            inboxId,
            PaymentTestData.VerifiedAt.AddSeconds(1),
            " attestation ",
            ProviderReceptionStates.PendingDurableReview,
            new string('B', 64));

        Assert.Equal(" provider ", receipt.Provider);
        Assert.Equal(" event ", receipt.ProviderEventId);
        Assert.Equal(" settlement ", receipt.ExternalSettlementId);
        Assert.Equal(new string('a', 64), receipt.BodySha256);
        Assert.Equal(ProviderVerificationStates.Authenticated, receipt.VerificationState);
        Assert.Equal(" attestation ", receipt.AttestationReference);
        Assert.Equal(
            ProviderReceptionStates.PendingDurableReview,
            receipt.ReceptionState);
        Assert.Equal(new string('b', 64), receipt.AnalysisSha256);
        Assert.Throws<InvalidOperationException>(() =>
            receipt.MarkRejected("late-rejection"));
    }

    [Fact]
    public void Revision_del_inbox_es_token_de_concurrencia_y_avanza_con_transiciones()
    {
        using var context = Context();
        var entityType = context.Model.FindEntityType(typeof(ProviderInboxEvent))!;
        var revision = entityType.FindProperty(nameof(ProviderInboxEvent.Revision))!;
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);

        Assert.True(revision.IsConcurrencyToken);
        Assert.Equal(1, inbox.Revision);

        inbox.MarkReady(now.AddSeconds(1));
        Assert.Equal(2, inbox.Revision);

        inbox.BeginAttempt(
            Guid.CreateVersion7(),
            now.AddMinutes(1),
            now.AddSeconds(2));
        Assert.Equal(3, inbox.Revision);
    }

    [Fact]
    public void Hold_interproveedor_no_puede_habilitarse_automaticamente()
    {
        var inbox = ProviderInboxEvent.Create(
            PaymentTestData.Evidence(),
            PaymentTestData.VerifiedAt);
        inbox.Hold(
            "cross_provider_identity_unresolved",
            PaymentTestData.VerifiedAt.AddSeconds(1));

        Assert.Equal(ProviderInboxStates.Held, inbox.ProcessingState);
        Assert.Equal(
            "cross_provider_identity_unresolved",
            inbox.HoldReason);
        Assert.Throws<InvalidOperationException>(() =>
            inbox.MarkReady(PaymentTestData.VerifiedAt.AddSeconds(2)));
    }

    [Fact]
    public void Lease_ajeno_no_puede_acreditar_efecto_comercial()
    {
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);
        var lease = Guid.CreateVersion7();
        inbox.MarkReady(now.AddSeconds(1));
        inbox.BeginAttempt(lease, now.AddMinutes(1), now.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() =>
            inbox.MarkCommercialEffectApplied(
                Guid.CreateVersion7(),
                ProviderCommercialEffectStates.Applied,
                "commercial-ref",
                now.AddSeconds(3)));

        inbox.MarkIndeterminate(
            lease,
            "response_lost",
            now.AddMinutes(2),
            now.AddSeconds(3));

        Assert.Equal(ProviderInboxStates.Indeterminate, inbox.ProcessingState);
        Assert.Equal(
            ProviderCommercialEffectStates.Indeterminate,
            inbox.CommercialEffectState);
        Assert.Null(inbox.CommercialEffectReference);
    }

    [Theory]
    [InlineData(ProviderCommercialEffectStates.Applied)]
    [InlineData(ProviderCommercialEffectStates.AlreadyApplied)]
    public void Resultado_aplicado_conserva_su_semantica_exacta(
        string effectState)
    {
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);
        var lease = Guid.CreateVersion7();
        inbox.MarkReady(now.AddSeconds(1));
        inbox.BeginAttempt(lease, now.AddMinutes(1), now.AddSeconds(2));

        inbox.MarkCommercialEffectApplied(
            lease,
            effectState,
            "commercial-reference",
            now.AddSeconds(3));

        Assert.Equal(ProviderInboxStates.Completed, inbox.ProcessingState);
        Assert.Equal(effectState, inbox.CommercialEffectState);
        Assert.Equal("commercial-reference", inbox.CommercialEffectReference);
        Assert.Null(inbox.LastFailureCode);
        Assert.Null(inbox.LeaseToken);
    }

    [Fact]
    public void Revision_manual_no_puede_liberar_un_evento_en_proceso()
    {
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);
        var lease = Guid.CreateVersion7();
        inbox.MarkReady(now.AddSeconds(1));
        inbox.BeginAttempt(lease, now.AddMinutes(1), now.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() =>
            inbox.MarkReady(now.AddSeconds(3)));

        Assert.Equal(ProviderInboxStates.Processing, inbox.ProcessingState);
        Assert.Equal(lease, inbox.LeaseToken);
        Assert.Equal(now.AddMinutes(1), inbox.LeaseUntil);
    }

    [Fact]
    public void Hold_de_intento_activo_exige_el_lease_correcto()
    {
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);
        var lease = Guid.CreateVersion7();
        inbox.MarkReady(now.AddSeconds(1));
        inbox.BeginAttempt(lease, now.AddMinutes(1), now.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() =>
            inbox.Hold("late_conflict", now.AddSeconds(3)));
        Assert.Throws<InvalidOperationException>(() =>
            inbox.HoldActiveAttempt(
                Guid.CreateVersion7(),
                "late_conflict",
                now.AddSeconds(3)));

        Assert.Equal(ProviderInboxStates.Processing, inbox.ProcessingState);
        Assert.Equal(lease, inbox.LeaseToken);
        Assert.Equal(3, inbox.Revision);

        inbox.HoldActiveAttempt(
            lease,
            "late_conflict",
            now.AddSeconds(3));

        Assert.Equal(ProviderInboxStates.Held, inbox.ProcessingState);
        Assert.Null(inbox.LeaseToken);
        Assert.Equal(4, inbox.Revision);
    }

    [Fact]
    public void Hold_elimina_reintento_programado_sin_acreditar_efecto()
    {
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);
        var lease = Guid.CreateVersion7();
        inbox.MarkReady(now.AddSeconds(1));
        inbox.BeginAttempt(lease, now.AddMinutes(1), now.AddSeconds(2));
        inbox.MarkIndeterminate(
            lease,
            "temporary_failure",
            now.AddMinutes(2),
            now.AddSeconds(3));

        inbox.Hold("identity_conflict", now.AddSeconds(4));

        Assert.Equal(ProviderInboxStates.Held, inbox.ProcessingState);
        Assert.Null(inbox.NextAttemptAt);
        Assert.Null(inbox.LeaseToken);
        Assert.Equal(
            ProviderCommercialEffectStates.Indeterminate,
            inbox.CommercialEffectState);
        Assert.Null(inbox.CommercialEffectReference);
    }

    [Theory]
    [InlineData(ProviderCommercialEffectStates.Conflict)]
    [InlineData(ProviderCommercialEffectStates.DecisionRequired)]
    public void Resultado_comercial_no_aplicado_queda_en_hold_sin_reintento(
        string effectState)
    {
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);
        var lease = Guid.CreateVersion7();
        inbox.MarkReady(now.AddSeconds(1));
        inbox.BeginAttempt(lease, now.AddMinutes(1), now.AddSeconds(2));

        inbox.MarkCommercialHold(
            lease,
            effectState,
            "commercial_authority_required",
            now.AddSeconds(3));

        Assert.Equal(ProviderInboxStates.Held, inbox.ProcessingState);
        Assert.Equal(effectState, inbox.CommercialEffectState);
        Assert.Equal("commercial_authority_required", inbox.HoldReason);
        Assert.Equal("commercial_authority_required", inbox.LastFailureCode);
        Assert.Null(inbox.CommercialEffectReference);
        Assert.Null(inbox.NextAttemptAt);
        Assert.Null(inbox.LeaseToken);
    }

    [Fact]
    public void Transicion_con_instante_anterior_no_muta_el_inbox()
    {
        var now = PaymentTestData.VerifiedAt;
        var inbox = ProviderInboxEvent.Create(PaymentTestData.Evidence(), now);

        Assert.Throws<ArgumentException>(() =>
            inbox.MarkReady(now.AddTicks(-1)));

        Assert.Equal(ProviderInboxStates.PendingReview, inbox.ProcessingState);
        Assert.Equal(now, inbox.UpdatedAt);
        Assert.Equal(1, inbox.Revision);
    }

    [Fact]
    public void Intento_es_append_only_y_exige_fallo_para_resultado_no_exitoso()
    {
        var now = PaymentTestData.VerifiedAt;
        var attempt = ProviderProcessingAttempt.Start(
            Guid.CreateVersion7(),
            1,
            Guid.CreateVersion7(),
            now);

        Assert.Throws<ArgumentException>(() =>
            attempt.Complete(
                ProviderAttemptOutcomes.Indeterminate,
                now.AddSeconds(1),
                null));

        attempt.Complete(
            ProviderAttemptOutcomes.Indeterminate,
            now.AddSeconds(1),
            "timeout");

        Assert.Equal(ProviderAttemptOutcomes.Indeterminate, attempt.Outcome);
        Assert.Throws<InvalidOperationException>(() =>
            attempt.Complete(
                ProviderAttemptOutcomes.Succeeded,
                now.AddSeconds(2),
                null));
    }

    private static PaymentsDbContext Context()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=sillar_tests;Username=sillar;Password=sillar")
            .Options;
        return new PaymentsDbContext(options);
    }

    private static void AssertOpaqueCollation(
        IEntityType entity,
        params string[] propertyNames)
    {
        Assert.All(
            propertyNames,
            propertyName => Assert.Equal(
                PaymentsDbContext.OpaqueIdentifierCollation,
                entity.FindProperty(propertyName)!.GetCollation()));
    }
}
