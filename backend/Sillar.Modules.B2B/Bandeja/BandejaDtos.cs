namespace Sillar.Modules.B2B.Bandeja;

/// <summary>Una solicitud de personalización en la bandeja del panel. Sin notas internas.</summary>
public sealed record PersonalizacionEnBandeja(
    int Id, Guid CustomerId, Guid? ProductId, string ProductName, string ProductSlug, bool PendingRelink,
    string Description, int? Quantity, DateOnly? NeededBy, string Status, bool IsActive, DateTimeOffset CreatedAt);

/// <summary>El detalle de una personalización, <b>con</b> sus notas internas: solo para el panel.</summary>
public sealed record PersonalizacionDetalle(PersonalizacionEnBandeja Solicitud, string? StaffNotes);

/// <summary>Una solicitud de volumen en la bandeja del panel. Sin notas internas.</summary>
public sealed record VolumenEnBandeja(
    int Id, Guid CustomerId, string InstitutionName, string? InstitutionDocument, string? ContactPerson,
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
    int Id, string QuoteNumber, Guid CustomerId, int? SpecialOrderLeadId, int? InstitutionRequestId,
    decimal TotalAmount, string Status, DateTimeOffset? InvalidatedAt, string? InvalidatedReason, bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>El detalle de una cotización con sus líneas.</summary>
public sealed record CotizacionDetalle(CotizacionEnBandeja Cotizacion, IReadOnlyList<LineaDeCotizacionAdmin> Lines,
    DateTimeOffset? ApprovedAt, DateTimeOffset? PaidAt, string? PaymentMethod, string? PaymentReference, string? PaidRegisteredBy);
