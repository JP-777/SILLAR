/**
 * Tipos de M07, uno a uno con los DTO del backend (`backend/Sillar.Modules.B2B`).
 * Ningún campo que el backend no devuelva.
 *
 * `customerId` existe en el contrato y se conserva para enlazar, pero **nunca
 * se pinta**: la identidad humana del cliente llega con la costura de CRM
 * (`integration/crm-identidad-cliente`), todavía no integrada.
 */

export type EstadoSolicitud = 'recibida' | 'en_revision' | 'cotizada' | 'cerrada' | 'rechazada';
export type EstadoCotizacion = 'borrador' | 'enviada' | 'aprobada' | 'pagada' | 'anulada';
export type EstadoMayorista = 'configuracion_pendiente' | 'no_evaluable' | 'alcanza' | 'no_alcanza';
export type MetodoDePago = 'yape' | 'efectivo';

export interface PersonalizacionEnBandeja {
  id: number;
  customerId: string;
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
  customerId: string;
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
  customerId: string;
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
