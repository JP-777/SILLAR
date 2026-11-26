/**
 * M10 D14 — compuerta de interpretación de dos fotografías de pedidos.
 * Ninguna consulta, cambio de permisos, envío ni conversión a historia.
 * Debe alimentarse EXCLUSIVAMENTE de respuestas que el llamador haya pasado
 * previamente por verifyOrderIndicators (D8). `validated` no sustituye esa llamada.
 */
import { explainComparison, type CohortWindow, type ComparisonExplanation } from './explainComparison.ts';

export type ReportingDataState = 'with_data' | 'empty' | 'source_unavailable' | 'error';
export interface ReportComparisonSnapshot {
  readonly window: CohortWindow;
  readonly state: ReportingDataState;
  readonly validated: boolean;
  /** I01, I04, activos/inactivos, estados: claves estables que aporta el UI. */
  readonly metrics: Readonly<Record<string, number | null>>;
}
export interface ComparableMetric {
  readonly code: string;
  readonly baseline: number | null;
  readonly compared: number | null;
  /** NULO si falta un valor: jamás convertir datos ausentes en cero. */
  readonly absoluteDifference: number | null;
  /** NULO si el denominador inicial es 0 o faltan valores. */
  readonly relativeDifferencePct: number | null;
}
export interface ReportComparisonDecision {
  readonly decision: 'comparable' | 'withheld';
  /** Las diferencias son aritmética descriptiva, nunca sincronización. */
  readonly differenceBasis: 'independent_snapshots' | 'none';
  readonly observationNotice: string | null;
  readonly provesSameSnapshot: false;
  readonly explanation: ComparisonExplanation;
  readonly reasons: readonly string[];
  readonly metrics: readonly ComparableMetric[];
  readonly isHistoricalTransition: false;
  readonly mayAddCohortTotals: false;
}
const validCount = (value: number | null): boolean =>
  value === null || (Number.isSafeInteger(value) && value >= 0);
const HAS_OWN = (obj: Readonly<Record<string, number | null>>, key: string) =>
  Object.prototype.hasOwnProperty.call(obj, key);

export function assessReportComparison(
  baseline: ReportComparisonSnapshot,
  compared: ReportComparisonSnapshot,
): ReportComparisonDecision {
  const explanation = explainComparison(baseline.window, compared.window);
  const reasons: string[] = []; // Las notas de D13 son informativas.
  if (!baseline.validated || !compared.validated) {
    reasons.push('Ambos informes deben pasar la verificación de coherencia antes de calcular diferencias.');
  }
  if (!(baseline.state === 'with_data' || baseline.state === 'empty') ||
      !(compared.state === 'with_data' || compared.state === 'empty')) {
    reasons.push('Fuente ausente o error: no calcular diferencias.');
  }
  if (explanation.relation === 'invalid' || explanation.relation === 'identical' ||
      explanation.relation === 'overlapping') {
    reasons.push('No comparar matemáticamente períodos inválidos, idénticos o solapados.');
  }
  // Dos GET independientes nunca acreditan una lectura atómica, aunque
  // coincidan sus timestamps. La resta descriptiva exige instantes trazables.
  if (explanation.observationRelation === 'missing' ||
      explanation.observationRelation === 'invalid') {
    reasons.push('Ambas observaciones deben existir y ser instantes ISO válidos.');
  }
  const aKeys = Object.keys(baseline.metrics).sort();
  const bKeys = Object.keys(compared.metrics).sort();
  if (aKeys.length === 0 || aKeys.length !== bKeys.length ||
      aKeys.some((key, i) => key !== bKeys[i])) {
    reasons.push('Las dos fotografías no tienen las mismas métricas.');
  }
  for (const key of [...new Set([...aKeys, ...bKeys])]) {
    if (!HAS_OWN(baseline.metrics, key) || !HAS_OWN(compared.metrics, key) ||
        !validCount(baseline.metrics[key]) || !validCount(compared.metrics[key])) {
      reasons.push('Existe alguna métrica ausente, negativa, no entera o fuera del rango exacto.');
      break;
    }
  }
  const decision = reasons.length === 0 ? 'comparable' : 'withheld';
  // En períodos disjuntos/contiguos la explicación puede contener observaciones
  // informativas, pero todas las prohibiciones anteriores mantienen los deltas ocultos.
  const metrics: ComparableMetric[] = decision === 'withheld' ? [] : aKeys.map((code) => {
    const a = baseline.metrics[code] ?? null;
    const b = compared.metrics[code] ?? null;
    const delta = a === null || b === null ? null : b - a;
    const ratio = delta === null || a === null || a === 0 ? null : 100 * delta / a;
    return { code, baseline: a, compared: b,
      absoluteDifference: delta, relativeDifferencePct: ratio };
  });
  return { decision, explanation, reasons, metrics,
    differenceBasis: decision === 'comparable' ? 'independent_snapshots' : 'none',
    observationNotice: decision === 'comparable'
      ? 'B − A es una resta descriptiva entre lecturas independientes; no son fotografías sincronizadas ni transiciones históricas.'
      : null,
    provesSameSnapshot: false,
    isHistoricalTransition: false, mayAddCohortTotals: false };
}
