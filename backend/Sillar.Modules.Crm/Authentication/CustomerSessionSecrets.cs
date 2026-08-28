using System.Security.Cryptography;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>
/// Genera secretos propios de las sesiones de cliente.
/// </summary>
/// <remarks>
/// No reutiliza CsrfTokenFactory de CORE: esa fábrica pertenece a las sesiones
/// administrativas y deriva sus tokens de la identidad de instalación.
/// M04 mantiene su propio ciclo de vida.
/// </remarks>
internal static class CustomerSessionSecrets
{
    private const int TokenBytes = 32;

    /// <summary>Genera un token CSRF aleatorio de 256 bits.</summary>
    public static string CreateCsrfToken()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
