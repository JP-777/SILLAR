namespace Sillar.Core.Contracts;

/// <summary>
/// Zona horaria IANA de la instalación. Fase A, candidato: clave privada, sin
/// semilla y sin claim público.
/// </summary>
/// <remarks>
/// <para>
/// La autoridad es la que lee el servidor; nunca un campo que mande el cliente.
/// No hay zona por defecto: sin valor, la zona está ausente, y quien la necesite
/// tiene que decirlo en vez de suponer una.
/// </para>
/// <para>
/// <b>Resolver no es canonizar.</b> Que el runtime encuentre la zona solo dice
/// que existe en la base de zonas de esta máquina. Un alias (<c>Pacific/Samoa</c>)
/// se resuelve igual que un identificador canónico, y en Windows la resolución
/// pasa por ICU con reglas propias. Por eso comparar con <see cref="TimeZoneInfo.Id"/>
/// no prueba la canonización, y <see cref="IanaZoneOutcome.IanaCanonicalVerified"/>
/// no lo devuelve ningún camino hasta que exista una regla ratificada (por
/// ejemplo, una lista canónica fijada con su versión de tzdata).
/// </para>
/// </remarks>
public static class IanaZoneCandidate
{
    /// <summary>Clave candidata en <c>core.site_settings</c>. Única: no hay otra en paralelo.</summary>
    public const string SettingKey = "installation.timezone_iana";

    /// <summary>
    /// Marcador de la semilla para claves por completar. Mismo literal que
    /// <c>SiteSettingService.PendingMarker</c>; una prueba vigila que no se separen.
    /// </summary>
    public const string PendingMarker = "PENDIENTE_DEFINIR";

    /// <summary>Lee la clave e interpreta su valor.</summary>
    /// <remarks>
    /// <b>Frescura.</b> En el host, <see cref="ISettingsReader"/> es una caché
    /// singleton que se invalida al escribir por el servicio de configuración. Esta
    /// lectura es tan fresca como esa caché, y no más: un cambio hecho por fuera
    /// del servicio no se ve hasta que la caché se recargue.
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

        // Filtro de forma antes de preguntar al runtime: sin espacios ni barras
        // invertidas (los identificadores de Windows llevan espacios), con al menos
        // una barra y sin segmentos vacíos ni de ruta.
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

/// <summary>Qué se puede decir del valor guardado.</summary>
public enum IanaZoneOutcome
{
    /// <summary>Sin valor, vacío o con el marcador de la semilla.</summary>
    Missing,

    /// <summary>Hay valor y no es un identificador IANA que este runtime resuelva.</summary>
    Invalid,

    /// <summary>El runtime lo resuelve. Pendiente de canonización: puede ser un alias.</summary>
    RuntimeResolvedCandidate,

    /// <summary>Reservado a una regla de canonización ratificada. Ningún camino de Fase A lo devuelve.</summary>
    IanaCanonicalVerified,
}

/// <summary>El valor tal como se guardó y lo que el runtime resolvió, por separado.</summary>
/// <param name="Outcome">Qué se puede decir del valor.</param>
/// <param name="OriginalValue">El valor guardado, sin tocar. Nulo si no había.</param>
/// <param name="RuntimeId">El identificador que devolvió el runtime. Solo con <see cref="IanaZoneOutcome.RuntimeResolvedCandidate"/>.</param>
public sealed record IanaZoneEvaluation(IanaZoneOutcome Outcome, string? OriginalValue, string? RuntimeId);
