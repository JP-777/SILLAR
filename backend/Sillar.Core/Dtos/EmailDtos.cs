namespace Sillar.Core.Dtos;

public sealed record TestEmailRequest(string? Recipient);

public sealed record TestEmailResponse(
    bool Success,
    string Message);
