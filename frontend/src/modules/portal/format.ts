import type { BadgeTone } from '../../shared/ui';

const moneyFormatter = new Intl.NumberFormat('es-PE', {
  style: 'currency',
  currency: 'PEN',
});

const dateFormatter = new Intl.DateTimeFormat('es-PE', {
  dateStyle: 'medium',
  timeZone: 'America/Lima',
});

const dateTimeFormatter = new Intl.DateTimeFormat('es-PE', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'America/Lima',
});

const quantityFormatter = new Intl.NumberFormat('es-PE', {
  maximumFractionDigits: 3,
});

const ORDER_STATUSES: Record<string, { label: string; tone: BadgeTone }> = {
  pending_payment: { label: 'Pendiente de pago', tone: 'warning' },
  payment_to_verify: { label: 'Pago por verificar', tone: 'warning' },
  preparing: { label: 'En preparación', tone: 'neutral' },
  ready_for_pickup: { label: 'Listo para recoger', tone: 'success' },
  delivered: { label: 'Entregado', tone: 'success' },
  expired: { label: 'Plazo de pago vencido', tone: 'danger' },
  cancelled: { label: 'Cancelado', tone: 'danger' },
};

const WORK_STATUSES: Record<string, { label: string; tone: BadgeTone }> = {
  received: { label: 'Recibido', tone: 'neutral' },
  in_progress: { label: 'En proceso', tone: 'warning' },
  ready: { label: 'Listo', tone: 'success' },
  completed: { label: 'Completado', tone: 'success' },
  cancelled: { label: 'Cancelado', tone: 'danger' },
};

const UNKNOWN_STATUS = { label: 'Estado no disponible', tone: 'neutral' } as const;

export function formatMoney(value: number): string {
  return moneyFormatter.format(value);
}

export function formatDate(value: string): string {
  return dateFormatter.format(new Date(value));
}

export function formatDateTime(value: string): string {
  return dateTimeFormatter.format(new Date(value));
}

export function formatQuantity(value: number): string {
  return quantityFormatter.format(value);
}

export function orderStatus(status: string): { label: string; tone: BadgeTone } {
  return ORDER_STATUSES[status] ?? UNKNOWN_STATUS;
}

export function workStatus(status: string): { label: string; tone: BadgeTone } {
  return WORK_STATUSES[status] ?? UNKNOWN_STATUS;
}
