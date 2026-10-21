/**
 * M10 — de dónde sale la zona horaria de los reportes.
 *
 * La autoridad es la instalación, y la publica CORE. Esa fuente no existe
 * todavía, así que aquí no se fabrica: hay un único punto de conexión que hoy
 * vale null y, sin él, la zona es la del negocio, la misma con la que ventas
 * numera sus pedidos (`TransactionalSeriesAllocator.BusinessTimeZone`).
 *
 * Lo que llega por la URL, por un formulario o por el navegador **nunca** es
 * autoridad: como mucho elige entre las zonas que esta función admite.
 */

/** Zona del negocio. La prueba 36 vigila que coincida con la de ventas. */
export const BUSINESS_DEFAULT_ZONE = 'America/Lima';

/** Lo que CORE tendrá que dar: el identificador IANA configurado. */
export interface InstallationZoneSource {
  readonly zone: unknown;
}

/**
 * ÚNICO punto de conexión. Integración lo sustituye cuando CORE publique la
 * zona de la instalación; hasta entonces se usa la del negocio.
 */
export const installationZoneSource: InstallationZoneSource | null = null;

export type InstallationZone =
  | { readonly zone: string; readonly origin: 'installation' }
  | { readonly zone: string; readonly origin: 'business_default'; readonly reason: string };

/**
 * Identificador IANA tal como lo escribe la base de zonas, o null.
 *
 * Rechaza desplazamientos sueltos (`-05:00`), mayúsculas cambiadas y alias que
 * el motor reescribe: si lo aceptáramos, la misma instalación tendría dos
 * nombres para su zona y un enlace podría pasar por ella.
 */
export function canonicalIanaZone(value: unknown): string | null {
  if (typeof value !== 'string' || !/^[A-Za-z_]+(?:\/[A-Za-z0-9_+-]+){0,2}$/.test(value)) return null;
  try {
    return new Intl.DateTimeFormat('en-US', { timeZone: value }).resolvedOptions().timeZone === value ? value : null;
  } catch {
    return null;
  }
}

export function resolveInstallationZone(source: InstallationZoneSource | null = installationZoneSource): InstallationZone {
  if (source === null) {
    return { zone: BUSINESS_DEFAULT_ZONE, origin: 'business_default', reason: 'La instalación todavía no publica su zona horaria.' };
  }
  const zone = canonicalIanaZone(source.zone);
  if (zone === null) {
    return { zone: BUSINESS_DEFAULT_ZONE, origin: 'business_default', reason: 'La zona publicada por la instalación no es un identificador IANA válido.' };
  }
  return { zone, origin: 'installation' };
}
