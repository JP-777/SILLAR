using Microsoft.AspNetCore.Http;
using Sillar.Core.Contracts;

namespace Sillar.Modules.Crm.Contracts;

/// <summary>
/// Exige el token CSRF de la sesión de cliente en peticiones que modifican datos.
/// </summary>
/// <remarks>
/// No acepta el claim administrativo. Las dos poblaciones comparten únicamente
/// la cabecera HTTP, no la identidad ni el hash contra el que se compara.
/// </remarks>
public sealed class CustomerCsrfEndpointFilter : IEndpointFilter
{
    /// <summary>Cabecera donde viaja el token.</summary>
    public const string HeaderName = "X-CSRF-Token";

    /// <summary>Claim exclusivo del hash CSRF de la sesión de cliente.</summary>
    public const string ClaimType = "sillar:customer:csrf_hash";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method))
        {
            return await next(context);
        }

        var sent = request.Headers[HeaderName].ToString();
        var expected = context.HttpContext.User.FindFirst(ClaimType)?.Value;

        if (!SessionTokens.Matches(sent, expected))
        {
            return Results.Problem(
                title: "Falta el token CSRF o no es válido.",
                detail: $"Envía la cabecera {HeaderName} con el token de la sesión de cliente.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
