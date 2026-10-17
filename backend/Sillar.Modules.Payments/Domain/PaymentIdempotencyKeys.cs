namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Identidad estable de una recepción externa dentro de M11.
/// </summary>
/// <remarks>
/// El valor representa la clave conceptual <c>(provider, provider_event_id)</c>.
/// Su igualdad por valor no coordina concurrencia, no persiste nada y no promete
/// procesamiento exactly-once. Esas garantías requerirán una barrera durable y
/// transaccional que todavía no está autorizada.
/// </remarks>
public sealed record ProviderEventIdempotencyKey
{
    private ProviderEventIdempotencyKey(string provider, string providerEventId)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
    }

    public string Provider { get; }

    public string ProviderEventId { get; }

    public static ProviderEventIdempotencyKey From(
        VerifiedExternalPaymentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new ProviderEventIdempotencyKey(
            evidence.Provider,
            evidence.ProviderEventId);
    }
}

/// <summary>
/// Identidad estable de la liquidación externa que el futuro contrato con M03
/// deberá deduplicar.
/// </summary>
/// <remarks>
/// Este valor sólo transporta <c>external_settlement_id</c>. No implementa ni
/// atribuye a M11 la unicidad comercial, la atomicidad o la escritura del pago;
/// esas responsabilidades permanecen fuera de este proyecto puro.
/// </remarks>
public sealed record ExternalSettlementIdempotencyKey
{
    private ExternalSettlementIdempotencyKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ExternalSettlementIdempotencyKey From(
        VerifiedExternalPaymentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new ExternalSettlementIdempotencyKey(
            evidence.ExternalSettlementId);
    }
}

/// <summary>
/// Las dos identidades complementarias derivadas de una misma evidencia.
/// </summary>
/// <remarks>
/// Agruparlas evita confundirlas, pero no las convierte en una transacción
/// distribuida ni acredita idempotencia hasta que existan las barreras durables
/// y las pruebas de caída correspondientes.
/// </remarks>
public sealed record PaymentIdempotencyKeys(
    ProviderEventIdempotencyKey Reception,
    ExternalSettlementIdempotencyKey Settlement)
{
    public static PaymentIdempotencyKeys From(
        VerifiedExternalPaymentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new PaymentIdempotencyKeys(
            ProviderEventIdempotencyKey.From(evidence),
            ExternalSettlementIdempotencyKey.From(evidence));
    }
}
