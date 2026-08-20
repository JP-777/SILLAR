using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Domain;
using Sillar.Modules.Cms.Dtos;

namespace Sillar.Modules.Cms.Services;

/// <summary>Enlaces sociales públicos y operaciones editoriales.</summary>
internal sealed class SocialLinkService(
    CmsDbContext database,
    CmsOrderService order)
{
    internal async Task<IReadOnlyList<SocialLinkResponse>> ListPublicAsync(
        CancellationToken cancellationToken)
        => await database.SocialLinks.AsNoTracking()
            .Where(link => link.IsActive)
            .OrderBy(link => link.DisplayOrder)
            .ThenBy(link => link.Id)
            .Select(link => new SocialLinkResponse(link.Id, link.Platform, link.Url))
            .ToListAsync(cancellationToken);

    internal async Task<IReadOnlyList<SocialLinkAdminResponse>> ListAsync(
        CancellationToken cancellationToken)
        => await database.SocialLinks.AsNoTracking()
            .OrderBy(link => link.DisplayOrder)
            .ThenBy(link => link.Id)
            .Select(link => new SocialLinkAdminResponse(
                link.Id,
                link.Platform,
                link.Url,
                link.DisplayOrder,
                link.IsActive))
            .ToListAsync(cancellationToken);

    internal Task<SocialLinkAdminResponse?> GetAsync(int id, CancellationToken cancellationToken)
        => database.SocialLinks.AsNoTracking()
            .Where(link => link.Id == id)
            .Select(link => new SocialLinkAdminResponse(
                link.Id,
                link.Platform,
                link.Url,
                link.DisplayOrder,
                link.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    internal async Task<CmsOperation<SocialLinkAdminResponse>> CreateAsync(
        CreateSocialLinkRequest request,
        CancellationToken cancellationToken)
    {
        var error = CmsContentRules.ValidateSocialLink(request.Platform, request.Url);
        if (error is not null)
        {
            return Invalid(error);
        }

        var lastOrder = await database.SocialLinks
            .Select(link => (int?)link.DisplayOrder)
            .MaxAsync(cancellationToken) ?? -1;
        var link = new SocialLink
        {
            Platform = CmsContentRules.NormalizePlatform(request.Platform)!,
            Url = request.Url!.Trim(),
            DisplayOrder = lastOrder + 1,
            IsActive = true
        };

        database.SocialLinks.Add(link);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Duplicate(link.Platform);
        }

        return new CmsOperation<SocialLinkAdminResponse>(CmsOutcome.Ok, Value: Project(link));
    }

    internal async Task<CmsOperation<SocialLinkAdminResponse>> UpdateAsync(
        int id,
        UpdateSocialLinkRequest request,
        CancellationToken cancellationToken)
    {
        var error = CmsContentRules.ValidateSocialLink(request.Platform, request.Url);
        if (error is not null)
        {
            return Invalid(error);
        }

        var link = await database.SocialLinks.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (link is null)
        {
            return new CmsOperation<SocialLinkAdminResponse>(CmsOutcome.NotFound);
        }

        link.Platform = CmsContentRules.NormalizePlatform(request.Platform)!;
        link.Url = request.Url!.Trim();
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Duplicate(link.Platform);
        }

        return new CmsOperation<SocialLinkAdminResponse>(CmsOutcome.Ok, Value: Project(link));
    }

    internal async Task<CmsOperation<SocialLinkAdminResponse>> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var link = await database.SocialLinks.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (link is null)
        {
            return new CmsOperation<SocialLinkAdminResponse>(CmsOutcome.NotFound);
        }

        if (link.IsActive)
        {
            link.IsActive = false;
            await database.SaveChangesAsync(cancellationToken);
        }

        return new CmsOperation<SocialLinkAdminResponse>(CmsOutcome.Ok, Value: Project(link));
    }

    internal Task<CmsOperation<IReadOnlyList<int>>> ReorderAsync(
        ReorderCmsRequest request,
        CancellationToken cancellationToken)
        => order.ReorderAsync(
            database.SocialLinks,
            request.OrderedIds,
            (link, position) => link.DisplayOrder = position,
            cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_social_links_plataforma"
        };

    private static SocialLinkAdminResponse Project(SocialLink link) => new(
        link.Id,
        link.Platform,
        link.Url,
        link.DisplayOrder,
        link.IsActive);

    private static CmsOperation<SocialLinkAdminResponse> Invalid(string error)
        => new(CmsOutcome.Invalid, error);

    private static CmsOperation<SocialLinkAdminResponse> Duplicate(string platform)
        => new(
            CmsOutcome.Conflict,
            $"Ya existe un enlace para {platform}. Solo puede haber una cuenta por red.");
}
