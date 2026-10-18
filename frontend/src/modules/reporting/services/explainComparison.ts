/**
 * D13 — interpretación de cohortes, pura y sin efectos.
 * Los instantes se validan como ISO 8601 con offset explícito, incluida
 * la validez calendárica. No se utiliza Date.parse para aceptar fechas.
 */
export interface CohortWindow {
  readonly startInclusive: string;
  readonly endExclusive: string;
  readonly observedAtUtc: string | null;
}
export type CohortRelation = 'identical' | 'overlapping' | 'adjacent' | 'disjoint' | 'invalid';
export type ObservationRelation = 'same_instant' | 'different_instants' | 'missing' | 'invalid';
export interface ComparisonExplanation {
  readonly relation: CohortRelation;
  readonly observationRelation: ObservationRelation;
  readonly overlapMinutes: number | null;
  readonly notes: readonly string[];
  readonly mayPresentAsTimeEvolution: false;
  readonly mayAddTotals: false;
}

// DateTimeOffset puede emitir hasta siete dígitos decimales (ticks de 100 ns).
const ISO = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?(Z|([+-])(\d{2}):(\d{2}))$/;
interface Instant {
  readonly epochMs: number;
  readonly subMs100ns: number;
}
const isLeapYear = (y: number) => y % 4 === 0 && (y % 100 !== 0 || y % 400 === 0);

/** No normaliza 2026-02-30 a marzo ni permite offsets imposibles. */
function strictInstant(input: string): Instant | null {
  const m = ISO.exec(input);
  if (!m) return null;
  const year = Number(m[1]);
  const month = Number(m[2]);
  const day = Number(m[3]);
  const hour = Number(m[4]);
  const minute = Number(m[5]);
  const second = Number(m[6]);
  if (month < 1 || month > 12 || hour > 23 || minute > 59 || second > 59) return null;
  const lengths = [31, isLeapYear(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  if (day < 1 || day > lengths[month - 1]) return null;
  let offsetMinutes = 0;
  if (m[8] !== 'Z') {
    const oh = Number(m[10]);
    const om = Number(m[11]);
    if (oh > 14 || om > 59 || (oh === 14 && om !== 0)) return null;
    offsetMinutes = (m[9] === '+' ? 1 : -1) * (oh * 60 + om);
  }
  const fraction = (m[7] ?? '').padEnd(7, '0');
  const milli = Number(fraction.slice(0, 3));
  const subMs100ns = Number(fraction.slice(3, 7));
  // setUTCFullYear evita la reescritura de años 00..99 de Date.UTC.
  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day);
  date.setUTCHours(hour, minute, second, milli);
  const epochMs = date.getTime() - offsetMinutes * 60_000;
  if (!Number.isFinite(epochMs)) return null;
  return { epochMs, subMs100ns };
}

function compareInstant(a: Instant, b: Instant): number {
  return a.epochMs === b.epochMs
    ? Math.sign(a.subMs100ns - b.subMs100ns)
    : Math.sign(a.epochMs - b.epochMs);
}
function differenceMs(a: Instant, b: Instant): number {
  return (a.epochMs - b.epochMs) + (a.subMs100ns - b.subMs100ns) / 10_000;
}
function compareObservations(a: CohortWindow, b: CohortWindow): ObservationRelation {
  if (a.observedAtUtc === null || b.observedAtUtc === null) return 'missing';
  const left = strictInstant(a.observedAtUtc);
  const right = strictInstant(b.observedAtUtc);
  if (left === null || right === null) return 'invalid';
  return compareInstant(left, right) === 0 ? 'same_instant' : 'different_instants';
}

export function explainComparison(a: CohortWindow, b: CohortWindow): ComparisonExplanation {
  const observationRelation = compareObservations(a, b);
  const aStart = strictInstant(a.startInclusive);
  const aEnd = strictInstant(a.endExclusive);
  const bStart = strictInstant(b.startInclusive);
  const bEnd = strictInstant(b.endExclusive);
  const notes: string[] = [];
  let relation: CohortRelation;
  let overlapMinutes: number | null = null;
  if (!aStart || !aEnd || !bStart || !bEnd
      || compareInstant(aStart, aEnd) >= 0 || compareInstant(bStart, bEnd) >= 0) {
    relation = 'invalid';
    notes.push('Período inválido. No interpretar diferencias entre cohortes.');
  } else {
    const lastStart = compareInstant(aStart, bStart) >= 0 ? aStart : bStart;
    const firstEnd = compareInstant(aEnd, bEnd) <= 0 ? aEnd : bEnd;
    const overlapMs = Math.max(0, differenceMs(firstEnd, lastStart));
    overlapMinutes = overlapMs / 60_000;
    if (compareInstant(aStart, bStart) === 0 && compareInstant(aEnd, bEnd) === 0) {
      relation = 'identical';
      notes.push('Las dos columnas representan la misma cohorte; no tratarlas como grupos distintos.');
    } else if (overlapMs > 0) {
      relation = 'overlapping';
      notes.push('Las cohortes se solapan: no sumar ni presentar sus totales como independientes.');
    } else if (compareInstant(aEnd, bStart) === 0 || compareInstant(bEnd, aStart) === 0) {
      relation = 'adjacent';
      notes.push('Períodos contiguos [inicio, fin) con el final exclusivo.');
    } else {
      relation = 'disjoint';
      notes.push('Cohortes disjuntas. Los resultados son fotografías, no transiciones históricas.');
    }
  }
  if (observationRelation === 'different_instants') {
    notes.push('Instantes de observación distintos; los estados actuales pueden diferir por esa causa.');
  } else if (observationRelation === 'missing') {
    notes.push('Falta el instante de observación de al menos una cohorte.');
  } else if (observationRelation === 'invalid') {
    notes.push('Instante de observación inválido; no afirmar simultaneidad.');
  }
  return { relation, observationRelation, overlapMinutes, notes,
    mayPresentAsTimeEvolution: false, mayAddTotals: false };
}
