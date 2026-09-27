using Sillar.Modules.Services.Domain;
namespace Sillar.Modules.Services.Dtos;
public sealed record ServicePublicResponse(int Id, string Name, string Slug, string? ShortDescription,
    string? Description, decimal? Price, string? SaleUnit, string? ImageUrl, string? ImageAltText);
public sealed record ServiceAdminResponse(int Id, string Name, string Slug, string? ShortDescription,
    string? Description, decimal? Price, string? SaleUnit, Guid? ImageId, string? ImageUrl,
    string? ImageAltText, PublicationState PublicationState, int DisplayOrder);
public sealed record SaveServiceRequest(string? Name, string? Slug, string? ShortDescription,
    string? Description, decimal? Price, string? SaleUnit, Guid? ImageId, string? ImageAltText);
public sealed record ReorderServicesRequest(IReadOnlyList<int> OrderedIds);
