using Microsoft.AspNetCore.Http;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>Cookie de sesión de la tienda.</summary>
internal static class CustomerSessionCookie
{
    public const string Name = "sillar_tienda";

    public static CookieOptions Options() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
    };
}

/// <summary>
/// Cookie separada con el token CSRF bruto de la tienda.
/// </summary>
/// <remarks>
/// A diferencia de la cookie de sesión, debe ser legible por JavaScript para
/// reconstruir la cabecera X-CSRF-Token después de una recarga. No autentica
/// por sí sola: el servidor compara su valor hasheado con la sesión persistida.
/// </remarks>
internal static class CustomerCsrfCookie
{
    public const string Name = "sillar_tienda_csrf";

    public static CookieOptions Options() => new()
    {
        HttpOnly = false,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
    };
}

/// <summary>Claims privados de una sesión de cliente.</summary>
internal static class CustomerSessionClaims
{
    public const string CustomerId = "sillar:customer:customer_id";
    public const string AccountId = "sillar:customer:account_id";
    public const string SessionId = "sillar:customer:session_id";
    public const string Email = "sillar:customer:email";
    public const string EmailVerified = "sillar:customer:email_verified";
}
