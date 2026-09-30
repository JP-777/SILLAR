namespace Sillar.Modules.B2B.Domain;

/// <summary>
/// Solicitud de personalización: cambia la especificación de algo que ya
/// existe en el catálogo (SPEC §4). La instantánea del producto <b>refresca</b>.
/// </summary>
public sealed class SpecialOrderLead
{
    public int Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public bool PendingRelink { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public DateOnly? NeededBy { get; set; }
    public string Status { get; set; } = RequestStatus.Recibida;
    public string? StaffNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Solicitud de volumen: la cantidad cambia el precio, y puede no existir en
/// el catálogo (SPEC §4). <c>InstitutionName</c> es instantánea, no entidad.
/// </summary>
public sealed class InstitutionRequest
{
    public int Id { get; set; }
    public Guid CustomerId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string? InstitutionDocument { get; set; }
    public string? ContactPerson { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateOnly? EventDate { get; set; }
    public string Status { get; set; } = RequestStatus.Recibida;
    public string? StaffNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Cotización. Nace de exactamente una solicitud (<c>ck_quotes_origen</c>).</summary>
public sealed class Quote
{
    public int Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public int? SpecialOrderLeadId { get; set; }
    public int? InstitutionRequestId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = QuoteStatus.Borrador;
    public DateTimeOffset? InvalidatedAt { get; set; }
    public string? InvalidatedReason { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public string? PaidRegisteredBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<QuoteLine> Lines { get; } = [];
}

/// <summary>
/// Línea de cotización, atada a la <b>presentación</b> (<c>catalog.product_items</c>)
/// o libre. Sus instantáneas <b>congelan</b>: registran lo que se cotizó.
/// </summary>
public sealed class QuoteLine
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Guid? ItemId { get; set; }
    public string? ProductName { get; set; }
    public string? VariantValue { get; set; }
    public string? SaleUnit { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// <c>ItemSnapshot.Price</c> al cotizar. Con <see cref="ItemId"/> presente,
    /// nulo significa «a consultar», nunca «gratis» ni «sin catálogo».
    /// </summary>
    public decimal? CatalogPriceAtQuote { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Estados de una solicitud (SPEC regla 5).</summary>
public static class RequestStatus
{
    public const string Recibida = "recibida";
    public const string EnRevision = "en_revision";
    public const string Cotizada = "cotizada";
    public const string Cerrada = "cerrada";
    public const string Rechazada = "rechazada";
}

/// <summary>Estados de una cotización (SPEC regla 6).</summary>
public static class QuoteStatus
{
    public const string Borrador = "borrador";
    public const string Enviada = "enviada";
    public const string Aprobada = "aprobada";
    public const string Pagada = "pagada";
    public const string Anulada = "anulada";
}

/// <summary>
/// El contador de una serie de números visibles de cotización: una fila por
/// (código de serie, año). ADR-016, excepción del 27/09/2026: se actualiza con
/// <c>UPDATE … RETURNING</c> en la misma transacción que crea la cotización,
/// así que un rollback no consume número. Nunca <c>nextval()</c>.
/// </summary>
public sealed class QuoteNumberSeries
{
    public string SeriesCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public int LastValue { get; set; }
}
