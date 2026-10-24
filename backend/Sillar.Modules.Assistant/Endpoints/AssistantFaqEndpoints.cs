using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Endpoints;

/// <summary>Única consulta pública preparada: la instalación no procede del visitante.</summary>
public static class AssistantFaqEndpoints
{
    /// <summary>
    /// Declara POST /api/assistant/faq sin activarlo en el host.
    /// El host debe proporcionar explícitamente contexto confiable de instalación,
    /// activación/licencia, fuente editorial autoritativa y TimeProvider.
    /// No habilitar en producción con almacenamiento volátil.
    /// </summary>
    public static IEndpointRouteBuilder MapAssistantFaqEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assistant/faq", HandleFaqAsync)
            .WithTags("Asistente — FAQ")
            .WithName("PostAssistantFaq")
            .WithSummary("Responde únicamente con contenido público vigente de la instalación autorizada.")
            .Produces<AssistantFaqReply>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }

    /// <summary>
    /// Frontera pública asíncrona: usa consultas de publicaciones EF revalidadas;
    /// nunca acepta instalación del request. La conexión física no está activada.
    /// </summary>
    public static async Task<IResult> HandleFaqAsync(
        AssistantFaqRequest? request,
        ITrustedAssistantInstallationScope scope,
        IAsyncPublishedFaqReader faq,
        TimeProvider clock,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(faq);
        ArgumentNullException.ThrowIfNull(clock);
        Guid installationId;
        try
        {
            if (!scope.TryGetEnabledInstallation(out installationId) || installationId == Guid.Empty)
                return Results.NotFound();
        }
        catch
        {
            return Results.NotFound();
        }
        if (request is null || string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 500)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pregunta"] = ["La pregunta es obligatoria y no puede superar 500 caracteres."]
            });
        FaqResult answer;
        try
        {
            answer = await faq.AnswerAsync(installationId, request.Question, clock.GetUtcNow(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Results.Problem(title: "La fuente pública no está disponible para verificación.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        if (answer is null)
            return Results.Problem(title: "La fuente pública no está disponible para verificación.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        if (answer.Outcome == FaqOutcome.Answered)
        {
            if (string.IsNullOrWhiteSpace(answer.Answer) ||
                string.IsNullOrWhiteSpace(answer.SourceTitle) ||
                !Uri.TryCreate(answer.PublicUrl, UriKind.Absolute, out var url) ||
                url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0)
                return Results.Problem(title: "La fuente pública no está disponible para verificación.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            return Results.Ok(new AssistantFaqReply("answered", answer.Answer,
                answer.SourceTitle, answer.PublicUrl));
        }
        if (answer.Outcome == FaqOutcome.Insufficient)
            return Results.Ok(new AssistantFaqReply("insufficient",
                FaqResult.Abstain(FaqOutcome.Insufficient).Answer, null, null));
        return Results.Problem(title: "La fuente pública no está disponible para verificación.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>Compatibilidad del servicio FAQ volátil, ya no montado por MapAssistantFaqEndpoints.</summary>
    /// <summary>Control de frontera reutilizable sin iniciar servidor HTTP.</summary>
    public static IResult HandleFaq(
        AssistantFaqRequest? request,
        ITrustedAssistantInstallationScope scope,
        FaqService faq,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(faq);
        ArgumentNullException.ThrowIfNull(clock);

        // Antes de examinar el cuerpo: no se permite que el cliente elija instalación,
        // ni que una instalación desactivada descubra el estado interno del módulo.
        Guid installationId;
        try
        {
            if (!scope.TryGetEnabledInstallation(out installationId) || installationId == Guid.Empty)
                return Results.NotFound();
        }
        catch
        {
            // Falla cerrada: nunca sustituir autoridad CORE por un header, cookie o body.
            return Results.NotFound();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Question) ||
            request.Question.Length > 500)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pregunta"] = ["La pregunta es obligatoria y no puede superar 500 caracteres."]
            });

        var answer = faq.Answer(installationId, request.Question, clock.GetUtcNow());
        return answer.Outcome switch
        {
            FaqOutcome.Answered => Results.Ok(new AssistantFaqReply(
                "answered", answer.Answer, answer.SourceTitle, answer.PublicUrl)),
            FaqOutcome.Insufficient => Results.Ok(new AssistantFaqReply(
                "insufficient", FaqResult.Abstain(FaqOutcome.Insufficient).Answer, null, null)),
            _ => Results.Problem(
                title: "La fuente pública no está disponible para verificación.",
                statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }
}
