import { useEffect, useRef, useState, type FormEvent } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { FailureAlert } from '../../../shared/ui/patterns';
import { loadOrderIndicators, orderStates, type OrderIndicatorsView } from '../services/reporting';
import '../reporting.css';

type TimeZone = 'America/Lima' | 'UTC';
type ViewState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; data: OrderIndicatorsView }
  | { kind: 'error'; failure: Failure };

/** Período explícito: jamás se deduce de la zona del navegador. */
function asInstant(value: string, zone: TimeZone): string {
  return new Date(`${value}:00${zone === 'America/Lima' ? '-05:00' : 'Z'}`).toISOString();
}

function showDate(value: string, zone: TimeZone): string {
  return new Date(value).toLocaleString('es-PE', { timeZone: zone, dateStyle: 'medium', timeStyle: 'short' });
}

/** Rangos de días calendario completos, con el huso elegido por el usuario. */
type QuickPeriod = 'today' | 'last7' | 'thisMonth';

function quickPeriodRange(period: QuickPeriod, zone: TimeZone, now: Date): { start: string; end: string } {
  // Los dos únicos husos permitidos son UTC y Lima (UTC-05, sin horario de verano).
  // El desplazamiento se aplica al reloj antes de tomar año/mes/día UTC, evitando
  // depender de la zona horaria del navegador o del sistema operativo.
  const offsetMs = zone === 'America/Lima' ? -5 * 60 * 60 * 1000 : 0;
  const localDay = new Date(now.getTime() + offsetMs);
  const year = localDay.getUTCFullYear();
  const month = localDay.getUTCMonth();
  const day = localDay.getUTCDate();
  const midnight = Date.UTC(year, month, day);
  const oneDay = 24 * 60 * 60 * 1000;
  let first: Date;
  let last: Date;

  if (period === 'today') {
    first = new Date(midnight);
    last = new Date(midnight + oneDay);
  } else if (period === 'last7') {
    // Siete días calendario: hoy más los seis anteriores. Fin exclusivo mañana.
    first = new Date(midnight - 6 * oneDay);
    last = new Date(midnight + oneDay);
  } else {
    first = new Date(Date.UTC(year, month, 1));
    last = new Date(Date.UTC(year, month + 1, 1));
  }

  const localInput = (instant: Date) => `${instant.toISOString().slice(0, 10)}T00:00`;
  return { start: localInput(first), end: localInput(last) };
}

/** M10 D3: cohorte de pedidos con filtros [inicio,fin), sin totales monetarios. */
export function ReportsPage() {
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [zone, setZone] = useState<TimeZone>('America/Lima');
  const [state, setState] = useState<ViewState>({ kind: 'idle' });
  const [inputError, setInputError] = useState<string | null>(null);

  // Cada modificación del período o de la zona invalida toda petición anterior.
  // Es síncrono: un resultado viejo nunca puede pisar la consulta vigente.
  const requestVersion = useRef(0);

  useEffect(() => () => {
    requestVersion.current += 1;
  }, []);

  function invalidateConsultation() {
    requestVersion.current += 1;
    setState({ kind: 'idle' });
  }

  function chooseQuickPeriod(period: QuickPeriod) {
    const range = quickPeriodRange(period, zone, new Date());
    // D4: invalidar sincrónicamente cualquier respuesta de filtros anteriores.
    invalidateConsultation();
    setInputError(null);
    setStart(range.start);
    setEnd(range.end);
  }

  async function consultar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentRequest = ++requestVersion.current;
    if (!start || !end) {
      setInputError('Indica fecha y hora de inicio y fin antes de consultar.');
      return;
    }
    const first = asInstant(start, zone);
    const last = asInstant(end, zone);
    if (new Date(last).getTime() <= new Date(first).getTime()) {
      setInputError('El fin debe ser posterior al inicio. El fin no se incluye.');
      return;
    }
    setInputError(null);
    setState({ kind: 'loading' });
    try {
      const data = await loadOrderIndicators(first, last);
      if (requestVersion.current === currentRequest) {
        setState({ kind: 'ready', data });
      }
    } catch (error) {
      if (requestVersion.current === currentRequest) {
        setState({ kind: 'error', failure: describe(error, 'consultar los indicadores de pedidos') });
      }
    }
  }

  if (state.kind === 'error' && state.failure.kind === 'forbidden') {
    return <ForbiddenPage minimum="admin" />;
  }

  const data = state.kind === 'ready' ? state.data : null;
  const statusCounts = new Map(data?.i03CurrentStatusCounts?.map((s) => [s.status, s.count]) ?? []);

  return (
    <PageContainer title="Reportes de pedidos" description="Pedidos creados en el intervalo elegido, según su estado actual. Incluye pedidos dados de baja.">
      <div className="rp-stack">
        <form className="rp-filters" onSubmit={(e) => void consultar(e)}>
          <div className="rp-presets" role="group" aria-label="Períodos rápidos">
            <span className="rp-presets__label">Períodos rápidos (días calendario)</span>
            <button className="ui-button" type="button" onClick={() => chooseQuickPeriod('today')}>Hoy</button>
            <button className="ui-button" type="button" onClick={() => chooseQuickPeriod('last7')}>Últimos 7 días</button>
            <button className="ui-button" type="button" onClick={() => chooseQuickPeriod('thisMonth')}>Este mes</button>
          </div>
          <label className="rp-field">
            Inicio (incluido)
            <input className="ui-input" type="datetime-local" value={start} onChange={(e) => { setStart(e.target.value); invalidateConsultation(); }} required />
          </label>
          <label className="rp-field">
            Fin (excluido)
            <input className="ui-input" type="datetime-local" value={end} onChange={(e) => { setEnd(e.target.value); invalidateConsultation(); }} required />
          </label>
          <label className="rp-field">
            Zona horaria
            <select className="ui-input" value={zone} onChange={(e) => { setZone(e.target.value as TimeZone); invalidateConsultation(); }}>
              <option value="America/Lima">Perú · America/Lima (UTC−05:00)</option>
              <option value="UTC">UTC (UTC+00:00)</option>
            </select>
          </label>
          <button className="ui-button" type="submit" disabled={state.kind === 'loading'}>Consultar período</button>
        </form>
        {inputError && <p className="rp-warning" role="alert">{inputError}</p>}
        {state.kind === 'idle' && <p className="rp-note">Selecciona un período y consulta sus indicadores. No se asigna uno por defecto.</p>}
        {state.kind === 'loading' && <p role="status">Consultando indicadores…</p>}
        {state.kind === 'error' && state.failure.kind !== 'silent' && <FailureAlert failure={state.failure} />}
        {data?.status === 'source_unavailable' && (
          <section className="rp-panel" role="status">
            <h2>Fuente de pedidos no disponible</h2>
            <p>M03 no está activo en esta instalación. No es correcto presentar los indicadores como cero.</p>
          </section>
        )}
        {data && data.status !== 'source_unavailable' && (
          <>
            <p className="rp-note">Observado: {data.observedAtUtc ? showDate(data.observedAtUtc, zone) : 'Sin instante informado'} · Zona: {zone}</p>
            {data.status === 'empty' && <p role="status">No se crearon pedidos en este período. Los recuentos son cero.</p>}
            <div className="rp-kpis">
              <section className="rp-panel"><h2>I01 · Pedidos creados</h2><strong>{data.i01CreatedOrders}</strong></section>
              <section className="rp-panel"><h2>I04 · Actualmente vencidos</h2><strong>{data.i04CurrentlyExpiredOrders}</strong></section>
              <section className="rp-panel"><h2>Actividad</h2><p>Activos: <strong>{data.activeOrders}</strong></p><p>Inactivos: <strong>{data.inactiveOrders}</strong></p></section>
            </div>
            <section className="rp-panel">
              <h2>I03 · Distribución por estado actual</h2>
              <table className="rp-table">
                <thead><tr><th scope="col">Estado</th><th scope="col">Pedidos</th></tr></thead>
                <tbody>{orderStates.map((status) => (
                  <tr key={status.code}><th scope="row">{status.label}</th><td>{statusCounts.get(status.code) ?? 0}</td></tr>
                ))}</tbody>
              </table>
            </section>
            <p className="rp-note">Cohorte: [{showDate(data.startInclusive, zone)}, {showDate(data.endExclusive, zone)}). Los vencidos son los que tienen estado actual «Vencido»; no es una evaluación histórica del plazo.</p>
          </>
        )}
      </div>
    </PageContainer>
  );
}
