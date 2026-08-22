using System.Text.Json.Serialization;

namespace Sillar.Modules.Cms.Domain;

/// <summary>Estado editorial de un contenido con ventana de publicación.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PublicationState>))]
public enum PublicationState
{
    /// <summary>El contenido fue dado de baja en CMS.</summary>
    [JsonStringEnumMemberName("inactive")]
    Inactive,

    /// <summary>El contenido está activo, pero todavía no alcanza su inicio.</summary>
    [JsonStringEnumMemberName("scheduled")]
    Scheduled,

    /// <summary>El contenido está activo y dentro de su ventana.</summary>
    [JsonStringEnumMemberName("current")]
    Current,

    /// <summary>El contenido está activo y alcanzó o superó su final.</summary>
    [JsonStringEnumMemberName("expired")]
    Expired
}
