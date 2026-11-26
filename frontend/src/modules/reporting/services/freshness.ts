/**
 * M10 — antigüedad de una observación de ventas.
 *
 * Los recuentos son estados actuales: valen en el instante en que ventas los
 * observó. Una pantalla que se queda abierta sigue enseñándolos, y a la media
 * hora ya no describen el presente. No se refresca sola —consultar es decisión
 * del usuario—, pero se dice cuándo dejaron de estar al día.
 */

/** A partir de aquí la cifra se presenta como posiblemente desactualizada. */
export const STALE_AFTER_MINUTES = 15;

/**
 * Margen para relojes desajustados entre este equipo y el servidor. Más allá,
 * una observación «del futuro» no permite calcular ninguna antigüedad.
 */
const CLOCK_SKEW_MS = 2 * 60_000;

export type Freshness =
  | { readonly kind: 'fresh'; readonly minutes: number }
  | { readonly kind: 'stale'; readonly minutes: number }
  | { readonly kind: 'unknown'; readonly reason: string };

export function assessFreshness(observedAtUtc: string | null, nowMs: number): Freshness {
  const observed = observedAtUtc === null ? NaN : Date.parse(observedAtUtc);
  if (!Number.isFinite(observed) || !Number.isFinite(nowMs)) {
    return { kind: 'unknown', reason: 'La fuente no informó cuándo observó estos datos.' };
  }
  const age = nowMs - observed;
  if (age < -CLOCK_SKEW_MS) {
    return { kind: 'unknown', reason: 'El reloj de este equipo no coincide con el del servidor; no se puede saber la antigüedad de los datos.' };
  }
  const minutes = Math.max(0, Math.floor(age / 60_000));
  return minutes >= STALE_AFTER_MINUTES ? { kind: 'stale', minutes } : { kind: 'fresh', minutes };
}

/** «hace 3 min», «hace 2 h 5 min»: la frase que acompaña a «Observado». */
export function describeAge(minutes: number): string {
  if (minutes < 1) return 'hace menos de un minuto';
  if (minutes < 60) return `hace ${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `hace ${hours} h` : `hace ${hours} h ${rest} min`;
}
