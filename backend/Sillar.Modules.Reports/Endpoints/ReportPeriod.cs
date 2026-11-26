using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Sillar.Modules.Reports.Endpoints;

/// <summary>
/// El período [inicio, fin) que reciben todos los indicadores: instantes ISO 8601
/// con desplazamiento explícito, para no depender de la zona del navegador, del
/// servidor ni de la instancia.
/// </summary>
internal static class ReportPeriod
{
    private static readonly Regex ExplicitOffset = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Lee el período. Si no es válido, <paramref name="problem"/> es el 400 que
    /// hay que devolver y no se consulta ninguna fuente.
    /// </summary>
    public static bool TryRead(string? startText, string? endText,
        out DateTimeOffset start, out DateTimeOffset end, out IResult? problem)
    {
        end = default;
        if (!TryReadInstant(startText, out start) || !TryReadInstant(endText, out end))
        {
            problem = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["periodo"] = ["Indica inicio y fin ISO 8601, cada uno con zona horaria explícita (Z o ±HH:MM)."]
            });
            return false;
        }
        if (end <= start)
        {
            problem = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["periodo"] = ["El fin debe ser posterior al inicio. El intervalo es [inicio, fin)."]
            });
            return false;
        }
        problem = null;
        return true;
    }

    private static bool TryReadInstant(string? text, out DateTimeOffset instant)
    {
        instant = default;
        return text is not null
            && ExplicitOffset.IsMatch(text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out instant);
    }
}
