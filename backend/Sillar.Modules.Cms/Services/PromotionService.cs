using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Domain;
using Sillar.Modules.Cms.Dtos;

namespace Sillar.Modules.Cms.Services;

/// <summary>Promociones públicas y operaciones editoriales, sin transporte HTTP.</summary>
internal sealed class PromotionService(
    CmsDbContext database,
    IMediaStorage media,
    CmsOrderService order,
    TimeProvider clock)
{
    internal async Task<IReadOnlyList<PromotionResponse>> ListPublicAsync(CancellationToken cancellationToken)
    {
        var promotions = await database.Promotions.AsNoTracking()
            .Where(PublicationWindow.CurrentAt<Promotion>(clock.GetUtcNow()))
            .OrderBy(promotion => promotion.DisplayOrder)
            .ThenBy(promotion => promotion.Id)
            .ToListAsync(cancellationToken);

        return [.. promotions.Select(promotion => new PromotionResponse(
            promotion.Id,
            promotion.Title,
            promotion.Subtitle,
            promotion.Description,
            promotion.BadgeText,
            MediaUrl(promotion.ImageId),
            promotion.AltText,
            promotion.LinkUrl,
            promotion.LinkLabel))];
    }

    internal async Task<IReadOnlyList<PromotionAdminResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var promotions = await database.Promotions.AsNoTracking()
            .OrderBy(promotion => promotion.DisplayOrder)
            .ThenBy(promotion => promotion.Id)
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return [.. promotions.Select(promotion => Project(promotion, now))];
    }

    internal async Task<PromotionAdminResponse?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var promotion = await database.Promotions.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return promotion is null ? null : Project(promotion, clock.GetUtcNow());
    }

    internal async Task<CmsOperation<PromotionAdminResponse>> CreateAsync(
        CreatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request.Title, request.BadgeText, request.ImageId, request.AltText,
            request.LinkUrl, request.LinkLabel, request.StartsAt, request.EndsAt);
        if (error is not null)
        {
            return Invalid(error);
        }

        var lastOrder = await database.Promotions
            .Select(promotion => (int?)promotion.DisplayOrder)
            .MaxAsync(cancellationToken) ?? -1;
        var promotion = new Promotion
        {
            Title = CmsContentRules.NormalizeOptional(request.Title),
            Subtitle = CmsContentRules.NormalizeOptional(request.Subtitle),
            Description = CmsContentRules.NormalizeOptional(request.Description),
            BadgeText = CmsContentRules.NormalizeOptional(request.BadgeText),
            ImageId = request.ImageId,
            AltText = CmsContentRules.NormalizeOptional(request.AltText),
            LinkUrl = CmsContentRules.NormalizeOptional(request.LinkUrl),
            LinkLabel = CmsContentRules.NormalizeOptional(request.LinkLabel),
            DisplayOrder = lastOrder + 1,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true
        };

        database.Promotions.Add(promotion);
        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<PromotionAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(promotion, clock.GetUtcNow()));
    }

    internal async Task<CmsOperation<PromotionAdminResponse>> UpdateAsync(
        int id,
        UpdatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request.Title, request.BadgeText, request.ImageId, request.AltText,
            request.LinkUrl, request.LinkLabel, request.StartsAt, request.EndsAt);
        if (error is not null)
        {
            return Invalid(error);
        }

        var promotion = await database.Promotions.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (promotion is null)
        {
            return new CmsOperation<PromotionAdminResponse>(CmsOutcome.NotFound);
        }

        promotion.Title = CmsContentRules.NormalizeOptional(request.Title);
        promotion.Subtitle = CmsContentRules.NormalizeOptional(request.Subtitle);
        promotion.Description = CmsContentRules.NormalizeOptional(request.Description);
        promotion.BadgeText = CmsContentRules.NormalizeOptional(request.BadgeText);
        promotion.ImageId = request.ImageId;
        promotion.AltText = CmsContentRules.NormalizeOptional(request.AltText);
        promotion.LinkUrl = CmsContentRules.NormalizeOptional(request.LinkUrl);
        promotion.LinkLabel = CmsContentRules.NormalizeOptional(request.LinkLabel);
        promotion.StartsAt = request.StartsAt;
        promotion.EndsAt = request.EndsAt;

        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<PromotionAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(promotion, clock.GetUtcNow()));
    }

    internal async Task<CmsOperation<PromotionAdminResponse>> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var promotion = await database.Promotions.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (promotion is null)
        {
            return new CmsOperation<PromotionAdminResponse>(CmsOutcome.NotFound);
        }

        if (promotion.IsActive)
        {
            promotion.IsActive = false;
            await database.SaveChangesAsync(cancellationToken);
        }

        return new CmsOperation<PromotionAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(promotion, clock.GetUtcNow()));
    }

    internal Task<CmsOperation<IReadOnlyList<int>>> ReorderAsync(
        ReorderCmsRequest request,
        CancellationToken cancellationToken)
        => order.ReorderAsync(
            database.Promotions,
            request.OrderedIds,
            (promotion, position) => promotion.DisplayOrder = position,
            cancellationToken);

    private string? Validate(
        string? title,
        string? badgeText,
        Guid? imageId,
        string? altText,
        string? linkUrl,
        string? linkLabel,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
        => CmsContentRules.ValidateOptionalText(title, "El título")
           ?? ValidateBadge(badgeText)
           ?? CmsContentRules.ValidatePeriod(startsAt, endsAt)
           ?? CmsContentRules.ValidateLink(linkUrl, linkLabel)
           ?? CmsContentRules.ValidateAltText(imageId is not null, altText)
           ?? ValidateMedia(imageId);

    private static string? ValidateBadge(string? badgeText)
        => badgeText is not null && string.IsNullOrWhiteSpace(badgeText)
            ? "La etiqueta no puede quedar vacía."
            : badgeText?.Trim().Length > 20
                ? "La etiqueta admite como máximo 20 caracteres."
                : null;

    private string? ValidateMedia(Guid? id)
        => id is not null && MediaUrl(id) is null
            ? "La imagen indicada no existe o no está activa."
            : null;

    private string? MediaUrl(Guid? id) => id is { } value ? media.GetPublicUrl(value) : null;

    private PromotionAdminResponse Project(Promotion promotion, DateTimeOffset now) => new(
        promotion.Id,
        promotion.Title,
        promotion.Subtitle,
        promotion.Description,
        promotion.BadgeText,
        promotion.ImageId,
        MediaUrl(promotion.ImageId),
        promotion.AltText,
        promotion.LinkUrl,
        promotion.LinkLabel,
        promotion.DisplayOrder,
        promotion.StartsAt,
        promotion.EndsAt,
        promotion.IsActive,
        PublicationWindow.IsCurrent(promotion, now));

    private static CmsOperation<PromotionAdminResponse> Invalid(string error)
        => new(CmsOutcome.Invalid, error);
}
