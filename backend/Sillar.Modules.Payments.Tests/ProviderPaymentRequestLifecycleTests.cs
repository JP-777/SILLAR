using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

public sealed class ProviderPaymentRequestLifecycleTests
{
    [Fact]
    public void Solicitud_nueva_vencida_se_rechaza_y_no_se_reactiva()
    {
        var expiresAt = PaymentTestData.VerifiedAt.AddMinutes(5);
        var lifecycle = ProviderPaymentRequestLifecycle.Start(
            Guid.CreateVersion7(),
            PaymentTestData.OrderId,
            expiresAt,
            expiresAt);

        var afterRedelivery = lifecycle.ObserveConfirmation(
            PaymentTestData.Facts(),
            expiresAt.AddMinutes(1));

        Assert.Equal(ProviderPaymentRequestState.Rejected, lifecycle.State);
        Assert.Equal("expired_order_new_request", lifecycle.Reason);
        Assert.Same(lifecycle, afterRedelivery);
        Assert.Null(lifecycle.ObservedFacts);
        Assert.False(lifecycle.CanEvaluateCommercially);
        Assert.False(lifecycle.MayReactivateOrder);
        Assert.Throws<InvalidOperationException>(() =>
            lifecycle.MarkIndeterminate("must_not_degrade_rejection"));
    }

    [Fact]
    public void Confirmacion_tardia_preexistente_conserva_evidencia_para_conciliacion()
    {
        var requestedAt = PaymentTestData.VerifiedAt;
        var expiresAt = requestedAt.AddMinutes(5);
        var facts = PaymentTestData.Facts();
        var lifecycle = ProviderPaymentRequestLifecycle.Start(
                Guid.CreateVersion7(),
                PaymentTestData.OrderId,
                requestedAt,
                expiresAt)
            .ObserveConfirmation(facts, expiresAt.AddSeconds(1));

        Assert.Equal(
            ProviderPaymentRequestState.LateConfirmationPendingReconciliation,
            lifecycle.State);
        Assert.Same(facts, lifecycle.ObservedFacts);
        Assert.Equal(expiresAt.AddSeconds(1), lifecycle.ConfirmationReceivedAt);
        Assert.True(lifecycle.RequiresHumanReconciliation);
        Assert.False(lifecycle.CanEvaluateCommercially);
        Assert.False(lifecycle.MayReactivateOrder);

        var decision = lifecycle.RequireDecision(
            "expired_order_commercial_authority_required");
        Assert.Equal(ProviderPaymentRequestState.DecisionRequired, decision.State);
        Assert.Same(facts, decision.ObservedFacts);
        Assert.Null(decision.ContradictoryFacts);
    }

    [Fact]
    public void Confirmacion_en_ventana_solo_habilita_evaluacion_comercial()
    {
        var requestedAt = PaymentTestData.VerifiedAt;
        var lifecycle = ProviderPaymentRequestLifecycle.Start(
                Guid.CreateVersion7(),
                PaymentTestData.OrderId,
                requestedAt,
                requestedAt.AddMinutes(5))
            .ObserveConfirmation(
                PaymentTestData.Facts(),
                requestedAt.AddMinutes(2));

        Assert.Equal(
            ProviderPaymentRequestState.ReadyForCommercialEvaluation,
            lifecycle.State);
        Assert.True(lifecycle.CanEvaluateCommercially);
        Assert.False(lifecycle.MayReactivateOrder);
        Assert.Equal(
            ProviderSettlementAmountScope.FullOutstandingAmount,
            lifecycle.ObservedFacts!.AmountScope);
        Assert.Equal("PEN", lifecycle.ObservedFacts.Currency.Value);
        Assert.Equal(0m, lifecycle.ObservedFacts.AdditionalCustomerFee);
    }

    [Fact]
    public void Redelivery_identica_no_muta_la_confirmacion_tardia()
    {
        var requestedAt = PaymentTestData.VerifiedAt;
        var facts = PaymentTestData.Facts();
        var lifecycle = ProviderPaymentRequestLifecycle.Start(
                Guid.CreateVersion7(),
                PaymentTestData.OrderId,
                requestedAt,
                requestedAt.AddMinutes(5))
            .ObserveConfirmation(facts, requestedAt.AddMinutes(6));

        var redelivery = lifecycle.ObserveConfirmation(
            facts,
            requestedAt.AddMinutes(7));

        Assert.Same(lifecycle, redelivery);
        Assert.Equal(
            ProviderPaymentRequestState.LateConfirmationPendingReconciliation,
            redelivery.State);
        Assert.Equal(
            requestedAt.AddMinutes(6),
            redelivery.ConfirmationReceivedAt);
    }

    [Fact]
    public void Timestamp_provider_fuera_de_ventana_tambien_exige_conciliacion()
    {
        var requestedAt = PaymentTestData.VerifiedAt;
        var expiresAt = requestedAt.AddMinutes(1);
        var lifecycle = ProviderPaymentRequestLifecycle.Start(
                Guid.CreateVersion7(),
                PaymentTestData.OrderId,
                requestedAt,
                expiresAt)
            .ObserveConfirmation(
                PaymentTestData.Facts(),
                requestedAt.AddSeconds(30));

        Assert.Equal(
            ProviderPaymentRequestState.LateConfirmationPendingReconciliation,
            lifecycle.State);
        Assert.True(lifecycle.RequiresHumanReconciliation);
        Assert.False(lifecycle.CanEvaluateCommercially);
    }

    [Fact]
    public void Redelivery_con_hechos_divergentes_es_conflicto_y_conserva_ambos()
    {
        var requestedAt = PaymentTestData.VerifiedAt;
        var evidence = PaymentTestData.Evidence();
        var original = PaymentTestData.Facts(evidence);
        var divergent = PaymentTestData.Facts(
            evidence,
            amount: original.GrossAmount + 1m);
        var lifecycle = ProviderPaymentRequestLifecycle.Start(
                Guid.CreateVersion7(),
                PaymentTestData.OrderId,
                requestedAt,
                requestedAt.AddMinutes(5))
            .ObserveConfirmation(original, requestedAt.AddMinutes(2));

        var conflict = lifecycle.ObserveConfirmation(
            divergent,
            requestedAt.AddMinutes(3));

        Assert.Equal(ProviderPaymentRequestState.Conflict, conflict.State);
        Assert.Same(original, conflict.ObservedFacts);
        Assert.Same(divergent, conflict.ContradictoryFacts);
        Assert.Equal("confirmation_redelivery_conflict", conflict.Reason);
        Assert.True(conflict.RequiresHumanReconciliation);
        Assert.False(conflict.CanEvaluateCommercially);
    }

    [Fact]
    public void Timeout_permanece_indeterminado_y_no_se_confunde_con_rechazo()
    {
        var requestedAt = PaymentTestData.VerifiedAt;
        var lifecycle = ProviderPaymentRequestLifecycle.Start(
                Guid.CreateVersion7(),
                PaymentTestData.OrderId,
                requestedAt,
                requestedAt.AddMinutes(5))
            .MarkIndeterminate("provider_timeout");

        Assert.Equal(ProviderPaymentRequestState.Indeterminate, lifecycle.State);
        Assert.Equal("provider_timeout", lifecycle.Reason);
        Assert.NotEqual(ProviderPaymentRequestState.Rejected, lifecycle.State);
        Assert.False(lifecycle.CanEvaluateCommercially);
        Assert.False(lifecycle.MayReactivateOrder);

        var recovered = lifecycle.ObserveConfirmation(
            PaymentTestData.Facts(),
            requestedAt.AddMinutes(2));
        Assert.Equal(
            ProviderPaymentRequestState.ReadyForCommercialEvaluation,
            recovered.State);
        Assert.NotNull(recovered.ObservedFacts);
    }
}
