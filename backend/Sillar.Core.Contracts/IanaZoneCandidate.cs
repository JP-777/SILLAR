namespace Sillar.Core.Contracts;

/// <summary>
/// Valor de zona horaria IANA guardado para la instalación. Fase A, candidato:
/// clave privada, sin semilla y sin claim público.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reconocible no es verificada.</b> Que el runtime resuelva la zona solo dice
/// que existe en la base de zonas de esta máquina: un alias se resuelve igual que
/// un identificador canónico. Lo que hace falta para verificarla lo decide
/// <see cref="IInstallationTimeZoneResolver"/>, no esta evaluación.
/// </para>
/// <para>
/// Hoy ninguna semilla ni migración crea la fila de <see cref="SettingKey"/>, y el
/// API de configuración solo actualiza filas que ya existen: el valor está
/// siempre ausente.
/// </para>
/// </remarks>
public static class IanaZoneCandidate
{
    /// <summary>Clave candidata en <c>core.site_settings</c>.</summary>
    public const string SettingKey = "installation.timezone_iana";

    /// <summary>
    /// Marcador de la semilla para claves por completar. Mismo literal que
    /// <c>SiteSettingService.PendingMarker</c>; una prueba vigila que no se separen.
    /// </summary>
    public const string PendingMarker = "PENDIENTE_DEFINIR";

    /// <summary>Lee la clave e interpreta su valor.</summary>
    /// <remarks>
    /// Tan fresca como <see cref="ISettingsReader"/>, que en el host es una caché
    /// singleton invalidada al escribir por el servicio de configuración.
    /// </remarks>
    public static IanaZoneEvaluation Read(ISettingsReader settings)
        => Evaluate(settings.Get(SettingKey));

    /// <summary>Interpreta un valor sin tocar nada más que la base de zonas del runtime.</summary>
    public static IanaZoneEvaluation Evaluate(string? value)
    {
        if (string.IsNullOrEmpty(value) || string.Equals(value, PendingMarker, StringComparison.Ordinal))
        {
            return new(IanaZoneOutcome.Missing, value, null);
        }

        // Forma antes que runtime: sin espacios ni barras invertidas (los
        // identificadores de Windows llevan espacios), con al menos una barra y sin
        // segmentos vacíos ni de ruta.
        if (value.Contains(' ')
            || value.Contains('\\')
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || !value.Contains('/')
            || value.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
        {
            return new(IanaZoneOutcome.Invalid, value, null);
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(value);
            return new(IanaZoneOutcome.RuntimeResolvedCandidate, value, zone.Id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new(IanaZoneOutcome.Invalid, value, null);
        }
    }
}

/// <summary>Qué se puede decir del valor guardado, sin verificar nada.</summary>
public enum IanaZoneOutcome
{
    /// <summary>Sin valor, vacío o con el marcador de la semilla.</summary>
    Missing,

    /// <summary>Hay valor y no es un identificador que este runtime resuelva.</summary>
    Invalid,

    /// <summary>El runtime lo resuelve. Puede ser un alias.</summary>
    RuntimeResolvedCandidate,
}

/// <summary>El valor tal como se guardó y lo que el runtime resolvió, por separado.</summary>
/// <param name="Outcome">Qué se puede decir del valor.</param>
/// <param name="OriginalValue">El valor guardado, sin tocar. Nulo si no había.</param>
/// <param name="RuntimeId">El identificador que devolvió el runtime. Solo con <see cref="IanaZoneOutcome.RuntimeResolvedCandidate"/>.</param>
public sealed record IanaZoneEvaluation(IanaZoneOutcome Outcome, string? OriginalValue, string? RuntimeId);
