using System.Security.Cryptography;
using System.Text;

namespace Sillar.Core.Authentication;

/// <summary>
/// Calcula el token CSRF de una sesión, derivándolo de su identidad (ADR-012).
/// </summary>
/// <remarks>
/// El token es una <b>función</b> de la sesión, no un estado guardado:
///
/// <code>
/// claveCsrf = HKDF( installation_key, info = "sillar-csrf-v1" )
/// csrfToken = base64url( HMAC-SHA256( claveCsrf, admin_session_id ) )
/// </code>
///
/// Eso es lo que hace idempotente a <c>GET /api/admin/auth/csrf</c>. Con un token
/// aleatorio no había forma: como en base de datos solo vive su hash, el valor
/// original es irrecuperable y cada llamada tenía que emitir uno nuevo,
/// invalidando el de las demás pestañas. Un panel de administración se usa en
/// varias pestañas a la vez, así que aquello convertía el 403 en un estado
/// esperado del frontend.
///
/// La clave sale de <c>core.installation.installation_key</c> y no de un valor
/// generado al arrancar: así sobrevive a los reinicios y las sesiones vivas
/// conservan su token cuando se reinicia el proceso.
/// </remarks>
public sealed class CsrfTokenFactory
{
    /// <summary>
    /// Etiqueta de contexto de la derivación.
    /// </summary>
    /// <remarks>
    /// Lleva versión a propósito: subirla a <c>v2</c> invalida de golpe todos los
    /// tokens CSRF sin tocar <c>installation_key</c>, que identifica la
    /// instalación y no debería rotarse por este motivo.
    /// </remarks>
    public const string DerivationInfo = "sillar-csrf-v1";

    /// <summary>Longitud de la clave derivada, en bytes.</summary>
    private const int KeyLength = 32;

    private readonly byte[] _key;

    /// <summary>
    /// Deriva la clave CSRF de la identidad de la instalación.
    /// </summary>
    /// <param name="installationKey">
    /// Valor de <c>core.installation.installation_key</c>.
    /// </param>
    /// <remarks>
    /// Solo se construye en modo normal, después de leer <c>core.installation</c>
    /// durante el arranque. En modo instalación esa fila no existe todavía y
    /// tampoco hace falta: <c>/api/setup</c> no tiene sesión que proteger.
    /// </remarks>
    public CsrfTokenFactory(Guid installationKey)
    {
        if (installationKey == Guid.Empty)
        {
            throw new ArgumentException(
                "La clave de instalación está vacía. La clave CSRF se deriva de core.installation, " +
                "así que solo puede calcularse después de leer esa fila.",
                nameof(installationKey));
        }

        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: ToBytes(installationKey),
            outputLength: KeyLength,
            salt: null,
            info: Encoding.UTF8.GetBytes(DerivationInfo));
    }

    /// <summary>Devuelve el token CSRF que corresponde a una sesión.</summary>
    /// <param name="adminSessionId">Identificador de la sesión.</param>
    /// <remarks>
    /// Determinista: la misma sesión da siempre el mismo token, y dos procesos
    /// distintos de la misma instalación calculan el mismo valor.
    /// </remarks>
    public string Create(Guid adminSessionId)
        => Base64UrlEncode(HMACSHA256.HashData(_key, ToBytes(adminSessionId)));

    /// <summary>
    /// Convierte un <see cref="Guid"/> a bytes en orden big-endian.
    /// </summary>
    /// <remarks>
    /// El orden por defecto de <see cref="Guid.ToByteArray()"/> depende de la
    /// plataforma: invierte los tres primeros campos en arquitecturas little
    /// endian. Como el desarrollo alterna entre Windows y Arch Linux, dejarlo al
    /// azar produciría tokens distintos en cada máquina para la misma sesión.
    /// </remarks>
    private static byte[] ToBytes(Guid value) => value.ToByteArray(bigEndian: true);

    /// <summary>Base64 apto para viajar en una cabecera: sin '+', '/' ni relleno.</summary>
    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
