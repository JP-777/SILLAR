namespace Sillar.Modules.Services.Domain;
public sealed class ServiceEntry
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? SaleUnit { get; set; }
    public Guid? ImageId { get; set; }
    public string? ImageAltText { get; set; }
    public PublicationState PublicationState { get; set; } = PublicationState.Draft;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
