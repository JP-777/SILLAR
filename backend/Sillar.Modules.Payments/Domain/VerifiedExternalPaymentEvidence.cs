namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Evidencia normalizada de una señal externa cuya firma ya fue verificada al
/// recibirla.
/// </summary>
/// <remarks>
/// Este valor no verifica criptografía ni contiene el cuerpo crudo. Su fábrica
/// es una frontera de precondición para el futuro verificador: sólo debe llamarse
/// después de una verificación válida. Conserva las identidades externas, el
/// SHA-256 del cuerpo recibido y el instante en que se obtuvo el resultado.
/// </remarks>
public sealed record VerifiedExternalPaymentEvidence
{
    private VerifiedExternalPaymentEvidence(
        string provider,
        string providerEventId,
        string externalSettlementId,
        string bodySha256,
        DateTimeOffset verifiedAt)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        ExternalSettlementId = externalSettlementId;
        BodySha256 = bodySha256;
        VerifiedAt = verifiedAt;
    }

    public string Provider { get; }

    public string ProviderEventId { get; }

    public string ExternalSettlementId { get; }

    public string BodySha256 { get; }

    public DateTimeOffset VerifiedAt { get; }

    /// <summary>
    /// Registra el resultado de un webhook que un verificador externo a este
    /// núcleo puro ya autenticó correctamente.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Cuando falta una identidad, el hash no es un SHA-256 hexadecimal o el
    /// instante de verificación no fue informado.
    /// </exception>
    public static VerifiedExternalPaymentEvidence FromVerifiedWebhook(
        string? provider,
        string? providerEventId,
        string? externalSettlementId,
        string? bodySha256,
        DateTimeOffset verifiedAt)
    {
        var normalizedProvider = Required(provider, nameof(provider));
        var normalizedEventId = Required(providerEventId, nameof(providerEventId));
        var normalizedSettlementId = Required(
            externalSettlementId,
            nameof(externalSettlementId));

        if (string.IsNullOrWhiteSpace(bodySha256)
            || bodySha256.Length != 64
            || !bodySha256.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException(
                "La evidencia provider exige el SHA-256 hexadecimal del cuerpo recibido.",
                nameof(bodySha256));
        }

        if (verifiedAt == default)
        {
            throw new ArgumentException(
                "La evidencia provider exige el instante de verificación.",
                nameof(verifiedAt));
        }

        return new VerifiedExternalPaymentEvidence(
            normalizedProvider,
            normalizedEventId,
            normalizedSettlementId,
            bodySha256.ToLowerInvariant(),
            verifiedAt);
    }

    private static string Required(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "La evidencia provider exige todas sus identidades externas.",
                parameterName);
        }

        return value.Trim();
    }
}
