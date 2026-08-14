using System.Globalization;
using System.Text.Json;
using Sillar.Core.Authentication;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Settings;

/// <summary>Comprueba que un valor encaje con el tipo declarado de su clave.</summary>
/// <remarks>
/// La comprobación vive aquí y no en la base de datos porque <c>value_type</c> es
/// una columna, no un tipo de PostgreSQL: un CHECK que valide seis formatos
/// distintos según el valor de otra columna sería ilegible y difícil de cambiar.
/// </remarks>
internal static class SettingValueValidator
{
    /// <summary>
    /// Valores que se aceptan como booleanos, además de los que entiende .NET.
    /// </summary>
    /// <remarks>
    /// El panel enviará <c>true</c> y <c>false</c>, pero estas claves también se
    /// tocan a mano desde SQL y desde el seed.
    /// </remarks>
    private static readonly string[] TrueValues = ["true", "1", "si", "sí", "yes"];
    private static readonly string[] FalseValues = ["false", "0", "no"];

    /// <summary>Valida el valor y devuelve el motivo del rechazo, o <c>null</c>.</summary>
    /// <param name="valueType">Tipo declarado de la clave.</param>
    /// <param name="value">Valor propuesto.</param>
    public static string? Validate(string valueType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // Los textos obligatorios no vacíos son convención del proyecto: una
            // clave sin valor es una clave a medio configurar.
            return "El valor no puede quedar vacío. Para retirar una configuración se desactiva, no se vacía.";
        }

        var trimmed = value.Trim();

        return valueType switch
        {
            SettingValueType.Number when !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                => "Se esperaba un número.",

            SettingValueType.Boolean when !IsBoolean(trimmed)
                => $"Se esperaba un booleano: {string.Join(", ", TrueValues.Concat(FalseValues))}.",

            SettingValueType.Url when !IsUrl(trimmed)
                => "Se esperaba una dirección web que empiece por http:// o https://.",

            SettingValueType.Email when !EmailAddress.IsValid(trimmed)
                => "Se esperaba un correo electrónico.",

            SettingValueType.Json when !IsJson(trimmed)
                => "Se esperaba un documento JSON válido.",

            _ => null
        };
    }

    private static bool IsBoolean(string value)
        => TrueValues.Contains(value, StringComparer.OrdinalIgnoreCase)
            || FalseValues.Contains(value, StringComparer.OrdinalIgnoreCase);

    /// <summary>Interpreta un valor booleano ya validado.</summary>
    public static bool AsBoolean(string value)
        => TrueValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool IsUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
