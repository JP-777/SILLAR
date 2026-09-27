namespace Sillar.Modules.Services.Contracts;

/// <summary>Fotografía editorial que un consumidor copia; nunca conserva una referencia viva.</summary>
public sealed record ServiceSnapshot(int ServiceId, string Name, string Slug, string? ShortDescription,
    string? Description, decimal? Price, string? SaleUnit, Guid? MediaAssetId, string? ImageUrl, string? ImageAltText);

/// <summary>Lectura puntual de servicios publicados para construir snapshots históricos.</summary>
public interface IServiceShowcaseSnapshots
{
    Task<ServiceSnapshot?> GetPublishedSnapshotAsync(int serviceId, CancellationToken cancellationToken);
}
