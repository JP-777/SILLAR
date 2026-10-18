/**
 * M10 D17 — Comparación descriptiva de la composición de estados ACTUALES (I03).
 * Reutiliza las fotografías que D8 ya validó y la decisión D15 calculada sobre ellas.
 * No interroga a M03, no normaliza I03 por duración y no interpreta transiciones.
 */
import type { ReportComparisonSnapshot, ReportComparisonDecision } from './reportComparisonGate';

export interface StatusDescriptor {
  readonly code: string;
  readonly label: string;
}
export interface StatusMixRow {
  readonly code: string;
  readonly label: string;
  readonly countA: number;
  readonly countB: number;
  /** Null = no existe denominador I01; NUNCA representa un 0%. */
  readonly shareAPct: number | null;
  readonly shareBPct: number | null;
  /** Diferencia en puntos porcentuales, no cambio temporal. */
  readonly differencePoints: number | null;
}
export type StatusMixResult =
  | {
      readonly kind: 'available';
      readonly i01A: number;
      readonly i01B: number;
      readonly observedAtA: string;
      readonly observedAtB: string;
      readonly rows: readonly StatusMixRow[];
      readonly interpretation: 'independent_snapshots';
      readonly historicalTransitions: false;
    }
  | { readonly kind: 'withheld'; readonly reason: string };

function validCount(n: number | null | undefined): n is number {
  return typeof n === 'number' && Number.isSafeInteger(n) && n >= 0;
}
function validateCounts(snapshot: ReportComparisonSnapshot, codes: readonly string[]): {
  readonly total: number;
  readonly counts: readonly number[];
} | null {
  const total = snapshot.metrics.i01;
  if (!validCount(total)) return null;
  const counts: number[] = [];
  let accumulated = 0;
  for (const code of codes) {
    const key = `status:${code}`;
    // D8 verificó el arreglo I03 original, su suma I01 y la no duplicación.
    // La proyección D16 convierte los estados omitidos (recuento cero) en null;
    // solo aquí, después de D8, esa ausencia equivale a CERO.
    if (!Object.prototype.hasOwnProperty.call(snapshot.metrics, key)) return null;
    const raw = snapshot.metrics[key];
    const value = raw === null ? 0 : raw;
    if (!validCount(value)) return null;
    accumulated += value;
    if (!Number.isSafeInteger(accumulated) || accumulated > total) return null;
    counts.push(value);
  }
  if (accumulated !== total) return null;
  return { total, counts };
}

export function compareCohortStatusMix(
  baseline: ReportComparisonSnapshot,
  compared: ReportComparisonSnapshot,
  gate: ReportComparisonDecision,
  statuses: readonly StatusDescriptor[],
): StatusMixResult {
  if (gate.decision !== 'comparable' || gate.differenceBasis !== 'independent_snapshots') {
    return { kind: 'withheld', reason: 'D15 no habilita esta comparación.' };
  }
  if (!baseline.validated || !compared.validated) {
    return { kind: 'withheld', reason: 'Falta verificación previa D8.' };
  }
  if (!['with_data', 'empty'].includes(baseline.state) ||
      !['with_data', 'empty'].includes(compared.state)) {
    return { kind: 'withheld', reason: 'Fuente no disponible o error.' };
  }
  if (statuses.length !== 7 || new Set(statuses.map(s => s.code)).size !== statuses.length ||
      statuses.some(s => !s.code || !s.label)) {
    return { kind: 'withheld', reason: 'Catálogo de estados inválido.' };
  }
  const a = validateCounts(baseline, statuses.map(s => s.code));
  const b = validateCounts(compared, statuses.map(s => s.code));
  if (!a || !b) {
    return { kind: 'withheld', reason: 'Inconsistencia I03/I01: estados no cubren exactamente la cohorte.' };
  }
  if (!baseline.window.observedAtUtc || !compared.window.observedAtUtc) {
    return { kind: 'withheld', reason: 'Faltan instantes de observación.' };
  }
  const rows = statuses.map((s, i) => {
    const shareAPct = a.total > 0 ? a.counts[i] * 100 / a.total : null;
    const shareBPct = b.total > 0 ? b.counts[i] * 100 / b.total : null;
    const differencePoints = shareAPct === null || shareBPct === null
      ? null : shareBPct - shareAPct;
    return {
      code: s.code, label: s.label,
      countA: a.counts[i], countB: b.counts[i],
      shareAPct, shareBPct, differencePoints,
    };
  });
  if (rows.some(r => [r.shareAPct, r.shareBPct, r.differencePoints]
    .some(n => n !== null && !Number.isFinite(n)))) {
    return { kind: 'withheld', reason: 'Porcentaje no representable.' };
  }
  return {
    kind: 'available', i01A: a.total, i01B: b.total,
    observedAtA: baseline.window.observedAtUtc,
    observedAtB: compared.window.observedAtUtc,
    rows, interpretation: 'independent_snapshots', historicalTransitions: false,
  };
}
