/**
 * D19 · Selección explícita del período inmediatamente anterior, de IGUAL duración
 * transcurrida. No consulta datos, no modifica contratos y nunca interpreta cambios
 * entre fotografías independientes como transiciones históricas.
 *
 * Los campos datetime-local de M10 tienen precisión de minuto; si la fuente tiene
 * segundos/subsegundos, no truncamos silenciosamente y se omite la sugerencia.
 */
import { asInstant, isReportZone, wallClock, type ReportZone } from './period.ts';
export type PreviousEqualWindow =
  | { readonly kind: 'ready'; readonly startLocal: string; readonly endLocal: string;
      readonly durationMinutes: number; readonly previousStartUtc: string; readonly previousEndUtc: string }
  | { readonly kind: 'unavailable'; readonly reason: string };

const ISO = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?(Z|([+-])(\d{2}):(\d{2}))$/;
const leap = (year: number) => year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
const lengths = (year: number) => [31, leap(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

function readMinutePrecisionInstant(value: string): number | null {
  const match = ISO.exec(value);
  if (match === null) return null;
  const [year, month, day, hour, minute, second] = match.slice(1, 7).map(Number);
  if (year < 1 || month < 1 || month > 12 || day < 1 || day > lengths(year)[month - 1] ||
      hour > 23 || minute > 59 || second !== 0 ||
      /[1-9]/.test(match[7] ?? '')) return null;
  let offsetMinutes = 0;
  if (match[8] !== 'Z') {
    const oh = Number(match[10]);
    const om = Number(match[11]);
    if (oh > 14 || om > 59 || (oh === 14 && om !== 0)) return null;
    offsetMinutes = (match[9] === '+' ? 1 : -1) * (oh * 60 + om);
  }
  const d = new Date(0);
  d.setUTCFullYear(year, month - 1, day);
  d.setUTCHours(hour, minute, 0, 0);
  const epochMs = d.getTime() - offsetMinutes * 60_000;
  return Number.isFinite(epochMs) ? epochMs : null;
}

/**
 * El instante como lo escribiría el formulario en la zona, o null si no se puede
 * escribir exactamente: segundos sueltos, fuera de 1..9999, o una hora que la
 * zona salta o repite. La ida y vuelta usa la misma regla que la consulta.
 */
function toLocalExact(ms: number, zone: ReportZone): string | null {
  if (!Number.isFinite(ms) || ms % 60_000 !== 0) return null;
  const wall = wallClock(ms, zone);
  if (!wall.endsWith(':00') || wall.startsWith('0000')) return null;
  const display = wall.slice(0, 16);
  try {
    return Date.parse(asInstant(display, zone)) === ms ? display : null;
  } catch {
    return null;
  }
}

/** Previous window [A.start - (A.end-A.start), A.start), with the same elapsed minutes. */
export function previousEqualWindow(startInclusive: string, endExclusive: string, zone: ReportZone): PreviousEqualWindow {
  if (!isReportZone(zone)) {
    return { kind: 'unavailable', reason: 'Zona horaria no compatible.' };
  }
  const start = readMinutePrecisionInstant(startInclusive);
  const end = readMinutePrecisionInstant(endExclusive);
  if (start === null || end === null || end <= start) {
    return { kind: 'unavailable', reason: 'La cohorte principal no tiene límites ISO válidos con precisión de minuto.' };
  }
  const span = end - start;
  const preceding = start - span;
  if (!Number.isSafeInteger(span) || span <= 0 || !Number.isSafeInteger(preceding)) {
    return { kind: 'unavailable', reason: 'La duración de la cohorte no es representable exactamente.' };
  }
  const startLocal = toLocalExact(preceding, zone);
  const endLocal = toLocalExact(start, zone);
  if (startLocal === null || endLocal === null) {
    return { kind: 'unavailable', reason: 'No puede expresarse el período anterior exactamente en la zona seleccionada.' };
  }
  const durationMinutes = span / 60_000;
  if (!Number.isSafeInteger(durationMinutes)) {
    return { kind: 'unavailable', reason: 'La duración no coincide exactamente con minutos completos.' };
  }
  return { kind: 'ready', startLocal, endLocal, durationMinutes,
    previousStartUtc: new Date(preceding).toISOString(), previousEndUtc: new Date(start).toISOString() };
}
