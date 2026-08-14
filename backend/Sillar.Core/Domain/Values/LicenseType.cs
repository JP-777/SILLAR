namespace Sillar.Core.Domain.Values;

/// <summary>Tipos de licencia admitidos en <c>core.installation</c>.</summary>
/// <remarks>
/// Cadenas y no un enum de C#: el valor viaja tal cual a la base de datos, donde
/// un CHECK lo restringe. Una sola lista, en un solo sitio, que además genera el
/// texto del CHECK en la migración.
/// </remarks>
public static class LicenseType
{
    /// <summary>Prueba con vencimiento.</summary>
    public const string Trial = "trial";

    /// <summary>Suscripción con renovación periódica.</summary>
    public const string Subscription = "subscription";

    /// <summary>Licencia perpetua. Sin vencimiento.</summary>
    public const string Perpetual = "perpetual";

    /// <summary>Todos los valores admitidos.</summary>
    public static readonly string[] All = [Trial, Subscription, Perpetual];
}
