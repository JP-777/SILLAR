namespace Sillar.Core.Domain;

/// <summary>Configuración general del sitio, en pares clave-valor.</summary>
public class SiteSetting
{
    /// <summary>Identificador.</summary>
    public int SiteSettingId { get; set; }

    /// <summary>
    /// Clave. Se compara sin distinguir mayúsculas mediante la colación
    /// <c>core.es_ci</c>: <c>whatsapp_number</c> y <c>WhatsApp_Number</c> son la
    /// misma configuración y no pueden coexistir.
    /// </summary>
    public required string SettingKey { get; set; }

    /// <summary>Valor, siempre como texto.</summary>
    public required string SettingValue { get; set; }

    /// <summary>Naturaleza del valor. Ver <see cref="Values.SettingValueType"/>.</summary>
    public required string ValueType { get; set; }

    /// <summary>Para qué sirve esta configuración.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Si el valor se expone en el endpoint público.
    /// </summary>
    /// <remarks>
    /// Falso por defecto, y ese defecto es el punto: el número de WhatsApp se
    /// publica, una clave de correo saliente jamás. Publicar tiene que ser un
    /// acto deliberado, nunca un descuido.
    /// </remarks>
    public bool IsPublic { get; set; }

    /// <summary>Eliminación lógica.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
