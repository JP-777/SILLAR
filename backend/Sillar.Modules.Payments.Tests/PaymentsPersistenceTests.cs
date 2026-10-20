using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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
        var entity = context.Model.FindEntityType(typeof(ProviderInboxEvent))!;
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
                && check.Sql.Contains("accredited", StringComparison.Ordinal));
    }

    [Fact]
    public void Recepcion_conserva_identidades_opacas_y_no_puede_reclasificarse()
    {
        var receipt = ProviderEvidenceReceipt.Receive(
            " provider ",
            " event ",
            " settlement ",
            new string('A', 64),
            PaymentTestData.VerifiedAt);
        var inboxId = Guid.CreateVersion7();

        receipt.MarkAuthenticated(
            inboxId,
            PaymentTestData.VerifiedAt.AddSeconds(1),
            " attestation ");

        Assert.Equal(" provider ", receipt.Provider);
        Assert.Equal(" event ", receipt.ProviderEventId);
        Assert.Equal(" settlement ", receipt.ExternalSettlementId);
        Assert.Equal(new string('a', 64), receipt.BodySha256);
        Assert.Equal(ProviderVerificationStates.Authenticated, receipt.VerificationState);
        Assert.Equal(" attestation ", receipt.AttestationReference);
        Assert.Throws<InvalidOperationException>(() =>
            receipt.MarkRejected("late-rejection"));
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
            inbox.MarkCommercialEffectAccredited(
                Guid.CreateVersion7(),
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
}
