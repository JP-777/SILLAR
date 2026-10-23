/**
 * M10 — de dónde sale la zona horaria de los reportes.
 *
 * La autoridad es la instalación, y la publica CORE. Esa fuente no existe
 * todavía, así que aquí no se fabrica: hay un único punto de conexión que hoy
 * vale null. Lo que llegue por él se lee y se consume en `zoneObservation.ts`,
 * y solo una observación verificada habilita la zona; ausente, inválida o sin
 * verificar bloquean los reportes.
 *
 * Mientras el punto de conexión siga en null —no conectado, que no es lo mismo
 * que «CORE dice que no hay zona»— se usa la del negocio, la misma con la que
 * ventas numera sus pedidos (`TransactionalSeriesAllocator.BusinessTimeZone`).
 * Es un respaldo HISTÓRICO y transitorio: desaparece al conectar la costura y
 * no es la política para ningún estado de la observación.
 *
 * Lo que llega por la URL, por un formulario o por el navegador **nunca** es
 * autoridad: como mucho elige entre las zonas que esta función admite.
 */

import { consumeZoneObservation, readZoneObservation } from './zoneObservation.ts';

export { canonicalIanaZone } from './zoneObservation.ts';

/** Zona del negocio. La prueba 36 vigila que coincida con la de ventas. */
export const BUSINESS_DEFAULT_ZONE = 'America/Lima';

/**
 * ÚNICO punto de conexión. Integración lo sustituye por la observación que
 * publique CORE, traducida a la forma de `ZoneObservation`.
 */
export const installationZoneObservation: unknown = null;

export type InstallationZone =
  | { readonly zone: string; readonly origin: 'installation'; readonly original: string; readonly alias: boolean }
  /** Respaldo HISTÓRICO con la costura sin conectar: no prueba autoridad. */
  | { readonly zone: string; readonly origin: 'historical_fallback'; readonly reason: string }
  | { readonly zone: null; readonly origin: 'unavailable'; readonly original: string | null; readonly reason: string };

export function resolveInstallationZone(observation: unknown = installationZoneObservation): InstallationZone {
  if (observation === null) {
    return { zone: BUSINESS_DEFAULT_ZONE, origin: 'historical_fallback', reason: 'La instalación todavía no publica su zona horaria.' };
  }
  const consumed = consumeZoneObservation(readZoneObservation(observation));
  return consumed.kind === 'enabled'
    ? { zone: consumed.zone, origin: 'installation', original: consumed.original, alias: consumed.alias }
    : { zone: null, origin: 'unavailable', original: consumed.original, reason: consumed.reason };
}

/**
 * La zona solo si viene de una observación verificada; null con la costura sin
 * conectar (respaldo histórico), ausente, inválida o sin verificar. Lo nuevo
 * que necesite la zona de la instalación se habilita con esto, no con `zone`.
 */
export function verifiedInstallationZone(installation: InstallationZone): string | null {
  return installation.origin === 'installation' ? installation.zone : null;
}
