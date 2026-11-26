import type { ServiceOrderStatus } from './contracts';

export function displayDate(value: string | null | undefined): string {
  if (!value) return 'Sin fecha';
  const time = Date.parse(value);
  return Number.isNaN(time) ? 'Fecha no disponible' : new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' }).format(time);
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

/** Solo fechas locales reales: Date por defecto normaliza 31/febrero y horas inexistentes por DST. */
export function toIsoOrNull(local: string): string | null {
  if (!local.trim()) return null;
  const value = local.trim();
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,3}))?)?$/.exec(value);
  if (!match) return null;
  const [, yearText, monthText, dayText, hourText, minuteText, secondText, milliText] = match;
  const year = Number(yearText), month = Number(monthText), day = Number(dayText);
  const hour = Number(hourText), minute = Number(minuteText), second = Number(secondText ?? '0');
  const milliseconds = Number((milliText ?? '0').padEnd(3, '0'));
  if (year < 100 || month < 1 || month > 12 || day < 1 || day > 31 || hour > 23 || minute > 59 || second > 59) return null;
  const candidate = new Date(value);
  if (!Number.isFinite(candidate.getTime())
    || candidate.getFullYear() !== year || candidate.getMonth() + 1 !== month || candidate.getDate() !== day
    || candidate.getHours() !== hour || candidate.getMinutes() !== minute || candidate.getSeconds() !== second
    || candidate.getMilliseconds() !== milliseconds) return null;
  return candidate.toISOString();
}

export function asDateTimeLocal(value: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}
