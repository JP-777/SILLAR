import { asInstant, isReportZone, type ReportZone } from './period.ts';

/**
 * M10 D12 — Enlaces reproducibles para filtros de reportes.
 *
 * Puro: no lee cookies ni llama a la red. La interfaz restaura los filtros,
 * pero NUNCA realiza GET automáticamente después de abrir el enlace.
 * No serializa resultados, totales, IDs personales ni credenciales, y la URL
 * no autoriza nada: la ruta sigue exigiendo el rol de administración.
 *
 * Integración sobre D11: las fechas se validan con `asInstant` de `period.ts`
 * —la misma regla que la consulta— en vez de una conversión propia. Así un
 * enlace admite `HH:mm:ss` como el formulario y rechaza los mismos imposibles.
 */
export interface ReportLinkPeriod {
  readonly startLocal: string;
  readonly endLocal: string;
}
export interface ReportLinkFilters {
  readonly zone: ReportZone;
  readonly primary: ReportLinkPeriod;
  readonly comparison: ReportLinkPeriod | null;
}
export type ReportLinkRead =
  | { readonly kind: 'none' }
  | { readonly kind: 'invalid'; readonly message: string }
  | { readonly kind: 'valid'; readonly filters: ReportLinkFilters };

const KEYS = ['rp_v', 'rp_z', 'rp_a_i', 'rp_a_f', 'rp_b_i', 'rp_b_f'] as const;

function instantOrNull(value: string, zone: ReportZone): number | null {
  try {
    return Date.parse(asInstant(value, zone));
  } catch {
    return null;
  }
}

function validPeriod(period: ReportLinkPeriod, zone: ReportZone): boolean {
  const a = instantOrNull(period.startLocal, zone);
  const b = instantOrNull(period.endLocal, zone);
  return a !== null && b !== null && a < b;
}

/** Verifica intervalos [inicio,fin) sin consultar datos. */
export function validReportLink(filters: ReportLinkFilters): boolean {
  if (!isReportZone(filters.zone)) return false;
  return validPeriod(filters.primary, filters.zone)
    && (filters.comparison === null || validPeriod(filters.comparison, filters.zone));
}

/**
 * Devuelve un enlace relativo con exclusivamente parámetros de filtros M10.
 * Los parámetros ajenos se eliminan para no propagar accidentalmente tokens,
 * correos o parámetros de otras pantallas.
 */
export function encodeReportLink(filters: ReportLinkFilters): string | null {
  if (!validReportLink(filters)) return null;
  const params = new URLSearchParams();
  params.set('rp_v', '1');
  params.set('rp_z', filters.zone);
  params.set('rp_a_i', filters.primary.startLocal);
  params.set('rp_a_f', filters.primary.endLocal);
  if (filters.comparison !== null) {
    params.set('rp_b_i', filters.comparison.startLocal);
    params.set('rp_b_f', filters.comparison.endLocal);
  }
  return `/admin/reportes?${params.toString()}`;
}

/**
 * Lee únicamente filtros. Nunca devuelve indicadores y no dispara solicitudes.
 * Un enlace parcial, repetido o con versión desconocida se rechaza íntegro.
 */
export function decodeReportLink(search: string): ReportLinkRead {
  const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);
  const hasReportKeys = KEYS.some((key) => params.has(key));
  if (!hasReportKeys) return { kind: 'none' };
  if (KEYS.some((key) => params.getAll(key).length > 1)) {
    return { kind: 'invalid', message: 'El enlace de reportes contiene filtros repetidos.' };
  }
  if (params.get('rp_v') !== '1') {
    return { kind: 'invalid', message: 'Versión de enlace de reportes no admitida.' };
  }
  const zone = params.get('rp_z');
  if (!isReportZone(zone)) {
    return { kind: 'invalid', message: 'Zona horaria no válida en el enlace.' };
  }
  const primary = { startLocal: params.get('rp_a_i') ?? '', endLocal: params.get('rp_a_f') ?? '' };
  const hasB = params.has('rp_b_i') || params.has('rp_b_f');
  const comparison = hasB
    ? { startLocal: params.get('rp_b_i') ?? '', endLocal: params.get('rp_b_f') ?? '' }
    : null;
  const filters: ReportLinkFilters = { zone, primary, comparison };
  if (!validReportLink(filters)) {
    return { kind: 'invalid', message: 'El enlace de reportes tiene fechas inválidas o un intervalo vacío.' };
  }
  return { kind: 'valid', filters };
}
