namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Estados técnicos de una solicitud provider-neutral. Ninguno acredita un
/// pago ni cambia el estado comercial del pedido en M03.
/// </summary>
internal enum ProviderPaymentRequestState
{
    AwaitingConfirmation,
    ReadyForCommercialEvaluation,
    LateConfirmationPendingReconciliation,
    Rejected,
    Conflict,
    DecisionRequired,
    Indeterminate
}

/// <summary>
/// Máquina pura para J-03C. Rechaza una solicitud iniciada cuando su ventana ya
/// venció y conserva una confirmación tardía de una solicitud preexistente para
/// conciliación humana, sin reactivar el pedido ni autorizar un efecto.
/// </summary>
internal sealed record ProviderPaymentRequestLifecycle
{
    private ProviderPaymentRequestLifecycle(
        Guid requestId,
        Guid orderId,
        DateTimeOffset requestedAt,
        DateTimeOffset expiresAt,
        ProviderPaymentRequestState state,
        ProviderSettlementFacts? observedFacts,
        ProviderSettlementFacts? contradictoryFacts,
        DateTimeOffset? confirmationReceivedAt,
        string? reason)
    {
        RequestId = requestId;
        OrderId = orderId;
        RequestedAt = requestedAt;
        ExpiresAt = expiresAt;
        State = state;
        ObservedFacts = observedFacts;
        ContradictoryFacts = contradictoryFacts;
        ConfirmationReceivedAt = confirmationReceivedAt;
        Reason = reason;
    }

    internal Guid RequestId { get; }
    internal Guid OrderId { get; }
    internal DateTimeOffset RequestedAt { get; }
    internal DateTimeOffset ExpiresAt { get; }
    internal ProviderPaymentRequestState State { get; }
    internal ProviderSettlementFacts? ObservedFacts { get; }
    internal ProviderSettlementFacts? ContradictoryFacts { get; }
    internal DateTimeOffset? ConfirmationReceivedAt { get; }
    internal string? Reason { get; }

    internal bool CanEvaluateCommercially
        => State == ProviderPaymentRequestState.ReadyForCommercialEvaluation;

    internal bool RequiresHumanReconciliation
        => State is ProviderPaymentRequestState.LateConfirmationPendingReconciliation
            or ProviderPaymentRequestState.DecisionRequired
            or ProviderPaymentRequestState.Conflict;

    // J-03C: una confirmación tardía nunca es autoridad para reactivar M03.
    internal bool MayReactivateOrder => false;

    internal static ProviderPaymentRequestLifecycle Start(
        Guid requestId,
        Guid orderId,
        DateTimeOffset requestedAt,
        DateTimeOffset expiresAt)
    {
        if (requestId == Guid.Empty
            || orderId == Guid.Empty
            || requestedAt == default
            || expiresAt == default)
        {
            throw new ArgumentException(
                "La solicitud exige identidades e instantes válidos.");
        }

        var expired = requestedAt >= expiresAt;
        return new(
            requestId,
            orderId,
            requestedAt,
            expiresAt,
            expired
                ? ProviderPaymentRequestState.Rejected
                : ProviderPaymentRequestState.AwaitingConfirmation,
            null,
            null,
            null,
            expired ? "expired_order_new_request" : null);
    }

    internal ProviderPaymentRequestLifecycle ObserveConfirmation(
        ProviderSettlementFacts facts,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (receivedAt == default)
        {
            throw new ArgumentException(
                "La confirmación exige instante de recepción.",
                nameof(receivedAt));
        }

        if (State == ProviderPaymentRequestState.Rejected)
        {
            return this;
        }

        if (ObservedFacts is not null)
        {
            return ReconcileRedelivery(facts);
        }

        if (State is not ProviderPaymentRequestState.AwaitingConfirmation
            and not ProviderPaymentRequestState.Indeterminate)
        {
            return this;
        }

        if (receivedAt < RequestedAt
            || facts.ProviderConfirmedAt < RequestedAt)
        {
            return Transition(
                ProviderPaymentRequestState.Conflict,
                facts,
                null,
                receivedAt,
                "confirmation_precedes_request");
        }

        if (receivedAt >= ExpiresAt
            || facts.ProviderConfirmedAt >= ExpiresAt)
        {
            return Transition(
                ProviderPaymentRequestState.LateConfirmationPendingReconciliation,
                facts,
                null,
                receivedAt,
                "late_confirmation_requires_reconciliation");
        }

        return Transition(
            ProviderPaymentRequestState.ReadyForCommercialEvaluation,
            facts,
            null,
            receivedAt,
            null);
    }

    internal ProviderPaymentRequestLifecycle RequireDecision(string? reason)
    {
        if (State !=
            ProviderPaymentRequestState.LateConfirmationPendingReconciliation)
        {
            throw new InvalidOperationException(
                "Sólo una confirmación tardía puede elevarse por esta transición.");
        }

        return Transition(
            ProviderPaymentRequestState.DecisionRequired,
            ObservedFacts,
            ContradictoryFacts,
            ConfirmationReceivedAt,
            RequiredReason(reason));
    }

    internal ProviderPaymentRequestLifecycle MarkIndeterminate(string? reason)
    {
        if (State is ProviderPaymentRequestState.Rejected
            or ProviderPaymentRequestState.Conflict
            or ProviderPaymentRequestState.DecisionRequired)
        {
            throw new InvalidOperationException(
                "Un estado terminal no puede degradarse a indeterminado.");
        }

        return Transition(
            ProviderPaymentRequestState.Indeterminate,
            ObservedFacts,
            ContradictoryFacts,
            ConfirmationReceivedAt,
            RequiredReason(reason));
    }

    private ProviderPaymentRequestLifecycle ReconcileRedelivery(
        ProviderSettlementFacts facts)
    {
        var decision = ProviderSettlementFactClassifier.Classify(
            facts,
            [ObservedFacts!]);
        return decision switch
        {
            ProviderSettlementFactDecision.Duplicate => this,
            ProviderSettlementFactDecision.Conflict => Transition(
                ProviderPaymentRequestState.Conflict,
                ObservedFacts,
                facts,
                ConfirmationReceivedAt,
                "confirmation_redelivery_conflict"),
            ProviderSettlementFactDecision.DecisionRequired => Transition(
                ProviderPaymentRequestState.DecisionRequired,
                ObservedFacts,
                facts,
                ConfirmationReceivedAt,
                "settlement_identity_requires_commercial_authority"),
            _ => Transition(
                ProviderPaymentRequestState.Conflict,
                ObservedFacts,
                facts,
                ConfirmationReceivedAt,
                "confirmation_redelivery_identity_changed")
        };
    }

    private ProviderPaymentRequestLifecycle Transition(
        ProviderPaymentRequestState state,
        ProviderSettlementFacts? facts,
        ProviderSettlementFacts? contradictoryFacts,
        DateTimeOffset? receivedAt,
        string? reason)
        => new(
            RequestId,
            OrderId,
            RequestedAt,
            ExpiresAt,
            state,
            facts,
            contradictoryFacts,
            receivedAt,
            reason);

    private static string RequiredReason(string? reason)
        => string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException(
                "La transición exige una razón.",
                nameof(reason))
            : reason;
}
