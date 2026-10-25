/**
 * M10 — consumidor de la observación de zona horaria que publicará CORE.
 *
 * CORE todavía no la publica, ni está ratificado con qué clave ni por qué
 * endpoint. Aquí no se supone ninguno: esta es la forma CANDIDATA que M10 sabe
 * consumir, y el adaptador que traduzca lo que CORE publique a esta forma se
 * escribe en integración, no aquí.
 *
 * Cinco estados, y solo uno habilita la zona:
 *
 * - `absent`: la instalación no tiene zona configurada.
 * - `approved_initial`: no hay zona configurada y CORE responde con la zona
 *   inicial aprobada. Sirve a la consulta de siempre, pero no está verificada:
 *   no habilita nada nuevo.
 * - `invalid`: tiene algo configurado que no es una zona.
 * - `runtime_resolved`: un motor de fechas la reconoció, pero nadie con
 *   autoridad la ha canonizado. ICU, por ejemplo, contesta `Asia/Calcutta` a
 *   `Asia/Kolkata`: reconocer no es verificar.
 * - `verified`: identificador IANA canónico verificado por la autoridad.
 *
 * Todo lo que no encaje exactamente en una de estas formas se lee como
 * inválido. El texto original se conserva tal cual en todos los estados.
 */

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

export type ZoneObservation =
  | { readonly kind: 'absent' }
  | { readonly kind: 'approved_initial'; readonly zone: string }
  | { readonly kind: 'invalid'; readonly original: string | null }
  | { readonly kind: 'runtime_resolved'; readonly original: string; readonly runtimeId: string }
  | { readonly kind: 'verified'; readonly original: string; readonly canonical: string };

/** Zona de la instalación ya consumida: habilitada o bloqueada, con su porqué. */
export type ConsumedZone =
  | {
    readonly kind: 'enabled';
    readonly zone: string;
    /** Lo que la instalación tenía escrito, sin tocar. */
    readonly original: string;
    /** El original es un alias del canónico según la autoridad, no según el motor. */
    readonly alias: boolean;
  }
  /** Zona inicial aprobada: utilizable para consultar, nunca verificada. */
  | { readonly kind: 'initial'; readonly zone: string }
  | {
    readonly kind: 'blocked';
    readonly cause: 'absent' | 'invalid' | 'not_authoritative' | 'unsupported' | 'inconsistent';
    readonly original: string | null;
    readonly reason: string;
  };

const text = (value: unknown): value is string => typeof value === 'string' && value !== '';

/** De lo que llegue por la costura a una observación. Lo que no encaja es `invalid`. */
export function readZoneObservation(input: unknown): ZoneObservation {
  if (typeof input !== 'object' || input === null || Array.isArray(input)) return { kind: 'invalid', original: null };
  const o = input as Record<string, unknown>;
  const original = typeof o.original === 'string' ? o.original : null;
  switch (o.kind) {
    case 'absent':
      return { kind: 'absent' };
    case 'approved_initial':
      return text(o.zone) ? { kind: 'approved_initial', zone: o.zone } : { kind: 'invalid', original };
    case 'invalid':
      return { kind: 'invalid', original };
    case 'runtime_resolved':
      return text(o.original) && text(o.runtimeId)
        ? { kind: 'runtime_resolved', original: o.original, runtimeId: o.runtimeId }
        : { kind: 'invalid', original };
    case 'verified':
      return text(o.original) && text(o.canonical)
        ? { kind: 'verified', original: o.original, canonical: o.canonical }
        : { kind: 'invalid', original };
    default:
      return { kind: 'invalid', original };
  }
}

const FIX = 'Pide a quien administra la instalación que la configure.';
const UNSUPPORTED = 'Este navegador no reconoce la zona horaria de la instalación, así que no puede calcular los períodos. Actualiza el navegador o usa otro.';

/** Cómo llama este motor de fechas a la zona, o null si no la conoce. */
function runtimeId(zone: string): string | null {
  try {
    return new Intl.DateTimeFormat('en-US', { timeZone: zone }).resolvedOptions().timeZone;
  } catch {
    return null;
  }
}

/**
 * Solo una observación verificada, utilizable aquí y coherente habilita la zona.
 * La inicial aprobada se usa para consultar si está bien escrita y el motor la
 * conoce; si no, bloquea igual que una configurada.
 */
export function consumeZoneObservation(observation: ZoneObservation): ConsumedZone {
  switch (observation.kind) {
    case 'absent':
      return { kind: 'blocked', cause: 'absent', original: null, reason: `La instalación no tiene configurada su zona horaria, así que no se puede consultar ni preparar ningún período. ${FIX}` };
    case 'approved_initial': {
      const { zone } = observation;
      if (IANA_SHAPE.test(zone) && runtimeId(zone) === null) {
        return { kind: 'blocked', cause: 'unsupported', original: zone, reason: UNSUPPORTED };
      }
      return canonicalIanaZone(zone) === zone
        ? { kind: 'initial', zone }
        : { kind: 'blocked', cause: 'invalid', original: zone, reason: `La zona horaria configurada en la instalación no es válida, así que no se puede consultar ni preparar ningún período. ${FIX}` };
    }
    case 'invalid':
      return { kind: 'blocked', cause: 'invalid', original: observation.original, reason: `La zona horaria configurada en la instalación no es válida, así que no se puede consultar ni preparar ningún período. ${FIX}` };
    case 'runtime_resolved':
      return { kind: 'blocked', cause: 'not_authoritative', original: observation.original, reason: 'La zona horaria de la instalación todavía no está verificada, así que no se puede consultar ni preparar ningún período. Vuelve a intentarlo cuando la instalación la haya confirmado.' };
    case 'verified': {
      const { original, canonical } = observation;
      // Con forma IANA pero desconocida para este motor: la zona puede ser buena
      // y el navegador, viejo. Sin forma, o con otras mayúsculas: no es válida.
      if (IANA_SHAPE.test(canonical) && runtimeId(canonical) === null) {
        return { kind: 'blocked', cause: 'unsupported', original, reason: UNSUPPORTED };
      }
      if (canonicalIanaZone(canonical) !== canonical) {
        return { kind: 'blocked', cause: 'invalid', original, reason: `La zona horaria configurada en la instalación no es válida, así que no se puede consultar ni preparar ningún período. ${FIX}` };
      }
      // Si el motor conoce también el original, tiene que ser la misma zona.
      const originalId = original === canonical ? null : runtimeId(original);
      if (originalId !== null && originalId !== runtimeId(canonical)) {
        return { kind: 'blocked', cause: 'inconsistent', original, reason: `La zona horaria de la instalación llegó con dos valores que no coinciden, así que no se puede consultar ni preparar ningún período. ${FIX}` };
      }
      return { kind: 'enabled', zone: canonical, original, alias: original !== canonical };
    }
  }
}
