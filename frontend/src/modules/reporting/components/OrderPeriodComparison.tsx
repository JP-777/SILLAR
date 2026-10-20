import { OrderCreationWindowPanel } from './OrderCreationWindowPanel';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { Failure } from '../../../shared/errors/messages';
import { FailureAlert } from '../../../shared/ui/patterns';
import { explainComparison, type CohortRelation } from '../services/explainComparison';
import { assessReportComparison, type ReportComparisonSnapshot } from '../services/reportComparisonGate';
import { previousEqualWindow } from '../services/previousEqualWindow';
import { asInstant, showCount, showDate, type ReportZone } from '../services/period';
import {
  describeReportFailure,
  loadOrderIndicators,
  orderStates,
  type OrderIndicatorsView,
} from '../services/reporting';
import './order-period-comparison.css';

/**
 * D11: cada resultado lleva la fotografía A con la que se pidió. Si la cohorte
 * principal cambia, un resultado B anterior deja de valer aunque llegue tarde.
 */
type ComparisonState =
  | { kind: 'idle' }
  | { kind: 'loading'; context: string }
  | { kind: 'unavailable'; context: string }
  | { kind: 'ready'; context: string; data: OrderIndicatorsView }
  | { kind: 'error'; context: string; failure: Failure };

interface Props {
  /** Cohorte principal, ya validada por D8 y nunca source_unavailable. */
  readonly baseline: OrderIndicatorsView;
  readonly zone: ReportZone;
  /**
   * D12: límites B traídos por un enlace compartido. Solo rellenan el formulario
   * al montarse; no consultan nada hasta que el usuario pulse «Comparar cohortes».
   */
  readonly initialPeriod?: { readonly start: string; readonly end: string } | null;
  /** D12: avisa a la página de lo que el usuario escribe, para poder compartirlo. */
  readonly onPeriodInput?: (start: string, end: string) => void;
}

function difference(first: number | null, second: number | null): string {
  if (first === null || second === null) return '—';
  const delta = second - first;
  return `${delta > 0 ? '+' : ''}${showCount(delta)}`;
}

/** Un estado que la respuesta no trae queda en blanco; no se inventa un cero. */
function statusCount(report: OrderIndicatorsView, code: string): number | null {
  return report.i03CurrentStatusCounts?.find((item) => item.status === code)?.count ?? null;
}

const SHARE = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 1 });

/** Participación sobre su propia cohorte. Sin cohorte (0 pedidos) no hay tasa. */
function share(value: number | null, total: number | null): number | null {
  return value === null || total === null || total === 0 ? null : (100 * value) / total;
}

function showShare(value: number | null): string {
  return value === null ? '—' : `${SHARE.format(value)} %`;
}

/** Diferencia de participación en puntos porcentuales; no es un cambio histórico. */
function showPoints(a: number | null, b: number | null): string {
  if (a === null || b === null) return '—';
  const delta = b - a;
  return `${delta > 0 ? '+' : ''}${SHARE.format(delta)} pp`;
}

/**
 * Todo lo que define la fotografía A. Si cambia cualquiera de estos valores, un
 * resultado B pedido antes ya no se compara con lo que la pantalla muestra.
 */
function contextOf(baseline: OrderIndicatorsView, zone: ReportZone): string {
  return JSON.stringify([
    zone, baseline.status, baseline.startInclusive, baseline.endExclusive, baseline.observedAtUtc,
    baseline.i01CreatedOrders, baseline.i04CurrentlyExpiredOrders,
    baseline.activeOrders, baseline.inactiveOrders,
    [...(baseline.i03CurrentStatusCounts ?? [])]
      .sort((x, y) => x.status.localeCompare(y.status))
      .map((entry) => [entry.status, entry.count]),
  ]);
}

/** D13: nombre visible de la relación entre las dos ventanas de creación. */
const RELATION_LABEL: Record<CohortRelation, string> = {
  identical: 'Misma cohorte: A y B tienen exactamente el mismo período.',
  overlapping: 'Cohortes solapadas: comparten pedidos, así que no son grupos independientes.',
  adjacent: 'Cohortes contiguas: B empieza donde termina A, o al revés, sin pedidos en común.',
  disjoint: 'Cohortes separadas: hay un hueco entre los dos períodos y no comparten pedidos.',
  invalid: 'No se pudo interpretar la relación entre los períodos.',
};

/** Duración legible del solape: días, horas y minutos, sin redondear a cero. */
function showDuration(totalMinutes: number): string {
  const minutes = Math.round(totalMinutes);
  if (minutes === 0) return totalMinutes > 0 ? 'menos de un minuto' : '0 minutos';
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const rest = minutes % 60;
  const parts = [
    days ? `${days.toLocaleString('es-PE')} ${days === 1 ? 'día' : 'días'}` : '',
    hours ? `${hours} ${hours === 1 ? 'hora' : 'horas'}` : '',
    rest ? `${rest} ${rest === 1 ? 'minuto' : 'minutos'}` : '',
  ].filter(Boolean);
  return parts.join(' y ');
}

/**
 * M10 D10: compara DOS cohortes de creación, consultadas en instantes independientes.
 *
 * No representa una evolución del estado de un mismo pedido ni una fotografía
 * histórica. Ningún importe o cliente atraviesa esta superficie. Ambas
 * respuestas pasan por la validación de D8 dentro de `loadOrderIndicators`.
 */
export function OrderPeriodComparison({ baseline, zone, initialPeriod = null, onPeriodInput }: Props) {
  const [start, setStart] = useState(initialPeriod?.start ?? '');
  const [end, setEnd] = useState(initialPeriod?.end ?? '');
  const [inputError, setInputError] = useState<string | null>(null);
  const [state, setState] = useState<ComparisonState>({ kind: 'idle' });
  // D4: cada cambio de los límites y el desmontaje anulan las peticiones en vuelo.
  const generation = useRef(0);
  // D11: la página ya remonta el componente con `key` al cambiar la consulta
  // principal; esta segunda barrera hace que el componente no dependa de ello.
  const context = contextOf(baseline, zone);
  const currentContext = useRef(context);
  currentContext.current = context;

  useEffect(() => () => {
    generation.current += 1;
  }, []);

  function invalidate() {
    generation.current += 1;
    setState({ kind: 'idle' });
    setInputError(null);
  }

  function choosePreviousEqualWindow() {
    const suggested = previousEqualWindow(baseline.startInclusive, baseline.endExclusive, zone);
    // Invalida cualquier GET anterior o pendiente antes de cambiar filtros B.
    invalidate();
    if (suggested.kind === 'unavailable') {
      setInputError(suggested.reason);
      return;
    }
    setStart(suggested.startLocal);
    setEnd(suggested.endLocal);
    // D12: actualizar el enlace compartible al rellenar ambos filtros.
    onPeriodInput?.(suggested.startLocal, suggested.endLocal);
  }

  async function compare(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const current = ++generation.current;
    const requestingContext = context;
    // Vale solo si nadie pidió otra cosa y la fotografía A sigue siendo la misma.
    const stillCurrent = () => generation.current === current
      && currentContext.current === requestingContext;
    if (!start || !end) {
      setInputError('Indica fecha y hora de inicio y fin de la cohorte que quieres comparar.');
      return;
    }
    let from: string;
    let until: string;
    try {
      from = asInstant(start, zone);
      until = asInstant(end, zone);
    } catch {
      setInputError('Revisa las fechas de la comparación antes de consultar.');
      return;
    }
    if (Date.parse(until) <= Date.parse(from)) {
      setInputError('El fin de la comparación debe ser posterior al inicio. El fin no se incluye.');
      return;
    }

    setInputError(null);
    setState({ kind: 'loading', context: requestingContext });
    try {
      // D8 ya comprueba aquí que B es del período pedido y que cuadra.
      const received = await loadOrderIndicators(from, until);
      if (!stillCurrent()) return;
      setState(received.status === 'source_unavailable'
        ? { kind: 'unavailable', context: requestingContext }
        : { kind: 'ready', context: requestingContext, data: received });
    } catch (error) {
      if (!stillCurrent()) return;
      setState({
        kind: 'error',
        context: requestingContext,
        failure: describeReportFailure(error, 'comparar las cohortes de pedidos'),
      });
    }
  }

  // Un resultado de otra fotografía A no se dibuja, aunque siga en el estado.
  const shown: ComparisonState = state.kind !== 'idle' && state.context !== context
    ? { kind: 'idle' } : state;
  const comparison = shown.kind === 'ready' ? shown.data : null;
  const counts = comparison === null ? [] : [
    { code: 'created', label: 'Pedidos creados', a: baseline.i01CreatedOrders, b: comparison.i01CreatedOrders },
    { code: 'expired-now', label: 'Actualmente vencidos', a: baseline.i04CurrentlyExpiredOrders, b: comparison.i04CurrentlyExpiredOrders },
    { code: 'active', label: 'Activos', a: baseline.activeOrders, b: comparison.activeOrders },
    { code: 'inactive', label: 'Dados de baja', a: baseline.inactiveOrders, b: comparison.inactiveOrders },
    ...orderStates.map((item) => ({
      code: item.code,
      label: `Estado actual: ${item.label}`,
      a: statusCount(baseline, item.code),
      b: statusCount(comparison, item.code),
    })),
  ];
  // D11: participación de cada fila sobre los pedidos creados de SU cohorte. La
  // fila «Pedidos creados» es el propio denominador: su 100 % no informa nada.
  const rows = counts.map((row) => {
    const own = row.code !== 'created';
    const shareA = own ? share(row.a, baseline.i01CreatedOrders) : null;
    const shareB = own && comparison ? share(row.b, comparison.i01CreatedOrders) : null;
    return { ...row, shareA, shareB };
  });
  // D13: cómo se relacionan las dos ventanas y sus observaciones. Puro: se
  // recalcula de A y B mostrados, así que nunca describe una respuesta obsoleta.
  const explanation = comparison === null ? null : explainComparison(baseline, comparison);
  const interpretable = explanation !== null && explanation.relation !== 'invalid';
  // D14: la compuerta decide si se puede restar B − A. A y B solo llegan aquí
  // tras loadOrderIndicators, que ya aplicó verifyOrderIndicators (D8): por eso
  // `validated` es cierto, no porque la compuerta lo compruebe.
  const snapshotOf = (report: OrderIndicatorsView, pick: (row: typeof counts[number]) => number | null): ReportComparisonSnapshot => ({
    window: report,
    state: report.status,
    validated: true,
    metrics: Object.fromEntries(counts.map((row) => [row.code, pick(row)])),
  });
  const gate = comparison === null ? null : assessReportComparison(
    snapshotOf(baseline, (row) => row.a),
    snapshotOf(comparison, (row) => row.b),
  );
  const mayShowDifferences = gate?.decision === 'comparable';
  // Motivos propios de la compuerta; las notas de relación ya se muestran arriba.
  const withheldReasons = gate === null || mayShowDifferences
    ? [] : gate.reasons.filter((reason) => !gate.explanation.notes.includes(reason));

  // Ciclo 3 · accesibilidad: región persistente, fuera del nodo con aria-busy
  // (patrón de Table en shared/ui/patterns.tsx). Los errores van por role="alert".
  const announcement = shown.kind === 'loading'
    ? 'Consultando la segunda cohorte.'
    : shown.kind === 'unavailable'
      ? 'El módulo de ventas no está activo; no se pueden calcular diferencias.'
      : comparison && !interpretable
        ? ''
        : comparison
          ? mayShowDifferences
            ? 'Comparación consultada. Se muestran las diferencias descriptivas entre A y B.'
            : 'Comparación consultada. Las diferencias entre A y B no se muestran; las cifras de cada cohorte sí.'
          : '';

  return (
    <section className="rp-compare" aria-labelledby="rp-compare-heading">
      <h2 id="rp-compare-heading">Comparar con otra cohorte</h2>
      <div className="sr-only" role="status" aria-live="polite" aria-atomic="true">{announcement}</div>
      <p className="rp-compare__note">
        Compara los pedidos creados en dos intervalos [inicio, fin). Cada cohorte muestra el estado
        que sus pedidos tienen ahora, observado en su propio instante; no es un historial de cambios
        de estado ni dice cuántos pedidos pasaron de un estado a otro.
      </p>
      <form className="rp-compare__filters" onSubmit={(event) => { void compare(event); }}>
        <label>
          Inicio de la comparación (incluido)
          <input className="ui-input" type="datetime-local" required value={start}
            onChange={(event) => { setStart(event.target.value); invalidate(); onPeriodInput?.(event.target.value, end); }} />
        </label>
        <label>
          Fin de la comparación (excluido)
          <input className="ui-input" type="datetime-local" required value={end}
            onChange={(event) => { setEnd(event.target.value); invalidate(); onPeriodInput?.(start, event.target.value); }} />
        </label>
        <button className="ui-button" type="button" onClick={choosePreviousEqualWindow}>
          Seleccionar período anterior equivalente
        </button>
        <p className="rp-compare__note">
          Solo rellena la ventana B contigua anterior de duración equivalente; no ejecuta consultas.
          Cada respuesta posterior es una fotografía independiente, nunca transición histórica.
        </p>
        <button className="ui-button" type="submit" disabled={shown.kind === 'loading'}>
          Comparar cohortes
        </button>
      </form>
      {/* D21: el enlace con A, B y zona lo genera «Copiar enlace a estos filtros»
          de la página (D12), que recibe B por onPeriodInput. El panel D20 lo duplicaba. */}
      <p className="rp-compare__note">Zona aplicada: {zone}, la misma de la consulta principal.</p>
      {inputError && <p role="alert" className="rp-compare__error">{inputError}</p>}
      {shown.kind === 'error' && shown.failure.kind !== 'silent' && (
        <FailureAlert failure={shown.failure} />
      )}
      <div className="rp-compare__results" aria-busy={shown.kind === 'loading'}>
      {shown.kind === 'loading' && <p className="rp-compare__note">Consultando la segunda cohorte…</p>}
      {shown.kind === 'unavailable' && (
        <p>El módulo de ventas no está activo, así que no se pueden calcular diferencias.</p>
      )}
      {explanation && (
        <section className="rp-compare__context" aria-labelledby="rp-compare-context-heading">
          <h3 id="rp-compare-context-heading">Relación entre las cohortes</h3>
          <p><strong>{RELATION_LABEL[explanation.relation]}</strong></p>
          {explanation.overlapMinutes !== null && explanation.relation !== 'disjoint'
            && explanation.relation !== 'adjacent' && (
            <p>Tiempo en común: {showDuration(explanation.overlapMinutes)}.</p>
          )}
          <ul className="rp-compare__notes">
            {explanation.notes.map((note) => <li key={note}>{note}</li>)}
          </ul>
        </section>
      )}
      {comparison && !interpretable && (
        <p role="alert" className="rp-compare__error">
          Los períodos recibidos no permiten comparar las cohortes, así que no se muestran diferencias.
          Vuelve a consultar la comparación.
        </p>
      )}
      {comparison && interpretable && (
        <>
          <OrderCreationWindowPanel baseline={baseline} compared={comparison} />
          <p className="rp-compare__note">
            A: [{showDate(baseline.startInclusive, zone)}, {showDate(baseline.endExclusive, zone)}),
            observada {baseline.observedAtUtc ? showDate(baseline.observedAtUtc, zone) : 'sin instante informado'}.
            {' '}B: [{showDate(comparison.startInclusive, zone)}, {showDate(comparison.endExclusive, zone)}),
            observada {comparison.observedAtUtc ? showDate(comparison.observedAtUtc, zone) : 'sin instante informado'}.
          </p>
          <p className="rp-compare__note">
            Cada porcentaje se calcula sobre los pedidos creados de su propia cohorte; una cohorte
            sin pedidos no tiene porcentajes.
            {mayShowDifferences && ' La diferencia de participación va en puntos porcentuales (pp) y compara dos fotografías, no un cambio de un mismo grupo en el tiempo.'}
          </p>
          {mayShowDifferences && gate?.observationNotice && (
            <p className="rp-compare__note" role="note">
              <strong>Resta descriptiva B − A entre consultas independientes.</strong>{' '}
              {gate.observationNotice}{' '}
              A observada: {baseline.observedAtUtc ? showDate(baseline.observedAtUtc, zone) : 'sin instante'};{' '}
              B observada: {comparison.observedAtUtc ? showDate(comparison.observedAtUtc, zone) : 'sin instante'}.
            </p>
          )}
          {!mayShowDifferences && (
            <div className="rp-compare__withheld">
              <p><strong>No se muestran diferencias entre A y B.</strong> Las cifras de cada cohorte sí son válidas por separado.</p>
              {withheldReasons.length > 0 && (
                <ul className="rp-compare__notes">
                  {withheldReasons.map((reason) => <li key={reason}>{reason}</li>)}
                </ul>
              )}
            </div>
          )}
          <div className="rp-compare__scroll">
            <table className="rp-compare__table">
              <caption>{mayShowDifferences
                ? 'Recuentos por cohorte y resta descriptiva B − A entre consultas independientes; no son transiciones históricas.'
                : 'Recuentos y participación de cada cohorte, por separado. Sin diferencias entre cohortes.'}</caption>
              <thead><tr><th scope="col">Indicador</th><th scope="col">A · Principal</th>
                <th scope="col">B · Comparación</th>
                {mayShowDifferences && <th scope="col">Resta descriptiva B − A</th>}
                <th scope="col">% de A</th><th scope="col">% de B</th>
                {mayShowDifferences && <th scope="col">Diferencia descriptiva de participación</th>}</tr></thead>
              <tbody>{rows.map((row) => (
                <tr key={row.code}><th scope="row">{row.label}</th>
                  <td>{showCount(row.a)}</td><td>{showCount(row.b)}</td>
                  {mayShowDifferences && <td>{difference(row.a, row.b)}</td>}
                  <td>{showShare(row.shareA)}</td><td>{showShare(row.shareB)}</td>
                  {mayShowDifferences && <td>{showPoints(row.shareA, row.shareB)}</td>}</tr>
              ))}</tbody>
            </table>
          </div>
        </>
      )}
      </div>
    </section>
  );
}
