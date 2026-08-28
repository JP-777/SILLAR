using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Endpoints;

/// <summary>Autenticación de la clientela de la tienda.</summary>
public static class CustomerAuthEndpoints
{
    private const string Prefix = "/api/customer/auth";
    private const string Tag = "Autenticación de clientes";
    private const string AccessDenied = "Correo o contraseña incorrectos.";

    public static IEndpointRouteBuilder MapCustomerAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Prefix + "/login", Login)
            .AddEndpointFilter<AnonymousCsrfEndpointFilter>()
            .WithName("CustomerLogin")
            .WithTags(Tag)
            .WithSummary("Abre una sesión de cliente.")
            .WithDescription(
                "La respuesta de rechazo no permite distinguir si el correo existe.")
            .Produces<CustomerLoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // /me se autentica explícitamente con el esquema cliente porque es
        // anónimo por definición: 'no hay sesión' también es una respuesta.
        endpoints.MapGet(Prefix + "/me", Me)
            .AllowAnonymous()
            .WithName("CustomerMe")
            .WithTags(Tag)
            .WithSummary("Devuelve el cliente en sesión, o null.");

        var session = endpoints.MapGroup(Prefix)
            .WithTags(Tag)
            .RequireAuthorization(CustomerAuthorization.PolicyName)
            .AddEndpointFilter<CustomerCsrfEndpointFilter>();

        session.MapPost("/logout", Logout)
            .WithName("CustomerLogout")
            .WithSummary("Revoca la sesión de cliente.")
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> Login(
        CustomerLoginRequest request,
        CustomerAuthenticationService authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrEmpty(request.Password))
        {
            return Results.Problem(
                title: AccessDenied,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var attempt = await authentication.LoginAsync(
            request.Email,
            request.Password,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (attempt.Outcome != CustomerLoginOutcome.Granted)
        {
            return Results.Problem(
                title: AccessDenied,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        context.Response.Cookies.Append(
            CustomerSessionCookie.Name,
            attempt.Session!.SessionToken,
            CustomerSessionCookie.Options());

        var customer = attempt.Customer!;

        return Results.Ok(
            new CustomerLoginResponse(
                new CustomerAuthenticatedResponse(
                    customer.CustomerId,
                    customer.FullName,
                    customer.Email,
                    customer.EmailVerified),
                attempt.Session.CsrfToken));
    }

    private static async Task<IResult> Me(HttpContext context)
    {
        var authentication = await context.AuthenticateAsync(
            CustomerSessionAuthenticationHandler.SchemeName);

        if (!authentication.Succeeded
            || authentication.Principal is null)
        {
            return Results.Content("null", "application/json");
        }

        var principal = authentication.Principal;

        if (!Guid.TryParse(
                principal.FindFirst(CustomerSessionClaims.CustomerId)?.Value,
                out var customerId))
        {
            return Results.Content("null", "application/json");
        }

        var email =
            principal.FindFirst(CustomerSessionClaims.Email)?.Value
            ?? string.Empty;

        var verified = bool.TryParse(
            principal.FindFirst(
                CustomerSessionClaims.EmailVerified)?.Value,
            out var isVerified)
            && isVerified;

        // El nombre completo no vive en claims todavía. /me solo devuelve la
        // identidad mínima; las pantallas de perfil vendrán en la siguiente unidad.
        return Results.Ok(
            new CustomerAuthenticatedResponse(
                customerId,
                string.Empty,
                email,
                verified));
    }

    private static async Task<IResult> Logout(
        CustomerSessionService sessions,
        CurrentCustomer current,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (current.SessionId is not { } sessionId
            || current.AccountId is not { } accountId)
        {
            return Results.Unauthorized();
        }

        await sessions.LogoutAsync(
            sessionId,
            accountId,
            cancellationToken);

        context.Response.Cookies.Delete(
            CustomerSessionCookie.Name,
            CustomerSessionCookie.Options());

        return Results.NoContent();
    }
}
