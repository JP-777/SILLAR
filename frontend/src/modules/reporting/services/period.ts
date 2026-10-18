/**
 * Fechas y recuentos de los reportes, en un solo sitio.
 *
 * D10: la consulta principal y la comparación de cohortes convierten el período
 * con la misma regla; antes cada una tenía la suya y la de la comparación volvía
 * a rechazar `HH:mm:ss`.
 */

/** Los dos husos que admite la pantalla. Lima es UTC−05 sin horario de verano. */
export type ReportZone = 'America/Lima' | 'UTC';

const ZONE_OFFSET_HOURS: Record<ReportZone, number> = { 'America/Lima': -5, UTC: 0 };

/**
 * Período explícito: jamás se deduce de la zona del navegador.
 * `datetime-local` entrega `HH:mm` o, según el navegador, `HH:mm:ss`.
 * Lanza `RangeError` si el valor no forma una fecha válida.
 *
 * D11: además exige ida y vuelta. Algunos motores normalizan fechas imposibles
 * (`2026-02-30` → `2026-03-02`) en vez de rechazarlas; una fecha así consultaría
 * en silencio otro día del que el usuario escribió.
 */
export function asInstant(value: string, zone: ReportZone): string {
  const parts = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})(?::(\d{2}))?$/.exec(value);
  if (!parts) throw new RangeError('Fecha y hora con formato inesperado.');
  const [, minutes, seconds = '00'] = parts;
  const offsetHours = ZONE_OFFSET_HOURS[zone];
  const offset = offsetHours === 0 ? 'Z' : `-${String(-offsetHours).padStart(2, '0')}:00`;
  const parsed = new Date(`${minutes}:${seconds}${offset}`);
  if (!Number.isFinite(parsed.getTime())) throw new RangeError('Fecha inválida.');
  const reflected = new Date(parsed.getTime() + offsetHours * 3_600_000).toISOString().slice(0, 19);
  if (reflected !== `${minutes}:${seconds}`) throw new RangeError('La fecha no existe en la zona seleccionada.');
  return parsed.toISOString();
}

export function showDate(value: string, zone: ReportZone): string {
  return new Date(value).toLocaleString('es-PE', { timeZone: zone, dateStyle: 'medium', timeStyle: 'short' });
}

/** Un recuento ausente se muestra como guion, nunca como cero. */
export function showCount(value: number | null): string {
  return value === null ? '—' : value.toLocaleString('es-PE');
}
