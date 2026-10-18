/**
 * Fechas y recuentos de los reportes, en un solo sitio.
 *
 * D10: la consulta principal y la comparación de cohortes convierten el período
 * con la misma regla; antes cada una tenía la suya y la de la comparación volvía
 * a rechazar `HH:mm:ss`.
 */

/** Los dos husos que admite la pantalla. Lima es UTC−05 sin horario de verano. */
export type ReportZone = 'America/Lima' | 'UTC';

/**
 * Período explícito: jamás se deduce de la zona del navegador.
 * `datetime-local` entrega `HH:mm` o, según el navegador, `HH:mm:ss`.
 * Lanza `RangeError` si el valor no forma una fecha válida.
 */
export function asInstant(value: string, zone: ReportZone): string {
  const withSeconds = /T\d{2}:\d{2}$/.test(value) ? `${value}:00` : value;
  return new Date(`${withSeconds}${zone === 'America/Lima' ? '-05:00' : 'Z'}`).toISOString();
}

export function showDate(value: string, zone: ReportZone): string {
  return new Date(value).toLocaleString('es-PE', { timeZone: zone, dateStyle: 'medium', timeStyle: 'short' });
}

/** Un recuento ausente se muestra como guion, nunca como cero. */
export function showCount(value: number | null): string {
  return value === null ? '—' : value.toLocaleString('es-PE');
}
