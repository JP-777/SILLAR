import type { EstadoSolicitud } from '../services/contratos';

/**
 * Estados de una solicitud en el panel (A3). La tabla es la misma que aplica el
 * backend (`TransicionesDeSolicitud`, SPEC regla 5); el backend sigue mandando
 * y su 409 se muestra tal cual. **`cerrada` y `rechazada` son finales**: no se
 * ofrece reabrirlas.
 */
const SIGUIENTES: Record<EstadoSolicitud, readonly EstadoSolicitud[]> = {
  recibida: ['en_revision', 'rechazada'],
  en_revision: ['cotizada', 'rechazada'],
  cotizada: ['cerrada', 'rechazada'],
  cerrada: [],
  rechazada: [],
};

export const NOMBRE_DE_ESTADO: Record<EstadoSolicitud, string> = {
  recibida: 'Recibida',
  en_revision: 'En revisión',
  cotizada: 'Cotizada',
  cerrada: 'Cerrada',
  rechazada: 'Rechazada',
};

export function estadosSiguientes(estado: EstadoSolicitud): readonly EstadoSolicitud[] {
  return SIGUIENTES[estado];
}

export function esFinal(estado: EstadoSolicitud): boolean {
  return SIGUIENTES[estado].length === 0;
}
