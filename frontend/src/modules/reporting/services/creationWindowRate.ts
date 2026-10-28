/**
 * M10 D16 — Normalización descriptiva de I01 por duración de la ventana.
 * Dos GET conservan instantes independientes: esto no es un análisis histórico
 * ni una comparación sincronizada. I03/I04 NO se dividen por la duración.
 *
 * Precondición obligatoria: decision procede de assessReportComparison(D15),
 * y las fotografías ya pasaron por verifyOrderIndicators(D8).
 */
import type { ReportComparisonDecision, ReportComparisonSnapshot } from './reportComparisonGate';

export interface CreationWindowRate {
  readonly kind: 'available';
  readonly windowADays: number;
  readonly windowBDays: number;
  readonly ordersA: number;
  readonly ordersB: number;
  readonly averagePer24hA: number;
  readonly averagePer24hB: number;
  readonly averagePer24hDifference: number;
  readonly coverageNotice: string;
  readonly synchronized: false;
  readonly historicTransition: false;
}
export interface CreationWindowRateWithheld {
  readonly kind: 'withheld';
  readonly reason: string;
}
export type CreationWindowRateResult = CreationWindowRate | CreationWindowRateWithheld;

const DAY_MS = 86_400_000;
function durationDays(window: ReportComparisonSnapshot['window']): number | null {
  // explainComparison(D13) ya validó estrictamente los límites calendario/offset.
  // Date.parse se usa AQUÍ solo como aritmética sobre valores previamente validados.
  const start = Date.parse(window.startInclusive);
  const end = Date.parse(window.endExclusive);
  if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) return null;
  const days = (end - start) / DAY_MS;
  return days > 0 && Number.isFinite(days) ? days : null;
}

/** No recalcula una compuerta retenida ni interpreta los estados como historia. */
export function compareCreationWindowRates(
  baseline: ReportComparisonSnapshot,
  compared: ReportComparisonSnapshot,
  decision: ReportComparisonDecision,
): CreationWindowRateResult {
  if (decision.decision !== 'comparable' || decision.differenceBasis !== 'independent_snapshots') {
    return { kind: 'withheld', reason: 'D15 no autoriza diferencias descriptivas para estas cohortes.' };
  }
  if (!baseline.validated || !compared.validated) {
    return { kind: 'withheld', reason: 'Falta verificación D8 de alguna respuesta.' };
  }
  if (!['with_data', 'empty'].includes(baseline.state) ||
      !['with_data', 'empty'].includes(compared.state)) {
    return { kind: 'withheld', reason: 'Fuente ausente o respuesta inválida.' };
  }
  const a = baseline.metrics.i01;
  const b = compared.metrics.i01;
  if (a === null || b === null || !Number.isSafeInteger(a) ||
      !Number.isSafeInteger(b) || a < 0 || b < 0) {
    return { kind: 'withheld', reason: 'I01 ausente o fuera del rango de recuentos exactos.' };
  }
  const windowADays = durationDays(baseline.window);
  const windowBDays = durationDays(compared.window);
  if (windowADays === null || windowBDays === null) {
    return { kind: 'withheld', reason: 'No puede medirse la duración de una ventana con precisión suficiente.' };
  }
  const averagePer24hA = a / windowADays;
  const averagePer24hB = b / windowBDays;
  const averagePer24hDifference = averagePer24hB - averagePer24hA;
  if (![averagePer24hA, averagePer24hB, averagePer24hDifference].every(Number.isFinite)) {
    return { kind: 'withheld', reason: 'Tasa derivada no representable.' };
  }
  return {
    kind: 'available', windowADays, windowBDays, ordersA: a, ordersB: b,
    averagePer24hA, averagePer24hB, averagePer24hDifference,
    coverageNotice: 'Promedio aritmético de I01 por cada 24 horas de ventana de creación; no es una tendencia histórica, velocidad instantánea ni observación sincronizada.',
    synchronized: false, historicTransition: false,
  };
}
