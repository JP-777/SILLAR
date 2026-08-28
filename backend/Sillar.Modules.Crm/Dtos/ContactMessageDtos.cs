namespace Sillar.Modules.Crm.Dtos;

public sealed record PublicContactRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? Subject,
    string? Message);

public sealed record PublicContactAcceptedResponse(
    string Message);

public sealed record AdminContactMessageListItemResponse(
    int ContactMessageId,
    Guid? CustomerId,
    string FullName,
    string? Email,
    string? Phone,
    string? Subject,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record AdminContactMessageDetailResponse(
    int ContactMessageId,
    Guid? CustomerId,
    string FullName,
    string? Email,
    string? Phone,
    string? Subject,
    string Message,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
