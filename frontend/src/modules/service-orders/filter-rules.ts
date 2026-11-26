import type { ServiceOrderFilters, ServiceOrderStatus } from './contracts';
import { SERVICE_ORDER_STATES } from './rules';

export function readFilters(search: string): ServiceOrderFilters {
  const values = new URLSearchParams(search);
  const state = values.get('status') || '';
  const page = Number(values.get('page') ?? 1);
  const assignee = Number(values.get('assigneeAdminUserId') ?? '');
  return {
    q: (values.get('q') || '').slice(0, 200) || undefined,
    status: SERVICE_ORDER_STATES.includes(state as ServiceOrderStatus) ? state as ServiceOrderStatus : undefined,
    assigneeAdminUserId: Number.isSafeInteger(assignee) && assignee > 0 ? assignee : undefined,
    receivedFrom: validInstant(values.get('receivedFrom')),
    receivedTo: validInstant(values.get('receivedTo')),
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
    pageSize: 20,
  };
}

/** Mantener filtros en URL permite regresar desde el detalle sin perder la búsqueda. */
export function encodeFilters(filters: ServiceOrderFilters): string {
  const q = new URLSearchParams();
  if (filters.q?.trim()) q.set('q', filters.q.trim().slice(0, 200));
  if (filters.status) q.set('status', filters.status);
  if (filters.assigneeAdminUserId && filters.assigneeAdminUserId > 0) q.set('assigneeAdminUserId', String(filters.assigneeAdminUserId));
  if (filters.receivedFrom) q.set('receivedFrom', filters.receivedFrom);
  if (filters.receivedTo) q.set('receivedTo', filters.receivedTo);
  if (filters.page && filters.page > 1) q.set('page', String(filters.page));
  return q.toString();
}

/** Solo instantes ISO con zona explícita, sin normalizar fechas imposibles de una URL manipulada. */
function validInstant(value: string | null): string | undefined {
  if (!value) return undefined;
  const parsed = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,3}))?)?(Z|([+-])(\d{2}):(\d{2}))$/.exec(value);
  if (!parsed) return undefined;
  const [, ys, ms, ds, hs, mins, ss, millis, zone, sign, offsetHours, offsetMinutes] = parsed;
  const y = Number(ys), mo = Number(ms), d = Number(ds), h = Number(hs), min = Number(mins);
  const sec = Number(ss ?? 0), milli = Number((millis ?? '0').padEnd(3, '0'));
  const oh = Number(offsetHours ?? 0), om = Number(offsetMinutes ?? 0);
  if (y < 100 || mo < 1 || mo > 12 || d < 1 || d > 31 || h > 23 || min > 59 || sec > 59 || oh > 14 || om > 59 || (oh === 14 && om !== 0)) return undefined;
  const utcMs = Date.parse(value);
  if (!Number.isFinite(utcMs)) return undefined;
  const delta = zone === 'Z' ? 0 : (sign === '-' ? -1 : 1) * (oh * 60 + om) * 60_000;
  const local = new Date(utcMs + delta);
  if (local.getUTCFullYear() !== y || local.getUTCMonth() + 1 !== mo || local.getUTCDate() !== d
    || local.getUTCHours() !== h || local.getUTCMinutes() !== min || local.getUTCSeconds() !== sec
    || local.getUTCMilliseconds() !== milli) return undefined;
  return new Date(utcMs).toISOString();
}
