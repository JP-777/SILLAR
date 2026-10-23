/**
 * M10 — de dónde sale la zona horaria de los reportes.
 *
 * La autoridad es la instalación, y la publica CORE. Esa fuente no existe
 * todavía, así que aquí no se fabrica: hay un único punto de conexión que hoy
 * vale null y, sin él, la zona es la del negocio, la misma con la que ventas
 * numera sus pedidos (`TransactionalSeriesAllocator.BusinessTimeZone`).
 *
 * Ese respaldo vale **solo** mientras no hay fuente. Si la fuente existe y
 * publica algo que no es una zona IANA canónica, no hay zona: sustituirla por
 * la del negocio daría períodos, consultas y CSV con horas equivocadas sin
 * que nadie lo note. Los reportes quedan bloqueados hasta que se corrija.
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
  | { readonly zone: string; readonly origin: 'business_default'; readonly reason: string }
  | { readonly zone: null; readonly origin: 'unavailable'; readonly reason: string };

/**
 * Identificador IANA tal como lo escribe la base de zonas, o null.
 *
 * Vale si el motor de fechas lo reconoce y está bien escrito. Rechaza
 * desplazamientos sueltos (`-05:00`), rutas, espacios y el mismo nombre con
 * otras mayúsculas (`america/lima`, `AMERICA/LIMA`).
 *
 * No exige que el motor lo devuelva igual: ICU contesta con nombres antiguos
 * (`America/Argentina/Buenos_Aires` → `America/Buenos_Aires`, `Asia/Kolkata` →
 * `Asia/Calcutta`), y exigirlo bloqueaba como inválidas instalaciones con el
 * nombre IANA vigente. Basta con que no sea solo un cambio de mayúsculas.
 */
const IANA_SHAPE = /^(?!.*(?:^|\/)[A-Z]{4,}(?:\/|$))[A-Z][A-Za-z_]*(?:\/[A-Z][A-Za-z0-9_+-]*){0,2}$/;

export function canonicalIanaZone(value: unknown): string | null {
  if (typeof value !== 'string' || !IANA_SHAPE.test(value)) return null;
  let known: string;
  try {
    known = new Intl.DateTimeFormat('en-US', { timeZone: value }).resolvedOptions().timeZone;
  } catch {
    return null;
  }
  return known === value || known.toLowerCase() !== value.toLowerCase() ? value : null;
}

export function resolveInstallationZone(source: InstallationZoneSource | null = installationZoneSource): InstallationZone {
  if (source === null) {
    return { zone: BUSINESS_DEFAULT_ZONE, origin: 'business_default', reason: 'La instalación todavía no publica su zona horaria.' };
  }
  const zone = canonicalIanaZone(source.zone);
  if (zone === null) {
    return {
      zone: null,
      origin: 'unavailable',
      reason: 'La zona horaria configurada en la instalación no es válida, así que no se puede consultar ni preparar ningún período. Pide a quien administra la instalación que la corrija.',
    };
  }
  return { zone, origin: 'installation' };
}
