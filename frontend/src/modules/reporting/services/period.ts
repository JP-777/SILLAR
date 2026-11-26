import { asZonedInstant, wallClock } from '../../../shared/zonedDate.ts';
/**
 * Fechas y recuentos de los reportes, en un solo sitio.
 *
 * D10: la consulta principal y la comparación de cohortes convierten el período
 * con la misma regla; antes cada una tenía la suya y la de la comparación volvía
 * a rechazar `HH:mm:ss`.
 */

import { resolveInstallationZone, type InstallationZone } from './installationZone.ts';

/**
 * Zona IANA, ya comprobada contra `REPORT_ZONES`. Es un texto porque la de la
 * instalación la decidirá CORE; por eso toda entrada pasa por `isReportZone`.
 */
export type ReportZone = string;

/**
 * Zonas que admite la pantalla: la de la instalación primero y UTC.
 *
 * Toda conversión pasa por el reloj IANA de la zona, nunca por un
 * desplazamiento fijo escrito aquí. La zona de la instalación sale de
 * `installationZone.ts`, no de la URL ni del navegador.
 */
export let INSTALLATION: InstallationZone = resolveInstallationZone();
/** null cuando la instalación publica una zona inválida: no hay reportes. */
export let INSTALLATION_ZONE: ReportZone | null = INSTALLATION.zone;

/**
 * Sin zona acreditada la lista queda vacía, también sin UTC: un período en UTC
 * cortaría los días en otra hora que el negocio y parecería correcto. Con la
 * lista vacía, `isReportZone` rechaza todo y nada forma un período, un enlace
 * ni un CSV.
 */
export function reportZonesFor(installation: InstallationZone): readonly ReportZone[] {
  return installation.zone === null ? [] : [...new Set([installation.zone, 'UTC'])];
}

export let REPORT_ZONES: readonly ReportZone[] = reportZonesFor(INSTALLATION);

/**
 * Adopta la zona que publica CORE. La pantalla la llama con lo que devuelve
 * `loadInstallationZoneObservation()` antes de montar la vista; hasta entonces
 * no forma ningún período. Los tres valores de arriba son enlaces vivos: todo
 * lo que los lee (períodos, enlaces, comparación, CSV, exportación) ve el nuevo.
 */
export function adoptInstallationZone(observation: unknown): InstallationZone {
  INSTALLATION = resolveInstallationZone(observation);
  INSTALLATION_ZONE = INSTALLATION.zone;
  REPORT_ZONES = reportZonesFor(INSTALLATION);
  return INSTALLATION;
}

export function isReportZone(value: unknown): value is ReportZone {
  return typeof value === 'string' && REPORT_ZONES.includes(value);
}

/** Nombre de la zona para un selector, con su desplazamiento de hoy. */
export function zoneLabel(zone: ReportZone, now: Date): string {
  if (zone === 'UTC') return 'UTC (UTC+00:00)';
  const offset = new Intl.DateTimeFormat('en-US', { timeZone: zone, timeZoneName: 'longOffset' })
    .formatToParts(now).find((part) => part.type === 'timeZoneName')?.value.replace('GMT', 'UTC').replace('-', '−') ?? '';
  const origin = zone === INSTALLATION_ZONE
    ? INSTALLATION.origin === 'installation' ? 'Zona de la instalación'
      : INSTALLATION.origin === 'approved_initial' ? 'Zona inicial de la instalación, sin verificar' : 'Zona del negocio'
    : 'Zona';
  return `${origin} · ${zone} (${offset === 'UTC' ? 'UTC+00:00' : offset})`;
}

// La admisión de la zona sigue perteneciendo a M10; la conversión es compartida.
export { wallClock } from '../../../shared/zonedDate.ts';
export function asInstant(value: string, zone: ReportZone): string {
  if (!isReportZone(zone)) throw new RangeError('Zona horaria no admitida.');
  return asZonedInstant(value, zone);
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
