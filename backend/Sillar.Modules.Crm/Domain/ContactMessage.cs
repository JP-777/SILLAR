namespace Sillar.Modules.Crm.Domain;

/// <summary>
/// Mensaje del formulario de contacto: captación propia de WEB.
/// </summary>
/// <remarks>
/// No replica (ADR-017): la ficha del cliente es un dato compartido WEB/ERP,
/// pero la captación pertenece exclusivamente al lado WEB. Por eso la PK es
/// <c>integer GENERATED ALWAYS AS IDENTITY</c> y no lleva
/// <c>origin_node</c> ni <c>row_version</c>.
///
/// <see cref="CustomerId"/> es opcional: un visitante puede escribir sin tener
/// ficha. Si se conoce o se vincula después, la FK interna apunta a
/// <see cref="Customer"/>. El mensaje conserva además el nombre y los medios
/// de contacto recibidos en el formulario como datos propios: no depende de
/// que la ficha cambie.
///
/// No lleva estado de atención en esta primera entrega: lectura, asignación
/// o resolución pertenecen al comportamiento de la bandeja y se decidirán
/// cuando exista esa pantalla, no dentro del esquema por anticipado.
/// </remarks>
public class ContactMessage
{
    /// <summary>Identificador del mensaje.</summary>
    public int ContactMessageId { get; set; }

    /// <summary>
    /// Ficha vinculada, o <c>null</c> si el mensaje llegó sin identificación.
    /// </summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Navegación a la ficha vinculada, si existe.</summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// Nombre de quien escribe. Obligatorio. Colación <c>core.es_search</c>:
    /// dato buscable por una persona.
    /// </summary>
    public required string FullName { get; set; }

    /// <summary>
    /// Correo de contacto, o <c>null</c>. Colación <c>core.es_ci</c>:
    /// identidad/contacto. Se normaliza en <c>CrmDbContext</c> con
    /// <c>Trim()</c> + <c>Normalize(NormalizationForm.FormC)</c> si no es null.
    /// Longitud lógica 150.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>Teléfono de contacto, o <c>null</c>.</summary>
    public string? Phone { get; set; }

    /// <summary>Asunto, o <c>null</c>.</summary>
    public string? Subject { get; set; }

    /// <summary>Cuerpo del mensaje. Obligatorio.</summary>
    public required string Message { get; set; }

    /// <summary>Baja lógica.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Momento de creación.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Momento de última modificación (trigger <c>crm.set_updated_at</c>).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
