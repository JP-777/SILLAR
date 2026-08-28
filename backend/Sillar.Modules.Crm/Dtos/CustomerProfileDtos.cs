namespace Sillar.Modules.Crm.Dtos;

public sealed record CustomerProfileResponse(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber,
    bool EmailVerified,
    IReadOnlyList<CustomerAddressResponse> Addresses);

public sealed record CustomerAddressResponse(
    Guid CustomerAddressId,
    string? Label,
    string AddressLine,
    string? District,
    string? Province,
    string? Department,
    string? Reference,
    bool IsPreferred);

public sealed record UpdateCustomerProfileRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber);

public sealed record SaveCustomerAddressRequest(
    string? Label,
    string? AddressLine,
    string? District,
    string? Province,
    string? Department,
    string? Reference,
    bool IsPreferred);
