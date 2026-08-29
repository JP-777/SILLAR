using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>
/// Origen público usado en enlaces enviados por correo.
/// Sillar:PublicBaseUrl gana sobre la request. Si no está configurado, la
/// request ya debe haber pasado por ForwardedHeaders para reflejar el proxy.
/// </summary>
internal sealed class CustomerPublicUrlResolver(IConfiguration configuration)
{
    private const string Setting = "Sillar:PublicBaseUrl";

    public string Resolve(HttpContext context)
    {
        var configured = configuration[Setting]?.Trim();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp
                    && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"{Setting} debe ser una URL absoluta http/https.");
            }

            return configured.TrimEnd('/');
        }

        return $"{context.Request.Scheme}://{context.Request.Host}";
    }
}
