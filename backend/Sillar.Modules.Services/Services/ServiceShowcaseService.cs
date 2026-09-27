using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Services.Contracts;
using Sillar.Modules.Services.Data;
using Sillar.Modules.Services.Domain;
using Sillar.Modules.Services.Dtos;

namespace Sillar.Modules.Services.Services;

internal sealed class ServiceShowcaseService(ServicesDbContext database, IMediaStorage media)
    : IServiceShowcaseSnapshots
{
    internal async Task<IReadOnlyList<ServicePublicResponse>> ListPublicAsync(CancellationToken ct)
    {
        var entries = await database.Entries.AsNoTracking()
            .Where(x => x.PublicationState == PublicationState.Published)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync(ct);
        return [.. entries.Select(Public)];
    }

    internal async Task<ServicePublicResponse?> GetPublicAsync(string slug, CancellationToken ct)
    {
        var entry = await database.Entries.AsNoTracking().FirstOrDefaultAsync(
            x => x.Slug == slug && x.PublicationState == PublicationState.Published, ct);
        return entry is null ? null : Public(entry);
    }

    internal async Task<IReadOnlyList<ServiceAdminResponse>> ListAdminAsync(CancellationToken ct) =>
        [.. (await database.Entries.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync(ct)).Select(Admin)];

    internal async Task<ServiceAdminResponse?> GetAdminAsync(int id, CancellationToken ct)
    {
        var entry = await database.Entries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entry is null ? null : Admin(entry);
    }

    internal async Task<ServiceOperation<ServiceAdminResponse>> CreateAsync(SaveServiceRequest request, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation is not null) return Invalid(validation);
        var slug = NormalizeSlug(request);
        if (await database.Entries.AnyAsync(x => x.Slug == slug, ct)) return Conflict("Ya existe un servicio con esa dirección.");
        var order = await database.Entries.Select(x => (int?)x.DisplayOrder).MaxAsync(ct) ?? -1;
        var entry = new ServiceEntry { Name = request.Name!.Trim(), Slug = slug,
            ShortDescription = Optional(request.ShortDescription), Description = Optional(request.Description),
            Price = request.Price, SaleUnit = Optional(request.SaleUnit), ImageId = request.ImageId,
            ImageAltText = Optional(request.ImageAltText), DisplayOrder = order + 1 };
        database.Entries.Add(entry);
        await database.SaveChangesAsync(ct);
        return new(ServiceOutcome.Ok, Value: Admin(entry));
    }

    internal async Task<ServiceOperation<ServiceAdminResponse>> UpdateAsync(int id, SaveServiceRequest request, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation is not null) return Invalid(validation);
        var entry = await database.Entries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null) return new(ServiceOutcome.NotFound);
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? entry.Slug : ServiceRules.Slugify(request.Slug);
        if (await database.Entries.AnyAsync(x => x.Id != id && x.Slug == slug, ct)) return Conflict("Ya existe un servicio con esa dirección.");
        entry.Name = request.Name!.Trim(); entry.Slug = slug;
        entry.ShortDescription = Optional(request.ShortDescription); entry.Description = Optional(request.Description);
        entry.Price = request.Price; entry.SaleUnit = Optional(request.SaleUnit); entry.ImageId = request.ImageId;
        entry.ImageAltText = Optional(request.ImageAltText);
        await database.SaveChangesAsync(ct);
        return new(ServiceOutcome.Ok, Value: Admin(entry));
    }

    internal async Task<ServiceOperation<ServiceAdminResponse>> TransitionAsync(int id, PublicationState target, CancellationToken ct)
    {
        var entry = await database.Entries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null) return new(ServiceOutcome.NotFound);
        if (!ServiceRules.CanTransition(entry.PublicationState, target))
            return Conflict($"No se puede pasar de {entry.PublicationState} a {target}.");
        entry.PublicationState = target;
        await database.SaveChangesAsync(ct);
        return new(ServiceOutcome.Ok, Value: Admin(entry));
    }

    internal async Task<ServiceOperation<IReadOnlyList<int>>> ReorderAsync(ReorderServicesRequest request, CancellationToken ct)
    {
        var ids = await database.Entries.Select(x => x.Id).ToListAsync(ct);
        if (request.OrderedIds.Count != ids.Count || request.OrderedIds.Distinct().Count() != ids.Count || ids.Except(request.OrderedIds).Any())
            return new(ServiceOutcome.Conflict, "El orden debe incluir cada servicio exactamente una vez.");
        var entries = await database.Entries.ToListAsync(ct);
        var positions = request.OrderedIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        foreach (var entry in entries) entry.DisplayOrder = positions[entry.Id];
        await database.SaveChangesAsync(ct);
        return new(ServiceOutcome.Ok, Value: request.OrderedIds);
    }

    public async Task<ServiceSnapshot?> GetPublishedSnapshotAsync(int serviceId, CancellationToken cancellationToken)
    {
        var entry = await database.Entries.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == serviceId && x.PublicationState == PublicationState.Published, cancellationToken);
        return entry is null ? null : new(entry.Id, entry.Name, entry.Slug, entry.ShortDescription, entry.Description,
            entry.Price, entry.SaleUnit, entry.ImageId, Url(entry.ImageId), entry.ImageAltText);
    }

    private string? Validate(SaveServiceRequest request) =>
        ServiceRules.Validate(request.Name, request.ShortDescription, request.Description, request.Price, request.ImageId, request.ImageAltText)
        ?? (request.ImageId is not null && Url(request.ImageId) is null ? "La imagen indicada no existe o no está activa." : null)
        ?? (string.IsNullOrWhiteSpace(NormalizeSlug(request)) ? "La dirección pública no es válida." : null);
    private static string NormalizeSlug(SaveServiceRequest request) => ServiceRules.Slugify(
        string.IsNullOrWhiteSpace(request.Slug) ? request.Name ?? string.Empty : request.Slug);
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string? Url(Guid? id) => id is { } value ? media.GetPublicUrl(value) : null;
    private ServicePublicResponse Public(ServiceEntry x) => new(x.Id, x.Name, x.Slug, x.ShortDescription, x.Description,
        x.Price, x.SaleUnit, Url(x.ImageId), x.ImageAltText);
    private ServiceAdminResponse Admin(ServiceEntry x) => new(x.Id, x.Name, x.Slug, x.ShortDescription, x.Description,
        x.Price, x.SaleUnit, x.ImageId, Url(x.ImageId), x.ImageAltText, x.PublicationState, x.DisplayOrder);
    private static ServiceOperation<ServiceAdminResponse> Invalid(string error) => new(ServiceOutcome.Invalid, error);
    private static ServiceOperation<ServiceAdminResponse> Conflict(string error) => new(ServiceOutcome.Conflict, error);
}
