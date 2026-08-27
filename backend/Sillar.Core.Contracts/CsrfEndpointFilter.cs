using Microsoft.AspNetCore.Http;

namespace Sillar.Core.Contracts;

/// <summary>
/// Exige el token CSRF en toda petición que modifique datos.
/// </summary>
/// <remarks>
/// Es la contrapartida obligatoria de autenticar con cookies: el navegador
/// adjunta la cookie sola, así que sin esto bastaría con que alguien con sesión
/// abierta visitara una página ajena que envíe un formulario contra el panel.
///
/// <c>SameSite=Strict</c> ayuda mucho, pero no es suficiente por sí solo: no
/// cubre subdominios comprometidos ni navegadores que lo apliquen de forma laxa.
///
/// Vive en <c>Sillar.Core.Contracts</c> y no dentro de CORE: todo módulo con
/// endpoints de escritura lo necesita —el SPEC de M01 §8 lo exige igual que
/// CORE—, y <c>AddEndpointFilter&lt;T&gt;()</c> necesita el tipo concreto, no
/// una interfaz resoluble por inyección. Es el mismo motivo que ya puso
/// <c>ICurrentAdmin</c> aquí.
/// </remarks>
public sealed class CsrfEndpointFilter : IEndpointFilter
{
    /// <summary>Cabecera donde viaja el token.</summary>
    public const string HeaderName = "X-CSRF-Token";

    /// <summary>
    /// Tipo del claim donde viaja el hash del token CSRF de la sesión administrativa, para que
    /// este filtro no vuelva a consultar la base de datos.
    /// </summary>
    public const string ClaimType = "sillar:admin:csrf_hash";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        // Los métodos que solo leen no necesitan token. Si un GET modificara
        // algo, el problema sería el GET.
        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method))
        {
            return await next(context);
        }

        var sent = request.Headers[HeaderName].ToString();
        var expected = context.HttpContext.User.FindFirst(ClaimType)?.Value;

        // Comparación en tiempo constante: un cortocircuito al primer carácter
        // distinto permitiría reconstruir el token a base de intentos.
        if (!SessionTokens.Matches(sent, expected))
        {
            return Results.Problem(
                title: "Falta el token CSRF o no es válido.",
                detail: $"Envía la cabecera {HeaderName} con el token que devolvió el inicio de sesión.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
