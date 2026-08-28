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

public sealed record CustomerRegisterRequest(
    string? FullName,
    string? Email,
    string? Password,
    string? Phone);

public sealed record CustomerRegistrationResponse(
    string Message);

public sealed record CustomerTokenRequest(
    string? Token);

public sealed record CustomerPasswordResetRequest(
    string? Email);

public sealed record CustomerPasswordResetConfirmRequest(
    string? Token,
    string? NewPassword);

public sealed record CustomerInvitationAcceptRequest(
    string? Token,
    string? Password);

public sealed record CustomerOperationResponse(
    string Message);

