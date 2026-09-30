using Sillar.Modules.B2B.Bandeja;

namespace Sillar.Modules.B2B.Cotizaciones;

/// <summary>Una línea al crear o editar una cotización en borrador.</summary>
/// <param name="ItemId">La presentación de M01, o nulo para una línea libre («100 cordones»).</param>
/// <param name="Description">Cómo se lee en el documento. Si viene vacía y hay presentación, se usa su nombre.</param>
/// <param name="Quantity">Cuántos. Mayor que cero.</param>
/// <param name="UnitPrice">Lo que se cobra por unidad, con el descuento que decida el personal.</param>
public sealed record LineaRequest(Guid? ItemId, string? Description, int Quantity, decimal UnitPrice);

/// <summary>Crear una cotización desde una solicitud.</summary>
/// <param name="Origen"><c>personalizacion</c> o <c>volumen</c>.</param>
/// <param name="SolicitudId">La solicitud de la que nace.</param>
/// <param name="Lines">Líneas iniciales; puede ir vacía y rellenarse en borrador.</param>
public sealed record CrearCotizacionRequest(string? Origen, int SolicitudId, IReadOnlyList<LineaRequest>? Lines);

/// <summary>Sustituir las líneas de una cotización en borrador.</summary>
public sealed record EditarLineasRequest(IReadOnlyList<LineaRequest>? Lines);

/// <summary>Registrar el pago de una cotización aprobada.</summary>
/// <param name="PaymentMethod"><c>yape</c> o <c>efectivo</c>. <c>tarjeta</c> llega con M11.</param>
/// <param name="PaymentReference">Código de operación. Obligatorio con Yape.</param>
public sealed record PagoRequest(string? PaymentMethod, string? PaymentReference);

/// <summary>El detalle de una cotización para el panel, con la evaluación mayorista.</summary>
public sealed record CotizacionPanel(CotizacionDetalle Detalle, EvaluacionMayorista Mayorista);
