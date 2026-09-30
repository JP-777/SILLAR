namespace Sillar.Modules.B2B.Solicitudes;

/// <summary>Pedir un cambio sobre un producto que ya existe (SPEC §4, personalización).</summary>
/// <param name="ProductId">El producto de M01 del que parte. Tiene que estar activo y publicado.</param>
/// <param name="Description">Qué se quiere exactamente, por diferencia con el producto.</param>
/// <param name="Quantity">Cuántos, si importa. Si viene, mayor que cero.</param>
/// <param name="NeededBy">Para cuándo. Informativa: no calcula ni bloquea nada.</param>
public sealed record CrearPersonalizacionRequest(Guid ProductId, string? Description, int? Quantity, DateOnly? NeededBy);

/// <summary>Pedir una cantidad que cambia el precio (SPEC §4, volumen).</summary>
/// <param name="InstitutionName">Para quién es: un colegio, una empresa, un evento.</param>
/// <param name="InstitutionDocument">RUC, si lo hay.</param>
/// <param name="ContactPerson">Con quién hablar, si no es quien pide.</param>
/// <param name="Description">Qué se pide.</param>
/// <param name="Quantity">Cuántos. Mayor que cero.</param>
/// <param name="EventDate">Para cuándo. Informativa.</param>
public sealed record CrearVolumenRequest(
    string? InstitutionName, string? InstitutionDocument, string? ContactPerson,
    string? Description, int Quantity, DateOnly? EventDate);

/// <summary>Una solicitud propia, de cualquiera de los dos tipos. Sin notas internas.</summary>
/// <param name="Kind"><c>personalizacion</c> o <c>volumen</c>.</param>
/// <param name="RequestId">Identificador interno, para enlazar; no se muestra.</param>
/// <param name="Description">Lo que se pidió.</param>
/// <param name="Status">Estado de la bandeja.</param>
/// <param name="CreatedAt">Cuándo se pidió.</param>
public sealed record SolicitudPropiaResponse(
    string Kind, int RequestId, string Description, string Status, DateTimeOffset CreatedAt);

/// <summary>Una línea de cotización, tal como la ve el cliente.</summary>
public sealed record LineaDeCotizacionResponse(string Description, int Quantity, decimal UnitPrice);

/// <summary>
/// Una cotización propia. <b>Sin</b> <c>catalog_price_at_quote</c>: enseñar el
/// precio de lista al lado del cobrado sería enseñar el descuento como un
/// error (SPEC §5).
/// </summary>
public sealed record CotizacionPropiaResponse(
    string QuoteNumber, decimal TotalAmount, string Status, bool SigueValida,
    DateTimeOffset? ApprovedAt, DateTimeOffset? PaidAt, IReadOnlyList<LineaDeCotizacionResponse> Lines);
