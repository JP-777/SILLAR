using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Domain;
using Sillar.Modules.Cms.Dtos;

namespace Sillar.Modules.Cms.Services;

/// <summary>Trabajos destacados públicos y operaciones editoriales.</summary>
internal sealed class FeaturedProjectService(
    CmsDbContext database,
    IMediaStorage media,
    CmsOrderService order)
{
    internal async Task<IReadOnlyList<FeaturedProjectResponse>> ListPublicAsync(
        CancellationToken cancellationToken)
    {
        var projects = await database.FeaturedProjects.AsNoTracking()
            .Where(project => project.IsActive && project.ImageId != null)
            .OrderBy(project => project.DisplayOrder)
            .ThenBy(project => project.Id)
            .ToListAsync(cancellationToken);

        var published = new List<FeaturedProjectResponse>(projects.Count);
        foreach (var project in projects)
        {
            var imageUrl = MediaUrl(project.ImageId);
            if (!FeaturedProjectRules.IsComplete(project, imageUrl))
            {
                continue;
            }

            published.Add(new FeaturedProjectResponse(
                project.Id,
                project.Title,
                project.Description,
                imageUrl!,
                project.AltText!));
        }

        return published;
    }

    internal async Task<IReadOnlyList<FeaturedProjectAdminResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var projects = await database.FeaturedProjects.AsNoTracking()
            .OrderBy(project => project.DisplayOrder)
            .ThenBy(project => project.Id)
            .ToListAsync(cancellationToken);
        return [.. projects.Select(Project)];
    }

    internal async Task<FeaturedProjectAdminResponse?> GetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var project = await database.FeaturedProjects.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return project is null ? null : Project(project);
    }

    internal async Task<CmsOperation<FeaturedProjectAdminResponse>> CreateAsync(
        CreateFeaturedProjectRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request.Title, request.ImageId, request.AltText);
        if (error is not null)
        {
            return Invalid(error);
        }

        var lastOrder = await database.FeaturedProjects
            .Select(project => (int?)project.DisplayOrder)
            .MaxAsync(cancellationToken) ?? -1;
        var project = new FeaturedProject
        {
            Title = request.Title!.Trim(),
            Description = CmsContentRules.NormalizeOptional(request.Description),
            ImageId = request.ImageId,
            AltText = CmsContentRules.NormalizeOptional(request.AltText),
            DisplayOrder = lastOrder + 1,
            IsActive = true
        };

        database.FeaturedProjects.Add(project);
        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<FeaturedProjectAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(project));
    }

    internal async Task<CmsOperation<FeaturedProjectAdminResponse>> UpdateAsync(
        int id,
        UpdateFeaturedProjectRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request.Title, request.ImageId, request.AltText);
        if (error is not null)
        {
            return Invalid(error);
        }

        var project = await database.FeaturedProjects.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (project is null)
        {
            return new CmsOperation<FeaturedProjectAdminResponse>(CmsOutcome.NotFound);
        }

        project.Title = request.Title!.Trim();
        project.Description = CmsContentRules.NormalizeOptional(request.Description);
        project.ImageId = request.ImageId;
        project.AltText = CmsContentRules.NormalizeOptional(request.AltText);
        await database.SaveChangesAsync(cancellationToken);

        return new CmsOperation<FeaturedProjectAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(project));
    }

    internal async Task<CmsOperation<FeaturedProjectAdminResponse>> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var project = await database.FeaturedProjects.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (project is null)
        {
            return new CmsOperation<FeaturedProjectAdminResponse>(CmsOutcome.NotFound);
        }

        if (project.IsActive)
        {
            project.IsActive = false;
            await database.SaveChangesAsync(cancellationToken);
        }

        return new CmsOperation<FeaturedProjectAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(project));
    }

    internal Task<CmsOperation<IReadOnlyList<int>>> ReorderAsync(
        ReorderCmsRequest request,
        CancellationToken cancellationToken)
        => order.ReorderAsync(
            database.FeaturedProjects,
            request.OrderedIds,
            (project, position) => project.DisplayOrder = position,
            cancellationToken);

    private string? Validate(string? title, Guid? imageId, string? altText)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "El título del trabajo es obligatorio.";
        }

        return CmsContentRules.ValidateAltText(imageId is not null, altText)
               ?? (imageId is not null && MediaUrl(imageId) is null
                   ? "La imagen indicada no existe o no está activa."
                   : null);
    }

    private string? MediaUrl(Guid? id) => id is { } value ? media.GetPublicUrl(value) : null;

    private FeaturedProjectAdminResponse Project(FeaturedProject project)
    {
        var imageUrl = MediaUrl(project.ImageId);
        return new FeaturedProjectAdminResponse(
            project.Id,
            project.Title,
            project.Description,
            project.ImageId,
            imageUrl,
            project.AltText,
            project.DisplayOrder,
            project.IsActive,
            FeaturedProjectRules.IsComplete(project, imageUrl));
    }

    private static CmsOperation<FeaturedProjectAdminResponse> Invalid(string error)
        => new(CmsOutcome.Invalid, error);
}
