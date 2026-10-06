/**
 * Tipos de M07, uno a uno con los DTO del backend (`backend/Sillar.Modules.B2B`).
 * Ningún campo que el backend no devuelva.
 *
 * La identidad del cliente llega resuelta: el backend cambia el `customer_id`
 * por una persona con `ICustomerIdentityReader` (M04 1.2.0) y **el uuid ya no
 * viaja**. Antes se declaraba aquí «para enlazar» y no se pintaba nunca.
 *
 * `cliente` puede ser `null`: el contrato de M04 solo devuelve fichas activas y
 * no dice por qué falta una. Nada lo rellena.
 */

export type EstadoSolicitud = 'recibida' | 'en_revision' | 'cotizada' | 'cerrada' | 'rechazada';
export type EstadoCotizacion = 'borrador' | 'enviada' | 'aprobada' | 'pagada' | 'anulada';
export type EstadoMayorista = 'configuracion_pendiente' | 'no_evaluable' | 'alcanza' | 'no_alcanza';
export type MetodoDePago = 'yape' | 'efectivo';

/** Identidad mínima y legible de un cliente. Sin identificador: no se enseña. */
export interface ClienteDeLaBandeja {
  fullName: string;
  email: string;
  phone: string | null;
}

export interface PersonalizacionEnBandeja {
  id: number;
  cliente: ClienteDeLaBandeja | null;
  productId: string | null;
  productName: string;
  productSlug: string;
  pendingRelink: boolean;
  description: string;
  quantity: number | null;
  neededBy: string | null;
  status: EstadoSolicitud;
  isActive: boolean;
  createdAt: string;
}

export interface PersonalizacionDetalle {
  solicitud: PersonalizacionEnBandeja;
  /** Solo panel. Nunca sale al cliente. */
  staffNotes: string | null;
}

export interface VolumenEnBandeja {
  id: number;
  cliente: ClienteDeLaBandeja | null;
  institutionName: string;
  institutionDocument: string | null;
  contactPerson: string | null;
  description: string;
  quantity: number;
  eventDate: string | null;
  status: EstadoSolicitud;
  isActive: boolean;
  createdAt: string;
}

export interface VolumenDetalle {
  solicitud: VolumenEnBandeja;
  staffNotes: string | null;
}

export interface CotizacionEnBandeja {
  id: number;
  /** El único identificador que se enseña: `C-AAAA-NNNN`. */
  quoteNumber: string;
  cliente: ClienteDeLaBandeja | null;
  specialOrderLeadId: number | null;
  institutionRequestId: number | null;
  totalAmount: number;
  status: EstadoCotizacion;
  /** No cambia el estado: una `enviada` invalidada sigue siendo `enviada`. */
  invalidatedAt: string | null;
  invalidatedReason: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface LineaDeCotizacionAdmin {
  itemId: string | null;
  productName: string | null;
  variantValue: string | null;
  saleUnit: string | null;
  description: string;
  quantity: number;
  unitPrice: number;
  /** Con `itemId`: nulo = «a consultar». Sin `itemId`: línea libre. */
  catalogPriceAtQuote: number | null;
  sortOrder: number;
}

export interface CotizacionDetalle {
  cotizacion: CotizacionEnBandeja;
  lines: LineaDeCotizacionAdmin[];
  approvedAt: string | null;
  paidAt: string | null;
  paymentMethod: string | null;
  paymentReference: string | null;
  /** Hoy guarda el correo de quien registró (C13); no se presenta como nombre. */
  paidRegisteredBy: string | null;
}

export interface EvaluacionMayorista {
  estado: EstadoMayorista;
  umbral: number | null;
  importeDeLista: number | null;
  motivo: string;
}

export interface CotizacionPanel {
  detalle: CotizacionDetalle;
  mayorista: EvaluacionMayorista;
}

export interface LineaRequest {
  itemId: string | null;
  description: string | null;
  quantity: number;
  unitPrice: number;
}

export interface ProductoParaElegir {
  productId: string;
  name: string;
  isPublic: boolean;
}

export interface PresentacionParaElegir {
  itemId: string;
  productName: string;
  variantValue: string | null;
  saleUnit: string | null;
  /** Nulo = «a consultar», nunca gratis. */
  price: number | null;
}
