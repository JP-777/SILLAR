import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useLocation } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { type Failure } from '../../../shared/errors/messages';
import { FailureAlert } from '../../../shared/ui/patterns';
import { OrderDistribution } from '../components/OrderDistribution';
import { OrderPeriodComparison } from '../components/OrderPeriodComparison';
import { asInstant, quickPeriodRange, showCount, showDate, type QuickPeriod, type ReportZone } from '../services/period';
import { decodeReportLink, encodeReportLink, type ReportLinkRead } from '../services/reportLinks';
import {
  describeReportFailure,
  loadOrderIndicators,
  type OrderIndicatorsView,
} from '../services/reporting';
import '../reporting.css';

type TimeZone = ReportZone;
type ViewState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; data: OrderIndicatorsView; previous: { start: string; end: string; data: OrderIndicatorsView | null } | null }
  | { kind: 'error'; failure: Failure };

/** M10 D3: cohorte de pedidos con filtros [inicio,fin), sin totales monetarios. */
export function ReportsPage() {
  // D12: la URL solo aporta filtros. `key` hace que cualquier cambio de URL
  // (otro enlace, el menú, atrás/adelante) desmonte la vista: su limpieza D4
  // anula toda respuesta en vuelo y nada consultado antes sigue en pantalla.
  const { search } = useLocation();
  return <ReportsView key={search} link={decodeReportLink(search)} />;
}

type ShareState =
  | { kind: 'none' }
  | { kind: 'copied' }
  | { kind: 'manual'; url: string }
  | { kind: 'incomplete'; message: string };

function ReportsView({ link }: { link: ReportLinkRead }) {
  const fromLink = link.kind === 'valid' ? link.filters : null;
  // Estado inicial desde el enlace; ninguna consulta hasta que el usuario la pida.
  const [start, setStart] = useState(fromLink?.primary.startLocal ?? '');
  const [end, setEnd] = useState(fromLink?.primary.endLocal ?? '');
  const [zone, setZone] = useState<TimeZone>(fromLink?.zone ?? 'America/Lima');
  const [state, setState] = useState<ViewState>({ kind: 'idle' });
  const [inputError, setInputError] = useState<string | null>(null);
  const [comparePrevious, setComparePrevious] = useState(false);
  // Límites B del enlace o los que el usuario escribe en la comparación (D10/D11).
  const [comparisonInput, setComparisonInput] = useState({
    start: fromLink?.comparison?.startLocal ?? '',
    end: fromLink?.comparison?.endLocal ?? '',
  });
  const [share, setShare] = useState<ShareState>({ kind: 'none' });

  async function copyShareLink() {
    const hasB = comparisonInput.start !== '' || comparisonInput.end !== '';
    const path = encodeReportLink({
      zone,
      primary: { startLocal: start, endLocal: end },
      comparison: hasB ? { startLocal: comparisonInput.start, endLocal: comparisonInput.end } : null,
    });
    if (path === null) {
      setShare({
        kind: 'incomplete',
        message: hasB
          ? 'Para compartir, completa un período válido y unas fechas de comparación válidas, o borra las de la comparación.'
          : 'Para compartir, indica un período válido: el fin debe ser posterior al inicio.',
      });
      return;
    }
    const url = `${window.location.origin}${path}`;
    try {
      await navigator.clipboard.writeText(url);
      setShare({ kind: 'copied' });
    } catch {
      // Sin permiso de portapapeles: el enlace queda visible para copiarlo a mano.
      setShare({ kind: 'manual', url });
    }
  }

  // Cada modificación del período o de la zona invalida toda petición anterior.
  // Es síncrono: un resultado viejo nunca puede pisar la consulta vigente.
  const requestVersion = useRef(0);

  useEffect(() => () => {
    requestVersion.current += 1;
  }, []);

  function invalidateConsultation() {
    requestVersion.current += 1;
    setState({ kind: 'idle' });
    // El aviso de entrada describía el período anterior; con otro período ya no aplica.
    setInputError(null);
    // D12: un enlace copiado antes ya no describe los filtros vigentes.
    setShare({ kind: 'none' });
  }

  function chooseQuickPeriod(period: QuickPeriod) {
    const range = quickPeriodRange(period, zone, new Date());
    // D4: invalidar sincrónicamente cualquier respuesta de filtros anteriores.
    invalidateConsultation();
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
    let first: string;
    let last: string;
    try {
      first = asInstant(start, zone);
      last = asInstant(end, zone);
    } catch {
      setInputError('Revisa las fechas indicadas antes de consultar.');
      return;
    }
    const startMs = Date.parse(first);
    const endMs = Date.parse(last);
    if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs <= startMs) {
      setInputError('El fin debe ser posterior al inicio. El fin no se incluye.');
      return;
    }
    // D6: la comparación usa una cohorte consecutiva de igual duración.
    // No se comparan estados como si fueran históricos ni se consulta dinero.
    const previousStartMs = startMs - (endMs - startMs);
    if (comparePrevious && !Number.isFinite(previousStartMs)) {
      setInputError('El período anterior queda fuera del intervalo de fechas válido.');
      return;
    }
    const previousStart = comparePrevious ? new Date(previousStartMs).toISOString() : null;
    setInputError(null);
    setState({ kind: 'loading' });
    try {
      // Una falla en la comparación NO oculta los indicadores principales.
      const previousPromise = previousStart
        ? loadOrderIndicators(previousStart, first).catch(() => null)
        : Promise.resolve(null);
      const [data, previousData] = await Promise.all([
        loadOrderIndicators(first, last),
        previousPromise,
      ]);
      if (requestVersion.current === currentRequest) {
        setState({ kind: 'ready', data, previous: previousStart
          ? { start: previousStart, end: first, data: previousData }
          : null });
      }
    } catch (error) {
      if (requestVersion.current === currentRequest) {
        // D8: una respuesta incoherente es un error con su propia frase, no el
        // genérico de describe() ni «fuente no disponible». No se dibuja ninguna cifra.
        setState({ kind: 'error', failure: describeReportFailure(error, 'consultar los indicadores de pedidos') });
      }
    }
  }

  if (state.kind === 'error' && state.failure.kind === 'forbidden') {
    return <ForbiddenPage minimum="admin" />;
  }

  const data = state.kind === 'ready' ? state.data : null;
  const previous = state.kind === 'ready' ? state.previous : null;
  const previousCount = previous?.data?.i01CreatedOrders ?? null;
  const currentCount = data?.i01CreatedOrders ?? null;
  const delta = currentCount !== null && previousCount !== null
    ? currentCount - previousCount : null;
  // D9: solo una cohorte validada por D8 y con pedidos dibuja distribuciones.
  // Los nulos no se rellenan con cero: si falta un recuento, no se dibuja.
  const distribution = data?.status === 'with_data'
    && data.i01CreatedOrders !== null
    && data.i03CurrentStatusCounts !== null
    && data.activeOrders !== null
    && data.inactiveOrders !== null
    ? {
      createdOrders: data.i01CreatedOrders,
      currentStatusCounts: data.i03CurrentStatusCounts,
      activeOrders: data.activeOrders,
      inactiveOrders: data.inactiveOrders,
    }
    : null;

  // Ciclo 3 · accesibilidad: un único anuncio por estado, leído desde una región
  // que existe desde el primer render y queda fuera del nodo con aria-busy (el
  // mismo patrón que Table en shared/ui/patterns.tsx; evita la técnica de fallo
  // F103 de WCAG). Los errores siguen saliendo por role="alert".
  const announcement = state.kind === 'loading'
    ? 'Consultando indicadores.'
    : data?.status === 'source_unavailable'
      ? 'Fuente de pedidos no disponible: el módulo de ventas no está activo.'
      : data?.status === 'empty'
        ? 'No se crearon pedidos en el período consultado.'
        : data?.status === 'with_data'
          ? `Indicadores consultados: ${showCount(data.i01CreatedOrders)} pedidos creados.`
          : fromLink && state.kind === 'idle'
            ? 'Filtros cargados desde un enlace. Pulsa «Consultar período» para ver los indicadores.'
            : '';
  const shareAnnouncement = share.kind === 'copied' ? 'Enlace copiado.' : '';

  return (
    <PageContainer title="Reportes de pedidos" description="Pedidos creados en el intervalo elegido, según su estado actual. Incluye pedidos dados de baja.">
      <div className="rp-stack">
        <div className="sr-only" role="status" aria-live="polite" aria-atomic="true">{announcement}</div>
        <div className="sr-only" role="status" aria-live="polite" aria-atomic="true">{shareAnnouncement}</div>
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
          <label className="rp-field">
            <span>Comparación opcional</span>
            <span><input type="checkbox" checked={comparePrevious} onChange={(e) => {
              setComparePrevious(e.target.checked);
              invalidateConsultation();
            }} /> Comparar los pedidos creados con el período anterior de igual duración</span>
          </label>
          <button className="ui-button" type="submit" disabled={state.kind === 'loading'}>Consultar período</button>
          <button className="ui-button" type="button" onClick={() => void copyShareLink()}>Copiar enlace a estos filtros</button>
        </form>
        {link.kind === 'invalid' && (
          <p className="rp-warning" role="alert">{link.message} No se aplicó ningún filtro del enlace; indica el período a mano.</p>
        )}
        {fromLink && state.kind === 'idle' && (
          <p className="rp-note">Filtros cargados desde un enlace. Revísalos y pulsa «Consultar período» para ver los indicadores.</p>
        )}
        {share.kind === 'copied' && <p className="rp-note">Enlace copiado. Guarda solo las fechas y la zona; quien lo abra necesita acceso de administración.</p>}
        {share.kind === 'manual' && (
          <label className="rp-field">
            No se pudo copiar automáticamente. Copia este enlace:
            <input className="ui-input" type="text" readOnly value={share.url} onFocus={(e) => e.currentTarget.select()} />
          </label>
        )}
        {share.kind === 'incomplete' && <p className="rp-warning" role="alert">{share.message}</p>}
        {inputError && <p className="rp-warning" role="alert">{inputError}</p>}
        {state.kind === 'idle' && !fromLink && <p className="rp-note">Selecciona un período y consulta sus indicadores. No se asigna uno por defecto.</p>}
        {state.kind === 'error' && state.failure.kind !== 'silent' && <FailureAlert failure={state.failure} />}
        <div className="rp-stack" aria-busy={state.kind === 'loading'}>
        {state.kind === 'loading' && <p className="rp-note">Consultando indicadores…</p>}
        {data?.status === 'source_unavailable' && (
          <section className="rp-panel">
            <h2>Fuente de pedidos no disponible</h2>
            <p>El módulo de ventas no está activo en esta instalación, así que estos indicadores no se pueden medir. Actívalo para ver los pedidos.</p>
          </section>
        )}
        {data && data.status !== 'source_unavailable' && (
          <>
            <p className="rp-note">Observado: {data.observedAtUtc ? showDate(data.observedAtUtc, zone) : 'Sin instante informado'} · Zona: {zone}</p>
            {data.status === 'empty' && <p>No se crearon pedidos en este período. Los recuentos son cero.</p>}
            <div className="rp-kpis">
              <section className="rp-panel"><h2>Pedidos creados</h2><strong>{showCount(data.i01CreatedOrders)}</strong></section>
              <section className="rp-panel"><h2>Actualmente vencidos</h2><strong>{showCount(data.i04CurrentlyExpiredOrders)}</strong></section>
            </div>
            {previous && (
              <section className="rp-panel">
                <h2>Pedidos creados · comparación con el período anterior</h2>
                <p className="rp-note">Anterior: [{showDate(previous.start, zone)}, {showDate(previous.end, zone)}). Ambos períodos tienen igual duración.</p>
                {delta === null ? (
                  <p role="status">La comparación no está disponible; los indicadores del período consultado siguen siendo válidos.</p>
                ) : (
                  <>
                    <p>Pedidos creados antes: <strong>{previousCount?.toLocaleString('es-PE')}</strong></p>
                    <p>Diferencia: <strong>{delta > 0 ? '+' : ''}{delta.toLocaleString('es-PE')}</strong> pedidos</p>
                    <p className="rp-note">{previousCount === 0
                      ? 'Sin variación porcentual calculable porque el período anterior tuvo cero pedidos.'
                      : `Variación: ${delta > 0 ? '+' : ''}${((delta / previousCount!) * 100).toLocaleString('es-PE', { maximumFractionDigits: 1 })} %`}</p>
                  </>
                )}
              </section>
            )}
            {distribution && <OrderDistribution {...distribution} />}
            {/* D10: la clave cambia con período, observación y zona, así que un
                cambio de la consulta principal desmonta la comparación y anula
                sus peticiones en vuelo (D4). Solo se monta con cohorte válida. */}
            <OrderPeriodComparison
              key={`${data.startInclusive}|${data.endExclusive}|${data.observedAtUtc}|${zone}`}
              baseline={data}
              zone={zone}
              // D12: B del enlace o lo último escrito; solo rellena, no consulta.
              initialPeriod={comparisonInput}
              onPeriodInput={(bStart, bEnd) => {
                setComparisonInput({ start: bStart, end: bEnd });
                setShare({ kind: 'none' });
              }}
            />
            <p className="rp-note">Cohorte: [{showDate(data.startInclusive, zone)}, {showDate(data.endExclusive, zone)}). Los vencidos son los que tienen estado actual «Vencido»; no es una evaluación histórica del plazo.</p>
          </>
        )}
        </div>
      </div>
    </PageContainer>
  );
}
