namespace Sillar.Core.Contracts;

/// <summary>
/// Moneda de la instalación: la autoridad monetaria que CORE ofrece a quien cobra.
/// </summary>
/// <remarks>
/// <para>
/// <b>No es <c>currency_code</c>.</b> Ese ajuste es público y de presentación
/// (sirve para pintar precios) y sigue igual. La autoridad es esta clave privada,
/// <see cref="SettingKey"/>, que nace sin moneda y solo un <c>super_admin</c>
/// establece, con auditoría en la misma transacción.
/// </para>
/// <para>
/// Ninguna moneda se deduce: ni de <c>currency_code = PEN</c>, ni del país, ni de
/// la cultura del servidor.
/// </para>
/// </remarks>
public static class InstallationCurrency
{
    /// <summary>Clave privada en <c>core.site_settings</c>.</summary>
    public const string SettingKey = "installation.currency_iso4217";

    /// <summary>Valor con el que nace la clave: sin moneda acreditada.</summary>
    public const string PendingMarker = "PENDIENTE_DEFINIR";

    /// <summary>Interpreta un valor sin tocar nada.</summary>
    /// <remarks>Comparación exacta: ni recorte de espacios ni cambio de mayúsculas.</remarks>
    public static CurrencyValueCheck Check(string? value)
    {
        if (value is null)
        {
            return new(CurrencyValueStatus.Missing, null);
        }

        if (value.Length == 0 || string.IsNullOrWhiteSpace(value))
        {
            return new(CurrencyValueStatus.Empty, null);
        }

        if (string.Equals(value, PendingMarker, StringComparison.Ordinal))
        {
            return new(CurrencyValueStatus.Pending, null);
        }

        if (value.Length != 3 || !value.All(c => c is >= 'A' and <= 'Z'))
        {
            return new(CurrencyValueStatus.Malformed, null);
        }

        return SupportedCurrencies.Find(value) is { } currency
            ? new(CurrencyValueStatus.Supported, currency)
            : new(CurrencyValueStatus.Unsupported, null);
    }
}

/// <summary>Qué es un valor de moneda.</summary>
public enum CurrencyValueStatus
{
    /// <summary>No hay valor.</summary>
    Missing,

    /// <summary>Vacío o solo espacios.</summary>
    Empty,

    /// <summary>El marcador de clave sin definir.</summary>
    Pending,

    /// <summary>No son tres letras mayúsculas ASCII exactas.</summary>
    Malformed,

    /// <summary>Tiene forma de código ISO 4217, pero no está en <see cref="SupportedCurrencies"/>.</summary>
    Unsupported,

    /// <summary>Está en la lista cerrada.</summary>
    Supported,
}

/// <summary>Resultado de comprobar un valor.</summary>
/// <param name="Status">Qué es.</param>
/// <param name="Currency">La moneda, solo si está soportada.</param>
public sealed record CurrencyValueCheck(CurrencyValueStatus Status, SupportedCurrency? Currency)
{
    /// <summary>Verdadero solo si está en la lista cerrada.</summary>
    public bool IsSupported => Status == CurrencyValueStatus.Supported;
}

/// <summary>Una moneda que el producto soporta de punta a punta.</summary>
/// <param name="Code">Código alfabético ISO 4217.</param>
/// <param name="MinorUnits">Decimales de la moneda. Los importes se guardan con dos (<c>numeric(12,2)</c>).</param>
public sealed record SupportedCurrency(string Code, int MinorUnits);

/// <summary>
/// Lista cerrada y versionada de monedas ISO 4217 que el producto soporta de verdad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué solo PEN.</b> Es la única que el producto maneja de punta a punta:
/// todos los importes se guardan con dos decimales (<c>numeric(12,2)</c>), y el
/// símbolo, los textos y la colación son de Perú (<c>currency_symbol = S/</c>,
/// <c>es-PE</c>). Ninguna otra moneda tiene hoy formato, símbolo ni prueba.
/// </para>
/// <para>
/// <b>Mantenimiento.</b> Añadir o quitar una moneda es una decisión de JP y un
/// cambio de código que sube <see cref="Version"/>. Una prueba fija el contenido
/// exacto, así que la lista no puede cambiar sin que se note. Los datos de cada
/// moneda se contrastan en prueba con los de CLDR del runtime; la publicación
/// oficial de la agencia de mantenimiento no fue alcanzable desde el entorno donde
/// se escribió.
/// </para>
/// </remarks>
public static class SupportedCurrencies
{
    /// <summary>Versión de la lista. Sube con cualquier cambio.</summary>
    public const string Version = "sillar-iso4217-v1";

    /// <summary>Las monedas soportadas.</summary>
    public static IReadOnlyList<SupportedCurrency> All { get; } = [new("PEN", 2)];

    /// <summary>La moneda con ese código exacto, o <c>null</c>.</summary>
    public static SupportedCurrency? Find(string code)
        => All.FirstOrDefault(currency => string.Equals(currency.Code, code, StringComparison.Ordinal));
}

/// <summary>
/// Lector de la autoridad monetaria de la instalación, para quien cobra (M03).
/// </summary>
/// <remarks>
/// <para>
/// Lee la base en cada llamada, no la caché de configuración. Solo hay moneda
/// acreditada si el valor guardado está en la lista cerrada <b>y</b> el último
/// registro de auditoría de esa clave es el de su establecimiento con ese mismo
/// valor. Un valor escrito por fuera de la operación administrativa no tiene ese
/// registro y no se acredita.
/// </para>
/// <para>
/// Si el token del llamador se cancela, se propaga <see cref="OperationCanceledException"/>;
/// cualquier otro fallo de lectura es <see cref="InstallationCurrencyStatus.Unavailable"/>.
/// </para>
/// </remarks>
public interface IInstallationCurrencyAuthority
{
    /// <summary>Lee la moneda acreditada.</summary>
    Task<InstallationCurrencyReading> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Estado de la moneda de la instalación.</summary>
public enum InstallationCurrencyStatus
{
    /// <summary>Acreditada: soportada y con su registro de auditoría.</summary>
    Accredited,

    /// <summary>La clave no está preparada en esta instalación.</summary>
    NotPrepared,

    /// <summary>Sin moneda: el valor está pendiente, vacío o ausente.</summary>
    NotEstablished,

    /// <summary>El valor guardado no es una moneda de la lista.</summary>
    Unsupported,

    /// <summary>Valor soportado, pero sin el registro de auditoría que lo estableció.</summary>
    NotAttested,

    /// <summary>No se pudo leer.</summary>
    Unavailable,
}

/// <summary>Lo que se leyó.</summary>
/// <param name="Status">Estado.</param>
/// <param name="Currency">La moneda, solo si está acreditada.</param>
/// <param name="ListVersion">Versión de la lista con la que se evaluó.</param>
/// <param name="ObservedAt">Instante de la lectura.</param>
public sealed record InstallationCurrencyReading(
    InstallationCurrencyStatus Status,
    SupportedCurrency? Currency,
    string ListVersion,
    DateTimeOffset ObservedAt)
{
    /// <summary>Verdadero solo con moneda acreditada.</summary>
    public bool IsAccredited => Status == InstallationCurrencyStatus.Accredited;
}

/// <summary>
/// Puerto por el que el dueño de los pedidos dice a CORE si ya hay registros
/// monetarios. CORE no lo implementa: no puede leer el schema de otro módulo.
/// </summary>
/// <remarks>
/// Sin ninguna implementación registrada, CORE no puede comprobar que cambiar la
/// moneda sea seguro, y no la cambia.
/// </remarks>
public interface IMonetaryRecordsProbe
{
    /// <summary>Si hay pedidos u otros registros con importe.</summary>
    Task<MonetaryRecordsState> ProbeAsync(CancellationToken cancellationToken);
}

/// <summary>Respuesta del dueño de los pedidos.</summary>
public enum MonetaryRecordsState
{
    /// <summary>No hay ninguno.</summary>
    None,

    /// <summary>Hay al menos uno.</summary>
    Present,

    /// <summary>No lo puede saber.</summary>
    Unknown,
}
