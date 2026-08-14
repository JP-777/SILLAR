using System.Security.Cryptography;
using System.Text;

namespace Sillar.Core.Authentication;

/// <summary>Generación, resumen y comparación de los secretos de sesión.</summary>
/// <remarks>
/// <see cref="Hash"/> y <see cref="Matches"/> los usan tanto el token de sesión
/// como el CSRF: ambos se guardan resumidos y se comparan en tiempo constante.
/// Lo que se separa es cómo nacen.
/// </remarks>
public static class SessionTokens
{
    /// <summary>Tamaño del token de sesión: 256 bits.</summary>
    public const int SessionTokenBytes = 32;

    /// <summary>Genera un token de sesión aleatorio en base64url.</summary>
    /// <remarks>
    /// Solo el token de sesión es aleatorio. El CSRF se deriva de la identidad de
    /// la sesión en <see cref="CsrfTokenFactory"/>, para que consultarlo no lo
    /// cambie (ADR-012).
    /// </remarks>
    public static string CreateSessionToken() => Create(SessionTokenBytes);

    /// <summary>Calcula el SHA-256 de un token, en base64.</summary>
    /// <remarks>
    /// SHA-256 y no BCrypt, deliberadamente. BCrypt es lento a propósito para
    /// resistir la fuerza bruta contra secretos de baja entropía, que es lo que
    /// son las contraseñas. Un token de 256 bits aleatorios no se fuerza por
    /// mucho tiempo que se le dé, y aquí hace falta una búsqueda rápida en cada
    /// petición: usar BCrypt sería pagar un coste alto sin ganar nada.
    /// </remarks>
    public static string Hash(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Compara un token con el hash guardado, en tiempo constante.
    /// </summary>
    /// <remarks>
    /// La comparación no puede cortocircuitar en el primer carácter distinto:
    /// el tiempo que tarda revelaría cuántos caracteres se acertaron y
    /// permitiría reconstruir el token a base de intentos.
    /// </remarks>
    public static bool Matches(string? token, string? expectedHash)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(token)),
            Encoding.UTF8.GetBytes(expectedHash));
    }

    private static string Create(int bytes)
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>
    /// Base64 apto para URL y cabeceras: sin '+', sin '/' y sin relleno.
    /// </summary>
    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
