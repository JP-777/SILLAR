import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { Failure } from '../../../shared/errors/messages';
import { FailureAlert } from '../../../shared/ui/patterns';
import { asInstant, showCount, showDate, type ReportZone } from '../services/period';
import {
  describeReportFailure,
  loadOrderIndicators,
  orderStates,
  type OrderIndicatorsView,
} from '../services/reporting';
import './order-period-comparison.css';

type ComparisonState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ready'; data: OrderIndicatorsView }
  | { kind: 'error'; failure: Failure };

interface Props {
  /** Cohorte principal, ya validada por D8 y nunca source_unavailable. */
  readonly baseline: OrderIndicatorsView;
  readonly zone: ReportZone;
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

/**
 * M10 D10: compara DOS cohortes de creación, consultadas en instantes independientes.
 *
 * No representa una evolución del estado de un mismo pedido ni una fotografía
 * histórica. Ningún importe o cliente atraviesa esta superficie. Ambas
 * respuestas pasan por la validación de D8 dentro de `loadOrderIndicators`.
 */
export function OrderPeriodComparison({ baseline, zone }: Props) {
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [inputError, setInputError] = useState<string | null>(null);
  const [state, setState] = useState<ComparisonState>({ kind: 'idle' });
  // D4: cada cambio de los límites y el desmontaje anulan las peticiones en vuelo.
  const generation = useRef(0);

  useEffect(() => () => {
    generation.current += 1;
  }, []);

  function invalidate() {
    generation.current += 1;
    setState({ kind: 'idle' });
    setInputError(null);
  }

  async function compare(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const current = ++generation.current;
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
    setState({ kind: 'loading' });
    try {
      const received = await loadOrderIndicators(from, until);
      if (generation.current !== current) return;
      setState(received.status === 'source_unavailable'
        ? { kind: 'unavailable' }
        : { kind: 'ready', data: received });
    } catch (error) {
      if (generation.current !== current) return;
      setState({ kind: 'error', failure: describeReportFailure(error, 'comparar las cohortes de pedidos') });
    }
  }

  const comparison = state.kind === 'ready' ? state.data : null;
  const rows = comparison === null ? [] : [
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

  return (
    <section className="rp-compare" aria-labelledby="rp-compare-heading">
      <h2 id="rp-compare-heading">Comparar con otra cohorte</h2>
      <p className="rp-compare__note">
        Compara los pedidos creados en dos intervalos [inicio, fin). Cada cohorte muestra el estado
        que sus pedidos tienen ahora, observado en su propio instante; no es un historial de cambios
        de estado ni dice cuántos pedidos pasaron de un estado a otro.
      </p>
      <form className="rp-compare__filters" onSubmit={(event) => { void compare(event); }}>
        <label>
          Inicio de la comparación (incluido)
          <input className="ui-input" type="datetime-local" required value={start}
            onChange={(event) => { setStart(event.target.value); invalidate(); }} />
        </label>
        <label>
          Fin de la comparación (excluido)
          <input className="ui-input" type="datetime-local" required value={end}
            onChange={(event) => { setEnd(event.target.value); invalidate(); }} />
        </label>
        <button className="ui-button" type="submit" disabled={state.kind === 'loading'}>
          Comparar cohortes
        </button>
      </form>
      <p className="rp-compare__note">Zona aplicada: {zone}, la misma de la consulta principal.</p>
      {inputError && <p role="alert" className="rp-compare__error">{inputError}</p>}
      {state.kind === 'loading' && <p role="status">Consultando la segunda cohorte…</p>}
      {state.kind === 'unavailable' && (
        <p role="status">El módulo de ventas no está activo, así que no se pueden calcular diferencias.</p>
      )}
      {state.kind === 'error' && state.failure.kind !== 'silent' && (
        <FailureAlert failure={state.failure} />
      )}
      {comparison && (
        <>
          <p className="rp-compare__note">
            A: [{showDate(baseline.startInclusive, zone)}, {showDate(baseline.endExclusive, zone)}),
            observada {baseline.observedAtUtc ? showDate(baseline.observedAtUtc, zone) : 'sin instante informado'}.
            {' '}B: [{showDate(comparison.startInclusive, zone)}, {showDate(comparison.endExclusive, zone)}),
            observada {comparison.observedAtUtc ? showDate(comparison.observedAtUtc, zone) : 'sin instante informado'}.
          </p>
          <div className="rp-compare__scroll">
            <table className="rp-compare__table">
              <caption>Recuentos de cada cohorte y diferencia B − A. No indica transiciones entre estados.</caption>
              <thead><tr><th scope="col">Indicador</th><th scope="col">A · Principal</th>
                <th scope="col">B · Comparación</th><th scope="col">Diferencia B − A</th></tr></thead>
              <tbody>{rows.map((row) => (
                <tr key={row.code}><th scope="row">{row.label}</th>
                  <td>{showCount(row.a)}</td><td>{showCount(row.b)}</td>
                  <td>{difference(row.a, row.b)}</td></tr>
              ))}</tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}
