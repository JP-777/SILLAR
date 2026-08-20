using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sillar.Modules.Catalog.Services;

/// <summary>
/// Genera y valida el slug: la parte legible de la URL pública (ADR-016 regla
/// 2 — el <c>uuid</c> nunca se muestra).
/// </summary>
/// <remarks>
/// Se llama una sola vez, al crear (SPEC regla 3). Editar el nombre después
/// nunca vuelve a generarlo: moverlo rompería los enlaces que ya se
/// compartieron. Quien llama puede corregirlo a mano si quiere, pero eso pasa
/// por el mismo <see cref="IsValidFormat"/> que usa la base.
/// </remarks>
public static partial class SlugGenerator
{
    // El mismo patrón que Check.SlugFormat exige en la migración: solo
    // a-z0-9, sin guion al principio ni al final, sin dos seguidos.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidFormatPattern { get; }

    /// <summary>Si el texto ya cumple el formato que exige el <c>CHECK</c> de la base.</summary>
    public static bool IsValidFormat(string? slug) => !string.IsNullOrEmpty(slug) && ValidFormatPattern.IsMatch(slug);

    /// <summary>
    /// Deriva un slug de un nombre: minúsculas, sin tildes, solo <c>a-z0-9-</c>.
    /// </summary>
    /// <remarks>
    /// La <c>ñ</c> se pliega a <c>n</c> a propósito: el formato del slug no
    /// admite otra cosa, y es distinto de la identidad —donde sí importa, y
    /// donde ninguna colación del proyecto la iguala a la <c>n</c>—. Un nombre
    /// sin ningún carácter <c>a-z0-9</c> devuelve cadena vacía, que
    /// <see cref="IsValidFormat"/> rechaza: quien llama decide qué hacer, esta
    /// función no inventa un slug de repuesto.
    /// </remarks>
    public static string From(string name)
    {
        var decomposed = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasHyphen = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                // La marca diacrítica que dejó la descomposición NFD: es lo que
                // hace que "Peña" pliegue a "pena" en vez de a "pea".
                continue;
            }

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(ch);
                lastWasHyphen = false;
                continue;
            }

            if (builder.Length > 0 && !lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        if (lastWasHyphen)
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}
