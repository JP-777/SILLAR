/**
 * M10 D18 — Comparación descriptiva de ACTIVIDAD, ortogonal a estados I03.
 * Se alimenta SOLO de dos fotografías pasadas por verificación D8 y compuerta D15.
 * No realiza GET, no cambia M03, no predice estados ni interpreta transiciones.
 */
import type { ReportComparisonDecision, ReportComparisonSnapshot } from './reportComparisonGate';

export type ActivityCode = 'active' | 'inactive';

export interface ActivityMixRow {
  readonly code: ActivityCode;
  readonly label: string;
  readonly countA: number;
  readonly countB: number;
  /** Sin denominador I01, la participación es desconocida (no 0%). */
  readonly shareAPct: number | null;
  readonly shareBPct: number | null;
  /** B−A en puntos porcentuales de fotografías independientes. */
  readonly differencePoints: number | null;
}

export type ActivityMixResult =
  | {
      readonly kind: 'available';
      readonly i01A: number;
      readonly i01B: number;
      readonly observedAtA: string;
      readonly observedAtB: string;
      readonly rows: readonly ActivityMixRow[];
      readonly interpretation: 'independent_snapshots';
      readonly historicalTransitions: false;
      readonly synchronizedObservations: false;
    }
  | { readonly kind: 'withheld'; readonly reason: string };

function count(value: number | null | undefined): value is number {
  return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;
}

interface Partition {
  readonly total: number;
  readonly active: number;
  readonly inactive: number;
}

function activityPartition(snapshot: ReportComparisonSnapshot): Partition | null {
  const { i01, active, inactive } = snapshot.metrics;
  // No convertir ausencias en 0. Los ceros verdaderos deben venir de D8.
  if (!count(i01) || !count(active) || !count(inactive)) return null;
  // Evitar redondeos, overflow y doble contabilización.
  const sum = active + inactive;
  if (!Number.isSafeInteger(sum) || sum !== i01) return null;
  return { total: i01, active, inactive };
}

export function compareCohortActivityMix(
  baseline: ReportComparisonSnapshot,
  compared: ReportComparisonSnapshot,
  gate: ReportComparisonDecision,
): ActivityMixResult {
  if (gate.decision !== 'comparable' || gate.differenceBasis !== 'independent_snapshots') {
    return { kind: 'withheld', reason: 'La compuerta D15 no habilita la diferencia descriptiva.' };
  }
  if (!baseline.validated || !compared.validated) {
    return { kind: 'withheld', reason: 'Ambos informes deben superar D8.' };
  }
  if (!(['with_data', 'empty'] as const).includes(baseline.state as 'with_data' | 'empty') ||
      !(['with_data', 'empty'] as const).includes(compared.state as 'with_data' | 'empty')) {
    return { kind: 'withheld', reason: 'Fuente ausente o error: actividad no comparable.' };
  }
  const a = activityPartition(baseline);
  const b = activityPartition(compared);
  if (a === null || b === null) {
    return { kind: 'withheld', reason: 'La partición activo/inactivo no coincide exactamente con I01.' };
  }
  const atA = baseline.window.observedAtUtc;
  const atB = compared.window.observedAtUtc;
  if (!atA || !atB || gate.explanation.observationRelation === 'invalid' ||
      gate.explanation.observationRelation === 'missing') {
    return { kind: 'withheld', reason: 'No hay instantes de observación válidos para ambas cohortes.' };
  }
  const rows: ActivityMixRow[] = (
    [
      { code: 'active', label: 'Activo', countA: a.active, countB: b.active },
      { code: 'inactive', label: 'Inactivo', countA: a.inactive, countB: b.inactive },
    ] as const
  ).map(({ code, label, countA, countB }) => {
    const shareAPct = a.total === 0 ? null : (countA / a.total) * 100;
    const shareBPct = b.total === 0 ? null : (countB / b.total) * 100;
    const differencePoints = shareAPct === null || shareBPct === null
      ? null : shareBPct - shareAPct;
    return { code, label, countA, countB, shareAPct, shareBPct, differencePoints };
  });
  if (rows.some((row) => [row.shareAPct, row.shareBPct, row.differencePoints]
    .some((value) => value !== null && !Number.isFinite(value)))) {
    return { kind: 'withheld', reason: 'Porcentajes de actividad no representables.' };
  }
  return {
    kind: 'available', i01A: a.total, i01B: b.total,
    observedAtA: atA, observedAtB: atB,
    rows, interpretation: 'independent_snapshots',
    historicalTransitions: false, synchronizedObservations: false,
  };
}
