namespace Sillar.Core.Dtos;

/// <summary>Una configuración del sitio, vista desde el panel.</summary>
/// <param name="Key">Clave.</param>
/// <param name="Value">Valor actual.</param>
/// <param name="ValueType">Tipo declarado: <c>text</c>, <c>number</c>, <c>boolean</c>, <c>url</c>, <c>email</c> o <c>json</c>.</param>
/// <param name="Description">Para qué sirve.</param>
/// <param name="IsPublic">Si se expone en el endpoint público.</param>
/// <param name="IsActive">Si está en uso.</param>
/// <param name="NeedsSetup">
/// La clave sigue con el marcador del seed y nadie la ha configurado. Es lo que
/// permite al panel mostrar qué le falta a un negocio recién instalado.
/// </param>
/// <param name="UpdatedAt">Última modificación.</param>
public sealed record SettingResponse(
    string Key,
    string Value,
    string ValueType,
    string? Description,
    bool IsPublic,
    bool IsActive,
    bool NeedsSetup,
    DateTimeOffset UpdatedAt);

/// <summary>Cambio de una configuración.</summary>
/// <param name="Value">Valor nuevo. Se valida contra el tipo declarado de la clave.</param>
/// <param name="IsPublic">
/// Si se expone en el endpoint público. Cambiarlo exige <c>super_admin</c>;
/// omitirlo deja el valor actual.
/// </param>
public sealed record UpdateSettingRequest(string? Value, bool? IsPublic);

/// <summary>Moneda que se quiere establecer para la instalación.</summary>
/// <param name="Value">Código ISO 4217 exacto, por ejemplo <c>PEN</c>.</param>
public sealed record SetInstallationCurrencyRequest(string? Value);

/// <summary>La moneda de la instalación tal como la acredita CORE.</summary>
/// <param name="Status">
/// <c>Accredited</c>, <c>NotPrepared</c>, <c>NotEstablished</c>, <c>Unsupported</c>,
/// <c>NotAttested</c> o <c>Unavailable</c>. Solo <c>Accredited</c> es una moneda utilizable.
/// </param>
/// <param name="Code">Código ISO 4217, solo si está acreditada.</param>
/// <param name="MinorUnits">Decimales, solo si está acreditada.</param>
/// <param name="ListVersion">Versión de la lista cerrada con la que se evaluó.</param>
/// <param name="ObservedAt">Instante de la lectura.</param>
public sealed record InstallationCurrencyResponse(
    string Status,
    string? Code,
    int? MinorUnits,
    string ListVersion,
    DateTimeOffset ObservedAt);
