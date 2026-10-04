namespace Sillar.Modules.B2B.Bandeja;

/// <summary>
/// Quién es el cliente de una fila de la bandeja: lo mínimo que una persona
/// necesita para reconocerlo.
/// </summary>
/// <remarks>
/// <para>
/// <b>No lleva el <c>customer_id</c>, y eso es la mitad del arreglo.</b> La bandeja
/// guardaba el uuid y lo sacaba en el DTO «para enlazar», pero no enlazaba a ningún
/// sitio: el frontend lo declaraba y no lo pintaba nunca
/// (<c>frontend/src/modules/b2b/services/contratos.ts</c>, versión anterior). Un
/// identificador que viaja sin uso es un identificador que algún día se pinta.
/// </para>
/// <para>
/// <b>Puede faltar, y entonces falta.</b> <c>ICustomerIdentityReader</c> devuelve solo
/// fichas activas: una de baja o bloqueada no aparece, sin decir por qué. En ese caso
/// la fila llega con el cliente en <c>null</c> y <b>no se rellena con nada</b> —ni
/// «Cliente dado de baja», ni el correo, ni el uuid—. Quien pinta decide qué decir;
/// este módulo no inventa una identidad que no tiene.
/// </para>
/// <para>
/// Es el dato vigente en cada lectura, no una instantánea: una cotización no congela
/// a su cliente. Lo que se congela es el pago (R-14), y eso es otra cosa.
/// </para>
/// </remarks>
public sealed record ClienteDeLaBandeja(string FullName, string Email, string? Phone);

/// <summary>Una solicitud de personalización en la bandeja del panel. Sin notas internas.</summary>
public sealed record PersonalizacionEnBandeja(
    int Id, ClienteDeLaBandeja? Cliente, Guid? ProductId, string ProductName, string ProductSlug, bool PendingRelink,
    string Description, int? Quantity, DateOnly? NeededBy, string Status, bool IsActive, DateTimeOffset CreatedAt);

/// <summary>El detalle de una personalización, <b>con</b> sus notas internas: solo para el panel.</summary>
public sealed record PersonalizacionDetalle(PersonalizacionEnBandeja Solicitud, string? StaffNotes);

/// <summary>Una solicitud de volumen en la bandeja del panel. Sin notas internas.</summary>
public sealed record VolumenEnBandeja(
    int Id, ClienteDeLaBandeja? Cliente, string InstitutionName, string? InstitutionDocument, string? ContactPerson,
    string Description, int Quantity, DateOnly? EventDate, string Status, bool IsActive, DateTimeOffset CreatedAt);

/// <summary>El detalle de una solicitud de volumen, <b>con</b> sus notas internas.</summary>
public sealed record VolumenDetalle(VolumenEnBandeja Solicitud, string? StaffNotes);

/// <summary>Cambiar el estado de una solicitud.</summary>
/// <param name="Status">Uno de: recibida, en_revision, cotizada, cerrada, rechazada.</param>
public sealed record CambiarEstadoRequest(string? Status);

/// <summary>Reescribir las notas internas. Vacío las borra.</summary>
public sealed record NotasRequest(string? StaffNotes);

/// <summary>Volver a atar una personalización a un producto activo de M01.</summary>
public sealed record ReenlazarRequest(Guid ProductId);

/// <summary>Una línea de cotización vista desde el panel, con su precio de catálogo.</summary>
public sealed record LineaDeCotizacionAdmin(
    Guid? ItemId, string? ProductName, string? VariantValue, string? SaleUnit, string Description,
    int Quantity, decimal UnitPrice, decimal? CatalogPriceAtQuote, int SortOrder);

/// <summary>Una cotización en la bandeja del panel.</summary>
public sealed record CotizacionEnBandeja(
    int Id, string QuoteNumber, ClienteDeLaBandeja? Cliente, int? SpecialOrderLeadId, int? InstitutionRequestId,
    decimal TotalAmount, string Status, DateTimeOffset? InvalidatedAt, string? InvalidatedReason, bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>El detalle de una cotización con sus líneas.</summary>
public sealed record CotizacionDetalle(CotizacionEnBandeja Cotizacion, IReadOnlyList<LineaDeCotizacionAdmin> Lines,
    DateTimeOffset? ApprovedAt, DateTimeOffset? PaidAt, string? PaymentMethod, string? PaymentReference, string? PaidRegisteredBy);

/// <summary>Un producto de M01 que se puede elegir para reenlazar. Sin URL ni imagen: no hacen falta.</summary>
public sealed record ProductoParaElegir(Guid ProductId, string Name, bool IsPublic);

/// <summary>
/// Una presentación de M01 para una línea de cotización. <c>Price</c> nulo = «a
/// consultar», nunca «gratis» (<c>ItemSnapshot.Price</c>).
/// </summary>
public sealed record PresentacionParaElegir(Guid ItemId, string ProductName, string? VariantValue, string? SaleUnit, decimal? Price);
