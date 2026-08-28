namespace Sillar.Core.Dtos;

public sealed record TestEmailRequest(string? Recipient);

public sealed record TestEmailResponse(
    bool Success,
    string Message);

/// <summary>
/// Estado persistente de la última prueba SMTP.
/// </summary>
public sealed record EmailTestStatusResponse(
    bool NeverTested,
    DateTimeOffset? LastTestedAt,
    bool? LastSuccess);
