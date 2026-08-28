using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Modules.Crm.Authentication;
using Sillar.Core.Contracts.Email;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Dtos;
using Sillar.Modules.Crm.Profiles;

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

        endpoints.MapPost(Prefix + "/register", (Delegate)Register)
            .AddEndpointFilter<AnonymousCsrfEndpointFilter>()
            .WithName("CustomerRegister")
            .WithTags(Tag)
            .WithSummary("Registra una cuenta de cliente.")
            .WithDescription(
                "Crea una ficha o enlaza una existente sin revelar si el correo ya estaba registrado.")
            .Produces<CustomerRegistrationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapPost(
                Prefix + "/password-reset/request",
                (Delegate)RequestPasswordReset)
            .AddEndpointFilter<AnonymousCsrfEndpointFilter>()
            .WithName("CustomerPasswordResetRequest")
            .WithTags(Tag)
            .WithSummary("Solicita recuperación de contraseña.")
            .WithDescription(
                "La respuesta es idéntica exista o no la cuenta.")
            .Produces<CustomerOperationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapPost(
                Prefix + "/password-reset/confirm",
                (Delegate)ConfirmPasswordReset)
            .AddEndpointFilter<AnonymousCsrfEndpointFilter>()
            .WithName("CustomerPasswordResetConfirm")
            .WithTags(Tag)
            .WithSummary("Consume un token y cambia la contraseña.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapPost(
                Prefix + "/email-verification/confirm",
                (Delegate)ConfirmEmailVerification)
            .AddEndpointFilter<AnonymousCsrfEndpointFilter>()
            .WithName("CustomerEmailVerificationConfirm")
            .WithTags(Tag)
            .WithSummary("Consume un token y verifica el correo.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapPost(
                Prefix + "/invitation/accept",
                (Delegate)AcceptInvitation)
            .AddEndpointFilter<AnonymousCsrfEndpointFilter>()
            .WithName("CustomerInvitationAccept")
            .WithTags(Tag)
            .WithSummary("Acepta una invitación y crea la cuenta.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // /me se autentica explícitamente con el esquema cliente porque es
        // anónimo por definición: 'no hay sesión' también es una respuesta.
        endpoints.MapGet(Prefix + "/me", (Delegate)Me)
            .AllowAnonymous()
            .WithName("CustomerMe")
            .WithTags(Tag)
            .WithSummary("Devuelve el cliente en sesión, o null.");

        var session = endpoints.MapGroup(Prefix)
            .WithTags(Tag)
            .RequireAuthorization(CustomerAuthorization.PolicyName)
            .AddEndpointFilter<CustomerCsrfEndpointFilter>();

        session.MapPost(
                "/email-verification/request",
                RequestEmailVerification)
            .WithName("CustomerEmailVerificationRequest")
            .WithSummary("Reenvía la verificación del correo.")
            .Produces<CustomerOperationResponse>(StatusCodes.Status200OK);

        session.MapPost("/logout", Logout)
            .WithName("CustomerLogout")
            .WithSummary("Revoca la sesión de cliente.")
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> Register(
        CustomerRegisterRequest request,
        CustomerRegistrationService registration,
        CustomerAccountTokenService tokens,
        IEmailSender email,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                title: "Revisa los datos del registro.");
        }

        await registration.RegisterAsync(
            request.FullName!,
            request.Email!,
            request.Password!,
            request.Phone,
            cancellationToken);

        var verification = await tokens.IssueEmailVerificationAsync(
            request.Email!,
            cancellationToken);

        if (verification is not null)
        {
            ScheduleEmail(
                context,
                email,
                CustomerEmailComposer.Verification(
                    verification,
                    BaseUrl(context)));
        }

        // Deliberadamente idéntica para Created, Linked y AlreadyRegistered.
        return Results.Ok(
            new CustomerRegistrationResponse(
                "Solicitud de registro procesada."));
    }

    private static Dictionary<string, string[]> ValidateRegistration(
        CustomerRegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;

        if (fullName.Length == 0)
        {
            errors["nombre"] = ["El nombre es obligatorio."];
        }

        if (email.Length == 0
            || email.Length > 150
            || !System.Net.Mail.MailAddress.TryCreate(email, out _))
        {
            errors["correo"] = ["Ingresa un correo válido."];
        }

        if (!errors.ContainsKey("correo")
            && !errors.ContainsKey("nombre"))
        {
            var password = CustomerPasswordPolicy.Check(
                request.Password,
                email,
                fullName);

            if (!password.IsValid)
            {
                errors["contrasena"] = [password.Error!];
            }
        }
        else if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["contrasena"] =
                [$"La contraseña debe tener al menos {CustomerPasswordPolicy.MinimumLength} caracteres."];
        }

        return errors;
    }

    private static async Task<IResult> RequestPasswordReset(
        CustomerPasswordResetRequest request,
        CustomerAccountTokenService tokens,
        IEmailSender email,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var candidate = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(candidate)
            || !System.Net.Mail.MailAddress.TryCreate(candidate, out _))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["correo"] = ["Ingresa un correo válido."]
                },
                title: "Revisa el correo.");
        }

        var issued = await tokens.IssuePasswordResetAsync(
            candidate,
            cancellationToken);

        if (issued is not null)
        {
            ScheduleEmail(
                context,
                email,
                CustomerEmailComposer.PasswordReset(
                    issued,
                    BaseUrl(context)));
        }

        return Results.Ok(
            new CustomerOperationResponse(
                "Si la cuenta corresponde, la solicitud fue procesada."));
    }

    private static async Task<IResult> ConfirmPasswordReset(
        CustomerPasswordResetConfirmRequest request,
        CustomerAccountTokenService tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.Problem(
                title: "El enlace de recuperación no es válido o ya caducó.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await tokens.ResetPasswordAsync(
            request.Token,
            request.NewPassword,
            cancellationToken);

        return result.Outcome switch
        {
            CustomerPasswordTokenOutcome.Success =>
                Results.NoContent(),

            CustomerPasswordTokenOutcome.InvalidPassword =>
                Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["contrasena"] = [result.Error!]
                    },
                    title: "La nueva contraseña no es válida."),

            _ => Results.Problem(
                title: "El enlace de recuperación no es válido o ya caducó.",
                statusCode: StatusCodes.Status400BadRequest)
        };
    }

    private static async Task<IResult> ConfirmEmailVerification(
        CustomerTokenRequest request,
        CustomerAccountTokenService tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)
            || !await tokens.VerifyEmailAsync(
                request.Token,
                cancellationToken))
        {
            return Results.Problem(
                title: "El enlace de verificación no es válido o ya caducó.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RequestEmailVerification(
        CurrentCustomer current,
        CustomerAccountTokenService tokens,
        IEmailSender email,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(current.Email))
        {
            return Results.Unauthorized();
        }

        var issued = await tokens.IssueEmailVerificationAsync(
            current.Email,
            cancellationToken);

        if (issued is not null)
        {
            ScheduleEmail(
                context,
                email,
                CustomerEmailComposer.Verification(
                    issued,
                    BaseUrl(context)));
        }

        return Results.Ok(
            new CustomerOperationResponse(
                "Solicitud de verificación procesada."));
    }

    private static async Task<IResult> AcceptInvitation(
        CustomerInvitationAcceptRequest request,
        CustomerAccountTokenService tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.Problem(
                title: "La invitación no es válida o ya caducó.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await tokens.AcceptInvitationAsync(
            request.Token,
            request.Password,
            cancellationToken);

        return result.Outcome switch
        {
            CustomerPasswordTokenOutcome.Success =>
                Results.NoContent(),

            CustomerPasswordTokenOutcome.InvalidPassword =>
                Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["contrasena"] = [result.Error!]
                    },
                    title: "La contraseña no es válida."),

            _ => Results.Problem(
                title: "La invitación no es válida o ya caducó.",
                statusCode: StatusCodes.Status400BadRequest)
        };
    }

    private static string BaseUrl(HttpContext context)
        => $"{context.Request.Scheme}://{context.Request.Host}";

    private static void ScheduleEmail(
        HttpContext context,
        IEmailSender sender,
        OutgoingEmail message)
    {
        // El hecho ya quedó persistido. Se manda después de entregar la
        // respuesta para que SMTP no forme parte de la transacción ni revele
        // por latencia si una cuenta existe.
        context.Response.OnCompleted(
            async () =>
            {
                await sender.SendAsync(
                    message,
                    CancellationToken.None);
            });
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

        // Token CSRF separado de la credencial de sesión. La cookie de sesión
        // permanece HttpOnly; esta segunda cookie permite reconstruir
        // X-CSRF-Token tras recargar la SPA sin guardar el secreto en localStorage.
        context.Response.Cookies.Append(
            CustomerCsrfCookie.Name,
            attempt.Session.CsrfToken,
            CustomerCsrfCookie.Options());

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

    private static async Task<IResult> Me(
        HttpContext context,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
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

        var profile = await profiles.GetAsync(
            customerId,
            cancellationToken);

        if (profile is null)
        {
            return Results.Content("null", "application/json");
        }

        return Results.Ok(
            new CustomerAuthenticatedResponse(
                profile.CustomerId,
                profile.FullName,
                profile.Email,
                profile.EmailVerified));
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

        context.Response.Cookies.Delete(
            CustomerCsrfCookie.Name,
            CustomerCsrfCookie.Options());

        return Results.NoContent();
    }
}
