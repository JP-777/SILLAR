using System.Globalization;
using System.Text;

namespace Sillar.Modules.Crm.Authentication;

internal sealed record CustomerPasswordCheck(string? Error)
{
    public bool IsValid => Error is null;
    public static readonly CustomerPasswordCheck Ok = new((string?)null);
}

/// <summary>
/// Política de contraseña de la clientela.
/// Mantiene la misma filosofía del panel: longitud antes que composición.
/// </summary>
internal static class CustomerPasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 72;

    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012",
        "1234567890123",
        "12345678901234",
        "111111111111",
        "000000000000",
        "abcdefghijkl",
        "qwertyuiopas",
        "qwertyuiop12",
        "asdfghjkl123",
        "password1234",
        "passwordpassword",
        "contrasena12",
        "contrasena123",
        "contrasena1234",
        "micontrasena",
        "administrador",
        "administrador1",
        "administrador123",
        "bienvenido12",
        "bienvenido123",
        "iloveyou1234",
        "letmein12345",
        "sillar123456",
        "sillaradmin1",
        "peru12345678",
        "lima12345678",
        "arequipa1234",
        "libreria1234",
        "papeleria123",
        "negocio12345"
    };

    public static CustomerPasswordCheck Check(
        string? password,
        string email,
        string fullName)
    {
        if (string.IsNullOrWhiteSpace(password)
            || password.Length < MinimumLength)
        {
            return new CustomerPasswordCheck(
                $"La contraseña debe tener al menos {MinimumLength} caracteres.");
        }

        if (password.Length > MaximumLength)
        {
            return new CustomerPasswordCheck(
                $"La contraseña no puede pasar de {MaximumLength} caracteres.");
        }

        if (Common.Contains(RemoveMarks(password)))
        {
            return new CustomerPasswordCheck(
                "Esa contraseña es demasiado común. Elige otra.");
        }

        if (ContainsIdentity(password, email, fullName))
        {
            return new CustomerPasswordCheck(
                "La contraseña no puede contener tu nombre ni tu correo.");
        }

        return CustomerPasswordCheck.Ok;
    }

    private static bool ContainsIdentity(
        string password,
        string email,
        string fullName)
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

    private static IEnumerable<string> IdentityFragments(
        string email,
        string fullName)
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

        foreach (var word in fullName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries))
        {
            if (word.Length >= 4)
            {
                yield return word.ToLowerInvariant();
            }
        }
    }

    private static string RemoveMarks(string value)
        => string.Concat(
            value.Normalize(NormalizationForm.FormD)
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character)
                    is not UnicodeCategory.NonSpacingMark));
}
