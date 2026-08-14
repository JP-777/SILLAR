namespace Sillar.Core.Authentication;

/// <summary>Resultado de comprobar una contraseña contra la política.</summary>
/// <param name="Error">Motivo del rechazo, en español, o <c>null</c> si sirve.</param>
public sealed record PasswordCheck(string? Error)
{
    /// <summary>La contraseña cumple la política.</summary>
    public bool IsValid => Error is null;

    /// <summary>Contraseña aceptada.</summary>
    public static readonly PasswordCheck Ok = new((string?)null);
}

/// <summary>
/// Política de contraseñas: longitud por encima de composición.
/// </summary>
/// <remarks>
/// Sigue la recomendación vigente del NIST. Exigir mayúsculas, dígitos y
/// símbolos produce contraseñas peores y anotadas en un papel bajo el teclado;
/// en un mostrador de librería, ese papel existe. Tampoco hay caducidad: rotar
/// cada noventa días lleva a <c>Verano2026!</c> seguido de <c>Otono2026!</c>.
/// </remarks>
public static class PasswordPolicy
{
    /// <summary>Longitud mínima.</summary>
    public const int MinimumLength = 12;

    /// <summary>Longitud máxima, para no aceptar entradas absurdas.</summary>
    /// <remarks>
    /// BCrypt solo considera los primeros 72 bytes: aceptar más daría la falsa
    /// impresión de que una contraseña larguísima aporta seguridad extra.
    /// </remarks>
    public const int MaximumLength = 72;

    /// <summary>Comprueba una contraseña contra la política.</summary>
    /// <param name="password">Contraseña propuesta.</param>
    /// <param name="email">Correo del usuario.</param>
    /// <param name="fullName">Nombre completo del usuario.</param>
    public static PasswordCheck Check(string? password, string email, string fullName)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            return new PasswordCheck($"La contraseña debe tener al menos {MinimumLength} caracteres.");
        }

        if (password.Length > MaximumLength)
        {
            return new PasswordCheck($"La contraseña no puede pasar de {MaximumLength} caracteres.");
        }

        if (CommonPasswords.Contains(password))
        {
            return new PasswordCheck("Esa contraseña es demasiado común. Elige otra.");
        }

        if (ContainsIdentity(password, email, fullName))
        {
            return new PasswordCheck("La contraseña no puede contener tu nombre ni tu correo.");
        }

        return PasswordCheck.Ok;
    }

    /// <summary>
    /// Detecta si la contraseña incluye el correo, la parte anterior a la arroba
    /// o alguna palabra del nombre.
    /// </summary>
    private static bool ContainsIdentity(string password, string email, string fullName)
    {
        var candidate = password.ToLowerInvariant();

        foreach (var fragment in IdentityFragments(email, fullName))
        {
            if (candidate.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> IdentityFragments(string email, string fullName)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (normalizedEmail.Length > 0)
        {
            yield return normalizedEmail;

            var at = normalizedEmail.IndexOf('@');
            if (at > 0)
            {
                yield return normalizedEmail[..at];
            }
        }

        // Palabras del nombre de cuatro letras o más. El umbral no es capricho:
        // buscando como subcadena, un nombre de tres letras veta media lengua.
        // Con «Ana Quispe», la contraseña «mesa lampara ventana» se rechazaría
        // porque 'ana' está dentro de 'ventana'. Se pierde poco: la contraseña
        // ya tiene que medir doce caracteres, así que un nombre corto nunca es
        // la contraseña, y el apellido y el correo se siguen comprobando.
        foreach (var word in fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (word.Length >= 4)
            {
                yield return word.ToLowerInvariant();
            }
        }
    }
}
