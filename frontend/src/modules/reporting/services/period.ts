/**
 * Fechas y recuentos de los reportes, en un solo sitio.
 *
 * D10: la consulta principal y la comparación de cohortes convierten el período
 * con la misma regla; antes cada una tenía la suya y la de la comparación volvía
 * a rechazar `HH:mm:ss`.
 */

/**
 * Zonas IANA que admite la pantalla. La de la instalación va primero.
 *
 * CORE no publica todavía una zona por instalación; mientras tanto la del
 * negocio es la misma con la que ventas numera sus pedidos
 * (`TransactionalSeriesAllocator.BusinessTimeZone`). Toda conversión pasa por
 * el reloj IANA de la zona, nunca por un desplazamiento fijo escrito aquí:
 * añadir una zona es añadir su identificador a esta lista.
 */
export const REPORT_ZONES = ['America/Lima', 'UTC'] as const;
export type ReportZone = (typeof REPORT_ZONES)[number];
export const INSTALLATION_ZONE: ReportZone = REPORT_ZONES[0];

export function isReportZone(value: unknown): value is ReportZone {
  return typeof value === 'string' && (REPORT_ZONES as readonly string[]).includes(value);
}

const formatters = new Map<ReportZone, Intl.DateTimeFormat>();

/** Fecha y hora de pared de un instante en la zona, `YYYY-MM-DDTHH:mm:ss`. */
export function wallClock(instantMs: number, zone: ReportZone): string {
  let formatter = formatters.get(zone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat('en-US', {
      timeZone: zone, hourCycle: 'h23', era: 'short', year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
    });
    formatters.set(zone, formatter);
  }
  const p = Object.fromEntries(formatter.formatToParts(new Date(instantMs)).map((part) => [part.type, part.value]));
  if (p.era !== 'AD') return '';
  return `${p.year.padStart(4, '0')}-${p.month}-${p.day}T${p.hour}:${p.minute}:${p.second}`;
}

/** El mismo texto leído como si fuera UTC; NaN si no es una fecha de calendario. */
function wallAsUtc(wall: string): number {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})$/.exec(wall);
  if (!m) return NaN;
  const d = new Date(0);
  d.setUTCFullYear(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  d.setUTCHours(Number(m[4]), Number(m[5]), Number(m[6]), 0);
  return d.getTime();
}

/**
 * Período explícito: jamás se deduce de la zona del navegador.
 * `datetime-local` entrega `HH:mm` o, según el navegador, `HH:mm:ss`.
 * Lanza `RangeError` si el valor no forma una fecha válida.
 *
 * D11: además exige ida y vuelta. Algunos motores normalizan fechas imposibles
 * (`2026-02-30` → `2026-03-02`) en vez de rechazarlas; una fecha así consultaría
 * en silencio otro día del que el usuario escribió. La misma ida y vuelta
 * rechaza la hora que no existe en la zona (salto de horario) y la que existe
 * dos veces (retroceso): ninguna se resuelve adivinando.
 */
export function asInstant(value: string, zone: ReportZone): string {
  const parts = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})(?::(\d{2}))?$/.exec(value);
  if (!parts) throw new RangeError('Fecha y hora con formato inesperado.');
  if (!isReportZone(zone)) throw new RangeError('Zona horaria no admitida.');
  const [, minutes, seconds = '00'] = parts;
  // Mismo rango que explainComparison (D14) y que DateTimeOffset de .NET: año 1..9999.
  if (minutes.startsWith('0000')) throw new RangeError('El año 0000 no existe en el calendario de la consulta.');
  const wall = `${minutes}:${seconds}`;
  const naive = wallAsUtc(wall);
  if (!Number.isFinite(naive) || new Date(naive).toISOString().slice(0, 19) !== wall) throw new RangeError('Fecha inválida.');
  // Candidatos: el texto menos cada desplazamiento que la zona usa alrededor de él.
  // En los bordes del año 1 y del 9999 el día vecino cae fuera del calendario.
  const offsets = new Set([-1, 0, 1].map((days) => {
    const at = naive + days * 86_400_000;
    return wallAsUtc(wallClock(at, zone)) - at;
  }).filter(Number.isFinite));
  const matches = [...offsets].map((offset) => naive - offset).filter((at) => wallClock(at, zone) === wall);
  if (matches.length === 0) throw new RangeError('La fecha no existe en la zona seleccionada.');
  if (matches.length > 1) throw new RangeError('Esa hora se repite en la zona seleccionada; elige otra.');
  const instant = new Date(matches[0]).toISOString();
  // El borde también vale en UTC: 9999-12-31 23:59 en Lima ya es el año 10000.
  if (!/^\d{4}-/.test(instant) || instant.startsWith('0000')) {
    throw new RangeError('La fecha queda fuera del calendario de la consulta.');
  }
  return instant;
}

export function showDate(value: string, zone: ReportZone): string {
  return new Date(value).toLocaleString('es-PE', { timeZone: zone, dateStyle: 'medium', timeStyle: 'short' });
}

/** Un recuento ausente se muestra como guion, nunca como cero. */
export function showCount(value: number | null): string {
  return value === null ? '—' : value.toLocaleString('es-PE');
}

/** Rangos de días calendario completos, con el huso elegido por el usuario. */
export type QuickPeriod = 'today' | 'last7' | 'thisMonth';

export function quickPeriodRange(period: QuickPeriod, zone: ReportZone, now: Date): { start: string; end: string } {
  // El día de pared en la zona, por su reloj IANA: no depende del navegador.
  const [year, month, day] = wallClock(now.getTime(), zone).slice(0, 10).split('-').map(Number);
  const monthIndex = month - 1;
  const midnight = Date.UTC(year, monthIndex, day);
  const oneDay = 24 * 60 * 60 * 1000;
  let first: Date;
  let last: Date;

  if (period === 'today') {
    first = new Date(midnight);
    last = new Date(midnight + oneDay);
  } else if (period === 'last7') {
    // Siete días calendario: hoy más los seis anteriores. Fin exclusivo mañana.
    first = new Date(midnight - 6 * oneDay);
    last = new Date(midnight + oneDay);
  } else {
    first = new Date(Date.UTC(year, monthIndex, 1));
    last = new Date(Date.UTC(year, monthIndex + 1, 1));
  }

  const localInput = (instant: Date) => `${instant.toISOString().slice(0, 10)}T00:00`;
  return { start: localInput(first), end: localInput(last) };
}
