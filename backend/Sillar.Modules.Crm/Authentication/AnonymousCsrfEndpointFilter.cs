using Microsoft.AspNetCore.Http;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>
/// Protección CSRF para escrituras anteriores a una sesión, como login.
/// </summary>
/// <remarks>
/// Todavía no existe un secreto de sesión que comprobar. En navegador se exige
/// Origin y debe coincidir exactamente con scheme + host de la petición.
/// </remarks>
internal sealed class AnonymousCsrfEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var origin = request.Headers.Origin.ToString();

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
            || !string.Equals(
                parsed.Scheme,
                request.Scheme,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                parsed.Authority,
                request.Host.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                title: "Origen de la petición no permitido.",
                detail: "Las operaciones públicas que modifican datos requieren una petición del mismo origen.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
