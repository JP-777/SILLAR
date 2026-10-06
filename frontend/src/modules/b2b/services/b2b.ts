import { http } from '../../../shared/http/client';
import type {
  CotizacionEnBandeja,
  CotizacionPanel,
  EstadoCotizacion,
  EstadoSolicitud,
  LineaRequest,
  MetodoDePago,
  PersonalizacionDetalle,
  PersonalizacionEnBandeja,
  PresentacionParaElegir,
  ProductoParaElegir,
  VolumenDetalle,
  VolumenEnBandeja,
} from './contratos';

/** Toda la conversación de M07 con su backend. Nada de `fetch` suelto. */
const base = '/admin/b2b';

export const personalizaciones = {
  listar: (estado?: EstadoSolicitud) => http.get<PersonalizacionEnBandeja[]>(`${base}/special-orders`, { query: { status: estado } }),
  obtener: (id: number) => http.get<PersonalizacionDetalle>(`${base}/special-orders/${id}`),
  cambiarEstado: (id: number, status: EstadoSolicitud) => http.put<PersonalizacionDetalle>(`${base}/special-orders/${id}/status`, { status }),
  guardarNotas: (id: number, staffNotes: string) => http.put<PersonalizacionDetalle>(`${base}/special-orders/${id}/notes`, { staffNotes }),
  reenlazar: (id: number, productId: string) => http.put<PersonalizacionDetalle>(`${base}/special-orders/${id}/relink`, { productId }),
};

export const volumen = {
  listar: (estado?: EstadoSolicitud) => http.get<VolumenEnBandeja[]>(`${base}/institution-requests`, { query: { status: estado } }),
  obtener: (id: number) => http.get<VolumenDetalle>(`${base}/institution-requests/${id}`),
  cambiarEstado: (id: number, status: EstadoSolicitud) => http.put<VolumenDetalle>(`${base}/institution-requests/${id}/status`, { status }),
  guardarNotas: (id: number, staffNotes: string) => http.put<VolumenDetalle>(`${base}/institution-requests/${id}/notes`, { staffNotes }),
};

export const cotizaciones = {
  listar: (estado?: EstadoCotizacion) => http.get<CotizacionEnBandeja[]>(`${base}/quotes`, { query: { status: estado } }),
  obtener: (id: number) => http.get<CotizacionPanel>(`${base}/quotes/${id}`),
  crear: (origen: 'personalizacion' | 'volumen', solicitudId: number) =>
    http.post<CotizacionPanel>(`${base}/quotes`, { origen, solicitudId, lines: [] }),
  editarLineas: (id: number, lines: LineaRequest[]) => http.put<CotizacionPanel>(`${base}/quotes/${id}`, { lines }),
  enviar: (id: number) => http.put<CotizacionPanel>(`${base}/quotes/${id}/send`),
  aprobar: (id: number) => http.put<CotizacionPanel>(`${base}/quotes/${id}/approve`),
  registrarPago: (id: number, paymentMethod: MetodoDePago, paymentReference: string) =>
    http.put<CotizacionPanel>(`${base}/quotes/${id}/payment`, { paymentMethod, paymentReference: paymentReference.trim() || null }),
  darDeBaja: (id: number) => http.delete<CotizacionPanel>(`${base}/quotes/${id}`),
};

export const catalogo = {
  productos: (q: string) => http.get<ProductoParaElegir[]>(`${base}/catalog/products`, { query: { q } }),
  presentaciones: (q: string) => http.get<PresentacionParaElegir[]>(`${base}/catalog/items`, { query: { q } }),
};
