using Microsoft.AspNetCore.Http;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>
/// Protección CSRF para escrituras anteriores a una sesión, como login.
/// </summary>
/// <remarks>
/// Todavía no existe un secreto de sesión que comprobar. En navegador se exige
/// una señal de mismo origen. Sec-Fetch-Site es la señal preferida porque la
/// calcula el navegador antes de atravesar proxies; para clientes que no la
/// envían se conserva la comparación estricta de Origin contra scheme + host.
/// </remarks>
internal sealed class AnonymousCsrfEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        // Fetch Metadata se calcula en el navegador antes del reverse proxy.
        // Así sigue describiendo el origen real aunque Vite/Traefik cambien
        // el Host interno al reenviar la petición.
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();

        if (string.Equals(
                fetchSite,
                "same-origin",
                StringComparison.OrdinalIgnoreCase))
        {
            return await next(context);
        }

        // Fallback para clientes/navegadores que no envían Fetch Metadata.
        var origin = request.Headers.Origin.ToString();

        if (Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
            && string.Equals(
                parsed.Scheme,
                request.Scheme,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                parsed.Authority,
                request.Host.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            return await next(context);
        }

        return Results.Problem(
            title: "Origen de la petición no permitido.",
            detail: "Las operaciones públicas que modifican datos requieren una petición del mismo origen.",
            statusCode: StatusCodes.Status403Forbidden);
    }
}
