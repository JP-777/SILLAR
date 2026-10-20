namespace Sillar.Modules.Payments.Domain;

/// <summary>Código monetario ISO 4217 explícito, sin normalización implícita.</summary>
public sealed record Iso4217CurrencyCode
{
    private Iso4217CurrencyCode(string value) => Value = value;

    public string Value { get; }

    public static Iso4217CurrencyCode From(string? value)
    {
        if (value is null
            || value.Length != 3
            || value.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException(
                "La moneda debe ser un código ISO 4217 explícito de tres mayúsculas.",
                nameof(value));
        }

        return new(value);
    }
}

/// <summary>
/// Alcance monetario ratificado para una aplicación provider: M03 debe cotejar
/// el importe informado contra todo el saldo pendiente, nunca inferir un parcial.
/// </summary>
public enum ProviderSettlementAmountScope
{
    FullOutstandingAmount
}

/// <summary>
/// Hechos monetarios extraídos de evidencia ya verificada. Siguen sin ser un
/// pago comercial ni autorizan una escritura fuera de M11.
/// </summary>
public sealed record ProviderSettlementFacts
{
    private ProviderSettlementFacts(
        VerifiedExternalPaymentEvidence evidence,
        decimal grossAmount,
        Iso4217CurrencyCode currency,
        DateTimeOffset providerConfirmedAt)
    {
        Evidence = evidence;
        GrossAmount = grossAmount;
        Currency = currency;
        ProviderConfirmedAt = providerConfirmedAt;
    }

    public VerifiedExternalPaymentEvidence Evidence { get; }
    public decimal GrossAmount { get; }
    public Iso4217CurrencyCode Currency { get; }
    public DateTimeOffset ProviderConfirmedAt { get; }
    public ProviderSettlementAmountScope AmountScope
        => ProviderSettlementAmountScope.FullOutstandingAmount;

    /// <summary>
    /// J-03E: M11 no añade comisión al importe que debe pagar el cliente.
    /// </summary>
    public decimal AdditionalCustomerFee => 0m;

    public static ProviderSettlementFacts Create(
        VerifiedExternalPaymentEvidence evidence,
        decimal grossAmount,
        string? currencyCode,
        DateTimeOffset providerConfirmedAt)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (grossAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grossAmount),
                "El importe provider debe ser positivo.");
        }

        if (providerConfirmedAt == default)
        {
            throw new ArgumentException(
                "El hecho provider exige instante de confirmación.",
                nameof(providerConfirmedAt));
        }

        return new(
            evidence,
            grossAmount,
            Iso4217CurrencyCode.From(currencyCode),
            providerConfirmedAt);
    }
}

public enum ProviderSettlementFactDecision
{
    New,
    Duplicate,
    Conflict,
    DecisionRequired
}

/// <summary>
/// Anti-replay de hechos monetarios. La identidad primaria continúa siendo
/// <c>(provider, event_id)</c>; una liquidación compartida por otra identidad no
/// se libera automáticamente.
/// </summary>
public static class ProviderSettlementFactClassifier
{
    public static ProviderSettlementFactDecision Classify(
        ProviderSettlementFacts incoming,
        IEnumerable<ProviderSettlementFacts> known)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);

        var incomingIdentity = ProviderEventIdempotencyKey.From(
            incoming.Evidence);
        var incomingSettlement = ExternalSettlementIdempotencyKey.From(
            incoming.Evidence);
        var duplicate = false;
        var decisionRequired = false;
        var conflict = false;

        foreach (var previous in known)
        {
            ArgumentNullException.ThrowIfNull(previous);
            var previousIdentity = ProviderEventIdempotencyKey.From(
                previous.Evidence);
            var previousSettlement = ExternalSettlementIdempotencyKey.From(
                previous.Evidence);

            if (previousIdentity == incomingIdentity)
            {
                if (EquivalentFacts(previous, incoming))
                {
                    duplicate = true;
                }
                else
                {
                    conflict = true;
                }

                continue;
            }

            if (previousSettlement != incomingSettlement)
            {
                continue;
            }

            if (StringComparer.Ordinal.Equals(
                    previousIdentity.Provider,
                    incomingIdentity.Provider)
                && !EquivalentFacts(previous, incoming))
            {
                conflict = true;
            }
            else
            {
                // Otra identidad de evento —y con mayor razón otro proveedor—
                // necesita la decisión de la autoridad comercial antes de usar
                // una misma liquidación.
                decisionRequired = true;
            }
        }

        if (conflict)
        {
            return ProviderSettlementFactDecision.Conflict;
        }

        if (decisionRequired)
        {
            return ProviderSettlementFactDecision.DecisionRequired;
        }

        return duplicate
            ? ProviderSettlementFactDecision.Duplicate
            : ProviderSettlementFactDecision.New;
    }

    private static bool EquivalentFacts(
        ProviderSettlementFacts left,
        ProviderSettlementFacts right)
        => StringComparer.Ordinal.Equals(
                left.Evidence.Provider,
                right.Evidence.Provider)
            && StringComparer.Ordinal.Equals(
                left.Evidence.ExternalSettlementId,
                right.Evidence.ExternalSettlementId)
            && StringComparer.Ordinal.Equals(
                left.Evidence.BodySha256,
                right.Evidence.BodySha256)
            && left.GrossAmount == right.GrossAmount
            && left.Currency == right.Currency
            && left.ProviderConfirmedAt == right.ProviderConfirmedAt;
}
