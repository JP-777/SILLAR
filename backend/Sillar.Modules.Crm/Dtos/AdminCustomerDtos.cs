namespace Sillar.Modules.Crm.Dtos;

public sealed record CreateAdminCustomerRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber,
    string? InternalNotes);

public sealed record UpdateAdminCustomerRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber,
    string? InternalNotes);

public sealed record AdminCustomerAccessResponse(
    string State,
    DateTimeOffset? Since,
    bool EmailVerified,
    DateTimeOffset? InvitationExpiresAt);

public sealed record AdminCustomerListItemResponse(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber,
    bool IsActive,
    AdminCustomerAccessResponse Access);

public sealed record AdminCustomerAddressResponse(
    Guid CustomerAddressId,
    string? Label,
    string AddressLine,
    string? District,
    string? Province,
    string? Department,
    string? Reference,
    bool IsPreferred,
    bool IsActive);

public sealed record AdminCustomerDetailResponse(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber,
    string? InternalNotes,
    bool IsActive,
    DateTimeOffset? DeactivatedAt,
    DateTimeOffset? BlockedAt,
    DateTimeOffset? ReactivationRequestedAt,
    DateTimeOffset? ReactivationResolvedAt,
    AdminCustomerAccessResponse Access,
    IReadOnlyList<AdminCustomerAddressResponse> Addresses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminCustomerInvitationResponse(
    bool EmailSent,
    string Message,
    DateTimeOffset InvitationExpiresAt);
