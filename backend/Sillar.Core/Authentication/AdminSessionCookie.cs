using Microsoft.AspNetCore.Http;

namespace Sillar.Core.Authentication;

/// <summary>La cookie de sesión administrativa.</summary>
public static class AdminSessionCookie
{
    /// <summary>Nombre de la cookie.</summary>
    public const string Name = "sillar_panel";

    /// <summary>Opciones con las que se emite y se borra.</summary>
    /// <remarks>
    /// <c>HttpOnly</c>: JavaScript no la ve, así que un XSS en el panel —riesgo
    /// real donde se edita contenido— no puede robar la sesión.
    ///
    /// <c>Secure</c> siempre, también en desarrollo: los navegadores tratan
    /// <c>localhost</c> como contexto seguro y aceptan la cookie sobre HTTP. Si
    /// alguien ve un problema de sesión en local, no es por esto; quitarlo sería
    /// un error.
    ///
    /// Sin <c>MaxAge</c> ni <c>Expires</c>: muere al cerrar el navegador. La
    /// autoridad sobre la vigencia es la fila de <c>core.admin_sessions</c>, no
    /// el navegador. Quien deja el mostrador y cierra la ventana, cierra sesión
    /// en ese equipo.
    /// </remarks>
    public static CookieOptions Options() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
    };
}

/// <summary>Datos de la sesión administrativa que viajan en el principal de la petición.</summary>
/// <remarks>
/// El claim del hash CSRF vive en <see cref="Contracts.CsrfEndpointFilter.ClaimType"/>,
/// no aquí: es quien lo lee, y todo módulo con endpoints de escritura necesita
/// el mismo nombre.
/// </remarks>
internal static class AdminSessionClaims
{
    /// <summary>Identificador de la fila de sesión.</summary>
    public const string SessionId = "sillar:admin:session_id";
}
