using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Dtos;
using Sillar.Core.Services;

namespace Sillar.Core.Endpoints;

/// <summary>Inicio y cierre de sesión, y cambio de la contraseña propia.</summary>
public static class AuthEndpoints
{
    private const string Prefix = "/api/admin/auth";
    private const string Tag = "Autenticación";

    /// <summary>
    /// Mismo mensaje para correo inexistente, contraseña incorrecta y cuenta
    /// desactivada.
    /// </summary>
    /// <remarks>
    /// Cualquier diferencia —de texto, de código o de tiempo— convierte el
    /// formulario de acceso en un comprobador de qué correos están registrados.
    /// </remarks>
    private const string AccessDenied = "Correo o contraseña incorrectos.";

    /// <summary>Monta las rutas de autenticación.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var anonymous = endpoints.MapGroup(Prefix).WithTags(Tag);

        anonymous.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Abre una sesión administrativa.")
            .WithDescription(
                "Devuelve la cookie de sesión y el token CSRF. El mensaje de error es idéntico " +
                "exista o no la cuenta.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

        var session = endpoints.MapGroup(Prefix)
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        session.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Cierra la sesión.")
            .WithDescription("Revoca la fila en base de datos y borra la cookie. Lo que cierra la sesión es la revocación.")
            .Produces(StatusCodes.Status204NoContent);

        // **«Quién soy» se responde siempre, y «nadie» es una respuesta.**
        //
        // Estaba dentro del grupo autorizado, así que un visitante sin sesión
        // recibía 401 — y el navegador lo apunta como error de consola pase lo
        // que pase, porque es la respuesta HTTP y no algo que el código pueda
        // tragarse. La aplicación pregunta esto **al arrancar en cualquier
        // ruta**, así que **cada visita a la tienda dejaba errores** por una
        // pregunta cuya respuesta correcta era «no hay sesión».
        //
        // No se cambia cuándo se pregunta —hacerlo solo en el panel obligaría
        // a volver a preguntarlo al navegar de la tienda al panel sin
        // recargar, y ahí se pierde la sesión— sino qué se responde.
        //
        // No revela nada: quien pregunta ya trae la cookie, y lo único que
        // averigua es si la suya vale.
        session.MapGet("/me", Me)
            .AllowAnonymous()
            .WithName("Me")
            .WithSummary("Devuelve el usuario en sesión, o nulo si no hay ninguna.")
            .Produces<AuthenticatedUserResponse>(StatusCodes.Status200OK);

        session.MapGet("/csrf", Csrf)
            .WithName("RefreshCsrfToken")
            .WithSummary("Emite un token CSRF nuevo para la sesión activa.")
            .WithDescription("Para cuando el frontend se recarga y pierde el que recibió al iniciar sesión. El anterior deja de valer.")
            .Produces<CsrfTokenResponse>(StatusCodes.Status200OK);

        session.MapPost("/change-password", ChangePassword)
            .WithName("ChangePassword")
            .WithSummary("Cambia la contraseña propia.")
            .WithDescription("Exige la contraseña actual y revoca las demás sesiones del usuario.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        return endpoints;
    }

    /// <summary>Inicia sesión.</summary>
    /// <param name="request">Correo y contraseña.</param>
    /// <param name="authentication">Servicio de autenticación.</param>
    /// <param name="context">Petición en curso, para la cookie y el navegador declarado.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el usuario y el token CSRF, 401 genérico, o 423 si la cuenta está bloqueada.</returns>
    private static async Task<IResult> Login(
        LoginRequest request,
        AdminAuthenticationService authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.Problem(title: AccessDenied, statusCode: StatusCodes.Status401Unauthorized);
        }

        var attempt = await authentication.LoginAsync(
            request.Email.Trim(),
            request.Password,
            context.Request.Headers.UserAgent.ToString(),
            cancellationToken);

        switch (attempt.Outcome)
        {
            case LoginOutcome.Locked:
                // Solo quien acertó la contraseña llega hasta aquí, así que se le
                // puede decir cuándo podrá entrar sin revelar nada a un extraño.
                return Results.Problem(
                    title: "La cuenta está bloqueada temporalmente por varios intentos fallidos.",
                    detail: $"Vuelve a intentarlo a partir de {attempt.LockedUntil:HH:mm}.",
                    statusCode: StatusCodes.Status423Locked);

            case LoginOutcome.Granted:
                context.Response.Cookies.Append(SessionCookie.Name, attempt.SessionToken!, SessionCookie.Options());

                var user = attempt.User!;
                return Results.Ok(new LoginResponse(
                    new AuthenticatedUserResponse(user.AdminUserId, user.FullName, user.Email, user.Role),
                    attempt.CsrfToken!));

            default:
                // UnknownEmail, WrongPassword e Inactive comparten respuesta.
                return Results.Problem(title: AccessDenied, statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    /// <summary>Cierra la sesión en curso.</summary>
    /// <param name="authentication">Servicio de autenticación.</param>
    /// <param name="currentUser">Usuario en sesión.</param>
    /// <param name="context">Petición en curso, para borrar la cookie.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>204 siempre que hubiera sesión.</returns>
    private static async Task<IResult> Logout(
        AdminAuthenticationService authentication,
        CurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (currentUser.SessionId is not { } sessionId)
        {
            return Results.Unauthorized();
        }

        await authentication.LogoutAsync(
            sessionId,
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        context.Response.Cookies.Delete(SessionCookie.Name, SessionCookie.Options());

        return Results.NoContent();
    }

    /// <summary>Devuelve el usuario en sesión.</summary>
    /// <param name="currentUser">Usuario en sesión.</param>
    /// <param name="context">Petición en curso.</param>
    /// <returns>Identificador, nombre, correo y rol. Nunca el hash de la contraseña.</returns>
    private static IResult Me(CurrentUser currentUser, HttpContext context)
        => currentUser.AdminUserId is null
            // Sin sesión: 200 con un `null` **escrito**, no con cuerpo vacío.
            // `Results.Ok(null)` manda `Content-Length: 0`, que para quien lee
            // no es «no hay nadie» sino «no hay respuesta» — y el cliente lo
            // recibía como `undefined` en vez de `null`.
            // Escrito a mano porque ni `Results.Ok(null)` ni `Results.Json(null)`
            // llegan a escribir nada: los dos mandan `Content-Length: 0`, y un
            // cuerpo vacío no dice «no hay nadie», dice «no hay respuesta».
            ? Results.Content("null", "application/json")
            : Results.Ok(new AuthenticatedUserResponse(
                currentUser.AdminUserId.Value,
                context.User.Identity?.Name ?? string.Empty,
                currentUser.Email!,
                currentUser.Role!));

    /// <summary>Emite un token CSRF nuevo para la sesión activa.</summary>
    /// <param name="authentication">Servicio de autenticación.</param>
    /// <param name="currentUser">Usuario en sesión.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>El token nuevo, o 401 si la sesión ya no vale.</returns>
    private static async Task<IResult> Csrf(
        AdminAuthenticationService authentication,
        CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var token = await authentication.RefreshCsrfTokenAsync(currentUser.SessionId!.Value, cancellationToken);

        return token is null
            ? Results.Unauthorized()
            : Results.Ok(new CsrfTokenResponse(token));
    }

    /// <summary>Cambia la contraseña propia.</summary>
    /// <param name="request">Contraseña actual y nueva.</param>
    /// <param name="authentication">Servicio de autenticación.</param>
    /// <param name="currentUser">Usuario en sesión.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>204 si se cambió, 400 con el motivo si no.</returns>
    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        AdminAuthenticationService authentication,
        CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var error = await authentication.ChangePasswordAsync(
            currentUser.AdminUserId!.Value,
            currentUser.SessionId!.Value,
            request.CurrentPassword ?? string.Empty,
            request.NewPassword,
            cancellationToken);

        return error is null
            ? Results.NoContent()
            : Results.ValidationProblem(
                new Dictionary<string, string[]> { ["contrasena"] = [error] },
                title: "No se pudo cambiar la contraseña.");
    }
}
