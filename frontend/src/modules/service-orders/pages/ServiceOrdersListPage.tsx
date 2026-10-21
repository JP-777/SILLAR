import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useSession } from '../../../session';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { isApiError } from '../../../shared/http/errors';
import { Badge, Button, EmptyState, NoResults } from '../../../shared/ui';
import { FailureAlert } from '../../../shared/ui/patterns';
import { SERVICE_ORDER_LABELS, SERVICE_ORDER_STATES } from '../rules';
import { asDateTimeLocal, displayAmount, displayDate, statusTone, toIsoOrNull } from '../format';
import { encodeFilters, readFilters } from '../filter-rules';
import { serviceOrdersService } from '../service';
import type { PagedResult, ServiceOrderStatus, ServiceOrderSummary } from '../contracts';
import { activeQuery, makeQueryKey, type KeyedQueryState } from '../query-state';
import '../service-orders.css';

export function ServiceOrdersListPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useSession();
  const filters = useMemo(() => readFilters(location.search), [location.search]);
  const [text, setText] = useState(filters.q ?? '');
  const [status, setStatus] = useState(filters.status ?? '');
  const [onlyMine, setOnlyMine] = useState(Boolean(user && filters.assigneeAdminUserId === user.id));
  const [from, setFrom] = useState(asDateTimeLocal(filters.receivedFrom ?? null));
  const [to, setTo] = useState(asDateTimeLocal(filters.receivedTo ?? null));
  const [storedQuery, setStoredQuery] = useState<KeyedQueryState<PagedResult<ServiceOrderSummary>, Failure>>({ key: '', status: 'loading' });
  const [filterError, setFilterError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const queryKey = makeQueryKey(location.search, revision);
  const current = activeQuery(storedQuery, queryKey);
  const result = current.status === 'ready' ? current.data ?? null : null;
  const loading = current.status === 'loading';
  const forbidden = current.status === 'forbidden';
  const failure = current.status === 'error' ? current.failure ?? null : null;

  useEffect(() => {
    setText(filters.q ?? ''); setStatus(filters.status ?? '');
    setOnlyMine(Boolean(user && filters.assigneeAdminUserId === user.id));
    setFrom(asDateTimeLocal(filters.receivedFrom ?? null));
    setTo(asDateTimeLocal(filters.receivedTo ?? null));
  }, [filters, user]);

  useEffect(() => {
    let alive = true;
    setStoredQuery({ key: queryKey, status: 'loading' });
    void serviceOrdersService.list(filters).then(value => {
      if (alive) setStoredQuery({ key: queryKey, status: 'ready', data: value });
    }).catch(error => {
      if (!alive) return;
      if (isApiError(error, 'Forbidden')) setStoredQuery({ key: queryKey, status: 'forbidden' });
      else setStoredQuery({ key: queryKey, status: 'error', failure: describe(error, 'cargar las órdenes de servicio') });
    });
    return () => { alive = false; };
  }, [filters, queryKey]);

  function submitFilters(event: FormEvent) {
    event.preventDefault(); setFilterError(null);
    const who = onlyMine && user ? user.id : undefined;
    const fromIso = toIsoOrNull(from);
    const toIso = toIsoOrNull(to);
    if ((from && !fromIso) || (to && !toIso) || (fromIso && toIso && Date.parse(fromIso) > Date.parse(toIso))) {
      setFilterError('Revisa las fechas: la fecha inicial debe ser anterior o igual a la final.'); return;
    }
    const search = encodeFilters({
      q: text, status: (status || undefined) as ServiceOrderStatus | undefined,
      assigneeAdminUserId: who, receivedFrom: fromIso ?? undefined,
      receivedTo: toIso ?? undefined, page: 1,
    });
    navigate({ pathname: '/admin/ordenes-servicio', search: search ? `?${search}` : '' });
  }

  function pageTo(page: number) {
    const search = encodeFilters({ ...filters, page });
    navigate({ pathname: '/admin/ordenes-servicio', search: search ? `?${search}` : '' });
  }

  if (forbidden) return <ForbiddenPage minimum="editor" />;
  const filterQuery = encodeFilters(filters);
  const hasFilters = Boolean(filters.q || filters.status || filters.assigneeAdminUserId || filters.receivedFrom || filters.receivedTo);
  // No confundir URL?page=999 con una instalación sin órdenes o filtros sin coincidencias.
  const pageOutOfRange = result !== null && result.items.length === 0
    && result.totalItems > 0 && result.page > result.totalPages;

  return <PageContainer title="Órdenes de servicio" description="Recibe, consulta y gestiona órdenes de servicio."
    actions={<Link className="so5-link" to="/admin/ordenes-servicio/nueva">Nueva orden</Link>}>
    <div className="so5-stack">
      <section className="so5-panel" aria-label="Filtros de órdenes">
        <form onSubmit={submitFilters} className="so5-stack">
          <div className="so5-grid3">
            <label>Cliente o código<input className="ui-input" type="search" maxLength={200} value={text} onChange={e => setText(e.target.value)} /></label>
            <label>Estado<select className="ui-input" value={status} onChange={e => setStatus(e.target.value)}>
              <option value="">Todos</option>
              {SERVICE_ORDER_STATES.map(value => <option key={value} value={value}>{SERVICE_ORDER_LABELS[value]}</option>)}
            </select></label>
            <label className="so5-inline"><input type="checkbox" checked={onlyMine} disabled={!user}
              onChange={e => setOnlyMine(e.target.checked)} /> Solo mis órdenes</label>
            <label>Recibida desde<input className="ui-input" type="datetime-local" value={from} onChange={e => setFrom(e.target.value)} /></label>
            <label>Recibida hasta<input className="ui-input" type="datetime-local" value={to} onChange={e => setTo(e.target.value)} /></label>
          </div>
          {filterError && <p role="alert">{filterError}</p>}
          <div className="so5-row"><Button type="submit">Aplicar filtros</Button>
            <Button type="button" variant="secondary" onClick={() => navigate('/admin/ordenes-servicio')}>Limpiar</Button>
            <Button type="button" variant="secondary" onClick={() => setRevision(i => i + 1)}>Actualizar</Button></div>
        </form>
      </section>
      <FailureAlert failure={failure} />
      <div className="sr-only" role="status" aria-live="polite" aria-atomic="true">
        {loading ? 'Cargando órdenes de servicio.' : ''}
      </div>
      <section className="so5-panel" aria-busy={loading}>
        {loading && <p>Cargando órdenes…</p>}
        {!loading && !failure && pageOutOfRange && <div role="status" className="so5-stack">
          <p>La página solicitada no existe para estos filtros. Hay {result?.totalItems} órdenes en otras páginas.</p>
          <Button variant="secondary" onClick={() => pageTo(1)}>Ir a la primera página</Button>
        </div>}
        {!loading && !failure && !pageOutOfRange && result?.items.length === 0 && (hasFilters
          ? <NoResults title="No hay coincidencias" description="Ajusta la búsqueda o quita los filtros."
              onClear={() => navigate('/admin/ordenes-servicio')} />
          : <EmptyState title="Todavía no hay órdenes" description="Registra una orden para comenzar."
              action={<Link to="/admin/ordenes-servicio/nueva">Nueva orden</Link>} />)}
        {!loading && !failure && result && result.items.length > 0 && <div className="so5-table-scroll so5-desktop-orders">
          <table className="so5-table"><thead><tr><th>Código</th><th>Cliente</th><th>Estado</th><th>Responsable</th><th>Recibida</th><th>Precio</th><th>Acción</th></tr></thead>
            <tbody>{result.items.map(order => <tr key={order.serviceOrderId}>
              <td><strong>{order.visibleCode}</strong></td><td>{order.customerName}</td>
              <td><Badge tone={statusTone(order.currentStatus)}>{SERVICE_ORDER_LABELS[order.currentStatus]}</Badge></td>
              <td>{order.currentAssignee?.displayName ?? 'Sin responsable'}</td>
              <td>{displayDate(order.receivedAt)}</td><td>{displayAmount(order.totalAmount)}</td>
              <td><Link to={`/admin/ordenes-servicio/${encodeURIComponent(order.serviceOrderId)}${filterQuery ? '?' + filterQuery : ''}`}>Abrir</Link></td>
            </tr>)}</tbody></table>
          </div>}
        {!loading && !failure && result && result.items.length > 0 && <div className="so5-mobile-orders" aria-label="Órdenes encontradas">
          {result.items.map(order => <article key={order.serviceOrderId} className="so5-order-card">
            <div className="so5-row"><strong>{order.visibleCode}</strong><Badge tone={statusTone(order.currentStatus)}>{SERVICE_ORDER_LABELS[order.currentStatus]}</Badge></div>
            <h3>{order.customerName}</h3>
            <dl className="so5-order-meta">
              <div><dt>Responsable</dt><dd>{order.currentAssignee?.displayName ?? 'Sin responsable'}</dd></div>
              <div><dt>Recibida</dt><dd>{displayDate(order.receivedAt)}</dd></div>
              <div><dt>Compromiso</dt><dd>{displayDate(order.promisedAt)}</dd></div>
              <div><dt>Precio acordado</dt><dd>{displayAmount(order.totalAmount)}</dd></div>
            </dl>
            <Link className="so5-link" to={`/admin/ordenes-servicio/${encodeURIComponent(order.serviceOrderId)}${filterQuery ? '?' + filterQuery : ''}`}>
              Abrir orden {order.visibleCode}
            </Link>
          </article>)}
        </div>}
        {!loading && !failure && result && result.totalPages > 1 && <nav className="so5-pagination" aria-label="Páginas de órdenes">
          <Button variant="secondary" disabled={filters.page === 1} onClick={() => pageTo((filters.page ?? 1) - 1)}>Anterior</Button>
          <span>Página {result.page} de {result.totalPages}</span>
          <Button variant="secondary" disabled={!result.hasNext} onClick={() => pageTo((filters.page ?? 1) + 1)}>Siguiente</Button>
        </nav>}
        {!loading && failure && <Button variant="secondary" onClick={() => setRevision(i => i + 1)}>Reintentar</Button>}
      </section>
    </div>
  </PageContainer>;
}
