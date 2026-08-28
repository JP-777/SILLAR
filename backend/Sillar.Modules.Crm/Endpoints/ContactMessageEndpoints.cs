using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Contact;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Endpoints;

/// <summary>Formulario público y bandeja administrativa de contacto.</summary>
public static class ContactMessageEndpoints
{
    public static IEndpointRouteBuilder MapContactMessageEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/contact",
                Submit)
            .WithTags("CRM — Contacto")
            .WithName("SubmitPublicContact")
            .WithSummary("Recibe un mensaje del formulario público.")
            .WithDescription(
                "Exige Origin del mismo origen. Si existe una sesión de cliente válida, vincula la ficha; si no, el mensaje sigue siendo anónimo.")
            .AddEndpointFilter<AnonymousCsrfEndpointFilter>()
            .Produces<PublicContactAcceptedResponse>(
                StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);

        var admin = endpoints.MapGroup(
                "/api/admin/crm/contact-messages")
            .WithTags("CRM — Contacto")
            .RequireAuthorization(AdminRole.Admin)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", ListAdmin)
            .WithName("ListAdminContactMessages")
            .WithSummary("Lista los mensajes de contacto.")
            .WithDescription(
                "Por defecto muestra solo los activos; includeInactive=true incluye la baja lógica.")
            .Produces<IReadOnlyList<AdminContactMessageListItemResponse>>(
                StatusCodes.Status200OK);

        admin.MapGet("/{contactMessageId:int}", GetAdmin)
            .WithName("GetAdminContactMessage")
            .WithSummary("Muestra el mensaje completo.")
            .Produces<AdminContactMessageDetailResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status404NotFound);

        admin.MapDelete("/{contactMessageId:int}", Deactivate)
            .WithName("DeactivateAdminContactMessage")
            .WithSummary("Da de baja lógicamente un mensaje.")
            .Produces<AdminContactMessageDetailResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> Submit(
        PublicContactRequest request,
        ContactMessageService contacts,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        Guid? customerId = null;

        var customerAuthentication =
            await context.AuthenticateAsync(
                CustomerSessionAuthenticationHandler.SchemeName);

        if (customerAuthentication.Succeeded
            && Guid.TryParse(
                customerAuthentication.Principal?
                    .FindFirst(
                        CustomerSessionClaims.CustomerId)?
                    .Value,
                out var authenticatedCustomerId))
        {
            customerId = authenticatedCustomerId;
        }

        var result = await contacts.SubmitAsync(
            request,
            customerId,
            context.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (result.Outcome
            == ContactMessageOutcome.RateLimited)
        {
            if (result.RetryAfter is { } retryAfter)
            {
                context.Response.Headers.RetryAfter =
                    Math.Max(
                        1,
                        (int)Math.Ceiling(
                            retryAfter.TotalSeconds))
                    .ToString();
            }

            return Results.Problem(
                title: result.Error,
                statusCode:
                    StatusCodes.Status429TooManyRequests);
        }

        if (result.Outcome
            == ContactMessageOutcome.Invalid)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["contacto"] = [result.Error!]
                },
                title:
                    "Los datos de contacto no son válidos.");
        }

        return Results.Accepted(
            value: new PublicContactAcceptedResponse(
                "Recibimos tu mensaje."));
    }

    private static async Task<IResult> ListAdmin(
        bool? includeInactive,
        ContactMessageService contacts,
        CancellationToken cancellationToken)
        => Results.Ok(
            await contacts.ListAdminAsync(
                includeInactive == true,
                cancellationToken));

    private static async Task<IResult> GetAdmin(
        int contactMessageId,
        ContactMessageService contacts,
        CancellationToken cancellationToken)
    {
        var message = await contacts.GetAdminAsync(
            contactMessageId,
            cancellationToken);

        return message is null
            ? Results.NotFound()
            : Results.Ok(message);
    }

    private static async Task<IResult> Deactivate(
        int contactMessageId,
        ContactMessageService contacts,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var result = await contacts.DeactivateAsync(
            contactMessageId,
            current.AdminUserId!.Value,
            current.Email!,
            cancellationToken);

        return result.Outcome
            == ContactMessageOutcome.Ok
            ? Results.Ok(result.Contact)
            : Results.NotFound();
    }
}
