import { asZonedInstant, wallClock } from '../../shared/zonedDate.ts';

// Zona del negocio ratificada para WEB v1. No depende de la máquina del operador.
export const SERVICE_TIME_ZONE = 'America/Lima';
import type { ServiceOrderStatus } from './contracts';

export function displayDate(value: string | null | undefined): string {
  if (!value) return 'Sin fecha';
  const time = Date.parse(value);
  return Number.isNaN(time) ? 'Fecha no disponible' : new Intl.DateTimeFormat('es-PE', { timeZone: SERVICE_TIME_ZONE, dateStyle: 'medium', timeStyle: 'short' }).format(time);
}

/** No asignar una moneda por inferencia: M05b v1 no aporta un código monetario en el DTO. */
export function displayAmount(value: number | null): string {
  return value === null ? 'Precio pendiente' : new Intl.NumberFormat('es-PE', { maximumFractionDigits: 2, minimumFractionDigits: 2 }).format(value);
}

export function statusTone(status: ServiceOrderStatus): 'success' | 'warning' | 'neutral' {
  if (status === 'completed') return 'success';
  if (status === 'cancelled') return 'neutral';
  return 'warning';
}

/** El vacío retira la fecha; una fecha imposible se devuelve nula para su validación. */
export function toIsoOrNull(local: string): string | null {
  if (!local.trim()) return null;
  try { return asZonedInstant(local.trim(), SERVICE_TIME_ZONE); }
  catch { return null; }
}

export function asDateTimeLocal(value: string | null): string {
  if (!value || !Number.isFinite(Date.parse(value))) return '';
  return wallClock(Date.parse(value), SERVICE_TIME_ZONE).slice(0, 16);
}
