using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Domain;
using Sillar.Modules.Cms.Dtos;

namespace Sillar.Modules.Cms.Services;

/// <summary>Banners públicos y operaciones editoriales, sin transporte HTTP.</summary>
internal sealed class BannerService(
    CmsDbContext database,
    IMediaStorage media,
    CmsOrderService order,
    TimeProvider clock)
{
    internal async Task<IReadOnlyList<BannerResponse>> ListPublicAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var banners = await database.Banners
            .AsNoTracking()
            .Where(PublicationWindow.CurrentAt<Banner>(now))
            .Where(banner => banner.ImageDesktopId != null)
            .OrderBy(banner => banner.DisplayOrder)
            .ThenBy(banner => banner.Id)
            .ToListAsync(cancellationToken);

        var published = new List<BannerResponse>(banners.Count);
        foreach (var banner in banners)
        {
            var desktopUrl = MediaUrl(banner.ImageDesktopId);
            if (desktopUrl is null || string.IsNullOrWhiteSpace(banner.AltText))
            {
                continue;
            }

            published.Add(new BannerResponse(
                banner.Id,
                banner.Title,
                banner.Subtitle,
                desktopUrl,
                MediaUrl(banner.ImageMobileId),
                banner.AltText,
                banner.LinkUrl,
                banner.LinkLabel));
        }

        return published;
    }

    internal async Task<IReadOnlyList<BannerAdminResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var banners = await database.Banners.AsNoTracking()
            .OrderBy(banner => banner.DisplayOrder)
            .ThenBy(banner => banner.Id)
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return [.. banners.Select(banner => Project(banner, now))];
    }

    internal async Task<BannerAdminResponse?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var banner = await database.Banners.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return banner is null ? null : Project(banner, clock.GetUtcNow());
    }

    internal async Task<CmsOperation<BannerAdminResponse>> CreateAsync(
        CreateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(
            request.Title,
            request.ImageDesktopId,
            request.ImageMobileId,
            request.AltText,
            request.LinkUrl,
            request.LinkLabel,
            request.StartsAt,
            request.EndsAt);
        if (error is not null)
        {
            return Invalid(error);
        }

        var lastOrder = await database.Banners
            .Select(banner => (int?)banner.DisplayOrder)
            .MaxAsync(cancellationToken) ?? -1;
        var banner = new Banner
        {
            Title = CmsContentRules.NormalizeOptional(request.Title),
            Subtitle = CmsContentRules.NormalizeOptional(request.Subtitle),
            ImageDesktopId = request.ImageDesktopId,
            ImageMobileId = request.ImageMobileId,
            AltText = CmsContentRules.NormalizeOptional(request.AltText),
            LinkUrl = CmsContentRules.NormalizeOptional(request.LinkUrl),
            LinkLabel = CmsContentRules.NormalizeOptional(request.LinkLabel),
            DisplayOrder = lastOrder + 1,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true
        };

        database.Banners.Add(banner);
        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<BannerAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(banner, clock.GetUtcNow()));
    }

    internal async Task<CmsOperation<BannerAdminResponse>> UpdateAsync(
        int id,
        UpdateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(
            request.Title,
            request.ImageDesktopId,
            request.ImageMobileId,
            request.AltText,
            request.LinkUrl,
            request.LinkLabel,
            request.StartsAt,
            request.EndsAt);
        if (error is not null)
        {
            return Invalid(error);
        }

        var banner = await database.Banners.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (banner is null)
        {
            return new CmsOperation<BannerAdminResponse>(CmsOutcome.NotFound);
        }

        banner.Title = CmsContentRules.NormalizeOptional(request.Title);
        banner.Subtitle = CmsContentRules.NormalizeOptional(request.Subtitle);
        banner.ImageDesktopId = request.ImageDesktopId;
        banner.ImageMobileId = request.ImageMobileId;
        banner.AltText = CmsContentRules.NormalizeOptional(request.AltText);
        banner.LinkUrl = CmsContentRules.NormalizeOptional(request.LinkUrl);
        banner.LinkLabel = CmsContentRules.NormalizeOptional(request.LinkLabel);
        banner.StartsAt = request.StartsAt;
        banner.EndsAt = request.EndsAt;

        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<BannerAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(banner, clock.GetUtcNow()));
    }

    internal async Task<CmsOperation<BannerAdminResponse>> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var banner = await database.Banners.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (banner is null)
        {
            return new CmsOperation<BannerAdminResponse>(CmsOutcome.NotFound);
        }

        if (banner.IsActive)
        {
            banner.IsActive = false;
            await database.SaveChangesAsync(cancellationToken);
        }

        return new CmsOperation<BannerAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(banner, clock.GetUtcNow()));
    }

    internal Task<CmsOperation<IReadOnlyList<int>>> ReorderAsync(
        ReorderCmsRequest request,
        CancellationToken cancellationToken)
        => order.ReorderAsync(
            database.Banners,
            request.OrderedIds,
            (banner, position) => banner.DisplayOrder = position,
            cancellationToken);

    private string? Validate(
        string? title,
        Guid? desktopImageId,
        Guid? mobileImageId,
        string? altText,
        string? linkUrl,
        string? linkLabel,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
        => CmsContentRules.ValidateOptionalText(title, "El título")
           ?? CmsContentRules.ValidatePeriod(startsAt, endsAt)
           ?? CmsContentRules.ValidateLink(linkUrl, linkLabel)
           ?? CmsContentRules.ValidateAltText(desktopImageId is not null || mobileImageId is not null, altText)
           ?? ValidateMedia(desktopImageId, "La imagen de escritorio")
           ?? ValidateMedia(mobileImageId, "La imagen móvil");

    private string? ValidateMedia(Guid? id, string label)
        => id is not null && MediaUrl(id) is null
            ? $"{label} indicada no existe o no está activa."
            : null;

    private string? MediaUrl(Guid? id) => id is { } value ? media.GetPublicUrl(value) : null;

    private BannerAdminResponse Project(Banner banner, DateTimeOffset now)
    {
        var desktopUrl = MediaUrl(banner.ImageDesktopId);
        return new BannerAdminResponse(
            banner.Id,
            banner.Title,
            banner.Subtitle,
            banner.ImageDesktopId,
            desktopUrl,
            banner.ImageMobileId,
            MediaUrl(banner.ImageMobileId),
            banner.AltText,
            banner.LinkUrl,
            banner.LinkLabel,
            banner.DisplayOrder,
            banner.StartsAt,
            banner.EndsAt,
            banner.IsActive,
            PublicationWindow.IsCurrent(banner, now),
            desktopUrl is not null && !string.IsNullOrWhiteSpace(banner.AltText));
    }

    private static CmsOperation<BannerAdminResponse> Invalid(string error)
        => new(CmsOutcome.Invalid, error);
}
