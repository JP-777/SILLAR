namespace Sillar.Modules.Crm.Dtos;

public sealed record CustomerLoginRequest(
    string? Email,
    string? Password);

public sealed record CustomerAuthenticatedResponse(
    Guid CustomerId,
    string FullName,
    string Email,
    bool EmailVerified);

public sealed record CustomerLoginResponse(
    CustomerAuthenticatedResponse Customer,
    string CsrfToken);
