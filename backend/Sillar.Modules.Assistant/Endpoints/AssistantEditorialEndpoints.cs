using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Endpoints;

/// <summary>
/// Rutas administrativas preparadas pero NO montadas en Sillar.Api.
/// Requieren autoridad de instalación y auditoría CORE antes de integrarse.
/// </summary>
public static class AssistantEditorialEndpoints
{
    /// <summary>Declara edición protegida con sesión administrativa y CSRF.</summary>
    public static IEndpointRouteBuilder MapAssistantEditorialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/assistant/articles")
            .WithTags("Asistente — Editorial")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapPost("", Create)
            .WithName("CreateAssistantArticle")
            .WithSummary("Crea un artículo de conocimiento en la instalación autorizada.")
            .Produces<AssistantEditorialWriteReply>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet("/{articleId:guid}", Inspect)
            .WithName("InspectAssistantArticle")
            .WithSummary("Consulta el borrador y publicación del artículo autorizado.")
            .Produces<AssistantEditorialArticleReply>()
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{articleId:guid}/draft", SaveDraft)
            .WithName("SaveAssistantArticleDraft")
            .WithSummary("Guarda una revisión de borrador con control de versión.")
            .Produces<AssistantEditorialWriteReply>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{articleId:guid}/publish", Publish)
            .WithName("PublishAssistantArticle")
            .WithSummary("Publica la revisión vigente del borrador.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<AssistantEditorialWriteReply>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{articleId:guid}/withdraw", Withdraw)
            .WithName("WithdrawAssistantArticle")
            .WithSummary("Retira la revisión publicada sin borrar el historial.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<AssistantEditorialWriteReply>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    /// <summary>Crea una identidad de artículo en el servidor, nunca desde un parámetro del usuario.</summary>
    public static Task<IResult> Create(ITrustedEditorialAdminScope scope,
        IAssistantEditorialAdministration service, CancellationToken ct)
        => ExecuteSafely(async () =>
    {
        if (!TryActor(scope, false, out var actor)) return Results.NotFound();
        var articleId = Guid.NewGuid();
        var result = await service.CreateAsync(actor!, articleId, ct);
        if (result == EditorialResult.Saved)
            return Results.Created($"/api/admin/assistant/articles/{articleId}",
                new AssistantEditorialWriteReply("saved", articleId, null, 0));
        return Failure(result);
    }, ct);

    /// <summary>Consulta una única instalación desde una identidad previamente autorizada.</summary>
    public static Task<IResult> Inspect(Guid articleId, ITrustedEditorialAdminScope scope,
        IAssistantEditorialAdministration service, CancellationToken ct)
        => ExecuteSafely(async () =>
    {
        if (!TryActor(scope, false, out var actor) || articleId == Guid.Empty)
            return Results.NotFound();
        var result = await service.InspectAsync(actor!, articleId, ct);
        if (result.Status != EditorialLookupStatus.Found || result.Article is not { } article)
            return Results.NotFound();
        return Results.Ok(new AssistantEditorialArticleReply(article.ArticleId, article.Version,
            article.Draft is { } d ? new AssistantEditorialDraftReply(
                d.RevisionId, d.Question, d.Answer, d.Title, d.PublicUri.AbsoluteUri) : null,
            article.Published is { } p ? new AssistantEditorialPublishedReply(
                p.RevisionId, p.Question, p.Answer, p.Title, p.PublicUri.AbsoluteUri,
                p.ArticleVersion, p.PublishedAt) : null));
    }, ct);

    /// <summary>Un borrador se versiona sin revelar escritor, rol o instalación en el body.</summary>
    public static Task<IResult> SaveDraft(Guid articleId, AssistantDraftRequest? request,
        ITrustedEditorialAdminScope scope, IAssistantEditorialAdministration service,
        CancellationToken ct)
        => ExecuteSafely(async () =>
    {
        if (!TryActor(scope, false, out var actor)) return Results.NotFound();
        if (articleId == Guid.Empty || !ValidDraftRequest(request, out var uri)) return BadRequest();
        var revisionId = Guid.NewGuid();
        var draft = new KnowledgeDraft(revisionId, request!.Question!, request.Answer!,
            request.Title!, uri!);
        var result = await service.SaveDraftAsync(actor!, articleId, draft, request.ExpectedVersion, ct);
        return result == EditorialResult.Saved
            ? Results.Ok(new AssistantEditorialWriteReply("saved", articleId,
                revisionId, request.ExpectedVersion + 1))
            : Failure(result);
    }, ct);

    /// <summary>Publicación solo para admin/super_admin, además del filtro de autorización del host.</summary>
    public static Task<IResult> Publish(Guid articleId, AssistantPublicationRequest? request,
        ITrustedEditorialAdminScope scope, IAssistantEditorialAdministration service,
        CancellationToken ct)
        => ExecuteSafely(async () =>
    {
        if (!TryActor(scope, true, out var actor)) return Results.NotFound();
        if (articleId == Guid.Empty || !ValidPublicationRequest(request)) return BadRequest();
        var result = await service.PublishAsync(actor!, articleId,
            request!.RevisionId, request.ExpectedVersion, ct);
        return result == EditorialResult.Published
            ? Results.Ok(new AssistantEditorialWriteReply("published", articleId,
                request.RevisionId, request.ExpectedVersion + 1))
            : Failure(result);
    }, ct);

    /// <summary>Retirada solo para admin/super_admin; nunca elimina la revisión histórica.</summary>
    public static Task<IResult> Withdraw(Guid articleId, AssistantPublicationRequest? request,
        ITrustedEditorialAdminScope scope, IAssistantEditorialAdministration service,
        CancellationToken ct)
        => ExecuteSafely(async () =>
    {
        if (!TryActor(scope, true, out var actor)) return Results.NotFound();
        if (articleId == Guid.Empty || !ValidPublicationRequest(request)) return BadRequest();
        var result = await service.WithdrawAsync(actor!, articleId,
            request!.RevisionId, request.ExpectedVersion, ct);
        return result == EditorialResult.Withdrawn
            ? Results.Ok(new AssistantEditorialWriteReply("withdrawn", articleId,
                request.RevisionId, request.ExpectedVersion + 1))
            : Failure(result);
    }, ct);

    // El proveedor puede incluir contenido editorial, preguntas o datos de conexión
    // en cualquier excepción. No entregarlos a respuestas ni al manejador global.
    // Un fallo de escritura deja resultado incierto: nunca informar éxito ni reintentar.
    private static async Task<IResult> ExecuteSafely(Func<Task<IResult>> action, CancellationToken ct)
    {
        try { return await action(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Preservar la cancelación sin mensaje, Data ni InnerException del proveedor.
            throw new OperationCanceledException(ct);
        }
        catch
        {
            return Results.Problem(title: "No se confirmó la operación editorial.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool TryActor(ITrustedEditorialAdminScope scope, bool requireAdmin,
        out EditorialPrincipal? actor)
    {
        actor = null;
        if (scope is null) return false;
        try
        {
            if (!scope.TryGetPrincipal(out var principal) || principal is null ||
                principal.InstallationId == Guid.Empty || !principal.HasArticleWriteAuthority ||
                string.IsNullOrWhiteSpace(principal.WriterNode)) return false;
            if (requireAdmin && principal.Role is not (EditorialRoles.Admin or EditorialRoles.SuperAdmin))
                return false;
            if (principal.Role is not (EditorialRoles.Editor or EditorialRoles.Admin or EditorialRoles.SuperAdmin))
                return false;
            actor = principal;
            return true;
        }
        catch { return false; }
    }

    private static bool ValidPublicationRequest(AssistantPublicationRequest? request)
        => request is { RevisionId: var id, ExpectedVersion: >= 0 and < long.MaxValue } &&
           id != Guid.Empty;

    private static bool ValidDraftRequest(AssistantDraftRequest? request, out Uri? uri)
    {
        uri = null;
        return request is { ExpectedVersion: >= 0 and < long.MaxValue } &&
            !string.IsNullOrWhiteSpace(request.Question) && request.Question.Length <= 500 &&
            !string.IsNullOrWhiteSpace(request.Answer) && request.Answer.Length <= 10000 &&
            !string.IsNullOrWhiteSpace(request.Title) && request.Title.Length <= 200 &&
            !string.IsNullOrWhiteSpace(request.PublicUrl) && request.PublicUrl.Length <= 2048 &&
            Uri.TryCreate(request.PublicUrl, UriKind.Absolute, out uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.Port == 443 &&
            uri.HostNameType != UriHostNameType.Unknown;
    }

    private static IResult BadRequest() => TypedResults.ValidationProblem(
        new Dictionary<string, string[]> { ["editorial"] =
            ["Los datos o la versión editorial no son válidos."] });

    private static IResult Failure(EditorialResult result) => result switch
    {
        EditorialResult.Forbidden => Results.NotFound(),
        EditorialResult.Conflict => Results.Problem(
            title: "La versión o la identidad editorial está en conflicto.",
            statusCode: StatusCodes.Status409Conflict),
        EditorialResult.Invalid => BadRequest(),
        _ => Results.Problem(title: "No se confirmó la operación editorial.",
            statusCode: StatusCodes.Status503ServiceUnavailable)
    };
}
