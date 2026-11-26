import { asZonedInstant, wallClock } from '../../../shared/zonedDate.ts';

// El tablero usa la zona del negocio, igual que las órdenes de servicio.
const TRACKING_TIME_ZONE = 'America/Lima';
import type { TrackingBoardColumn, TrackingBoardCard } from '../services/contracts';

/** Mueve un identificador dentro de una lista sin duplicarlo ni perder pares. */
export function moveId(ids: readonly string[], moving: string, before: string | null): string[] {
  if (!ids.includes(moving)) return [...ids];
  const remaining = ids.filter((id) => id !== moving);
  if (before === null) return [...remaining, moving];
  const index = remaining.indexOf(before);
  if (index < 0) return [...ids];
  return [...remaining.slice(0, index), moving, ...remaining.slice(index)];
}

/** Solo los pares de la misma banda de fijación participan en un reordenamiento. */
export function peerIds(column: TrackingBoardColumn, card: TrackingBoardCard): string[] {
  return column.cards.filter((candidate) => candidate.pinned === card.pinned).map((candidate) => candidate.serviceOrderId);
}

export function cardCount(columns: readonly TrackingBoardColumn[]): number {
  return columns.reduce((total, column) => total + column.cards.length, 0);
}

export function statusName(columns: readonly TrackingBoardColumn[], status: string | null): string {
  if (!status) return 'Inicio';
  return columns.find((column) => column.status === status)?.displayName ?? status;
}

export function formatDateTime(value: string | null): string {
  if (!value) return 'Sin fecha';
  if (!Number.isFinite(Date.parse(value))) return 'Fecha no disponible';
  return new Intl.DateTimeFormat('es-PE', {
    timeZone: TRACKING_TIME_ZONE,
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function toDateTimeLocal(value: string | null): string {
  if (!value || !Number.isFinite(Date.parse(value))) return '';
  return wallClock(Date.parse(value), TRACKING_TIME_ZONE).slice(0, 16);
}

export function fromDateTimeLocal(value: string): string | null {
  return value.trim() ? asZonedInstant(value.trim(), TRACKING_TIME_ZONE) : null;
}

export function shiftId(ids: readonly string[], moving: string, delta: -1 | 1): string[] {
  const index = ids.indexOf(moving);
  const next = index + delta;
  if (index < 0 || next < 0 || next >= ids.length) return [...ids];
  const result = [...ids];
  [result[index], result[next]] = [result[next], result[index]];
  return result;
}
