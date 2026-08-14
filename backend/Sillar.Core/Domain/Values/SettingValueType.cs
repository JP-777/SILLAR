namespace Sillar.Core.Domain.Values;

/// <summary>
/// Naturaleza del valor de una configuración. Dice al panel cómo editarlo y al
/// frontend cómo interpretarlo.
/// </summary>
public static class SettingValueType
{
    /// <summary>Texto libre.</summary>
    public const string Text = "text";

    /// <summary>Número.</summary>
    public const string Number = "number";

    /// <summary>Verdadero o falso.</summary>
    public const string Boolean = "boolean";

    /// <summary>Dirección web.</summary>
    public const string Url = "url";

    /// <summary>Correo electrónico.</summary>
    public const string Email = "email";

    /// <summary>Documento JSON.</summary>
    public const string Json = "json";

    /// <summary>Todos los valores admitidos.</summary>
    public static readonly string[] All = [Text, Number, Boolean, Url, Email, Json];
}
