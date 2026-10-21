import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { isApiError } from '../../../shared/http/errors';
import { http } from '../../../shared/http/client';
import { Badge, Button } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert } from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import { SERVICE_ORDER_LABELS, mayAssign, mayEdit, mayRelease, permittedTargets, updateFromDetail } from '../rules';
import { checkUpdate, newServiceOrderLine, removeUnsavedLine, moveLine, withDraftKeys, toUpdateRequest, resolvePromisedAt, unpublishedNewServiceLines, type EditDraft } from '../edit-rules';
import type { PublicServiceChoice } from '../service';
import { asDateTimeLocal, displayAmount, displayDate, statusTone, toIsoOrNull } from '../format';
import { serviceOrdersService } from '../service';
import type { ServiceOrderDetail, ServiceOrderStatus } from '../contracts';
import { activeQuery, makeQueryKey, type KeyedQueryState } from '../query-state';
import { beginFlight, endFlight } from '../flight-guard';
import '../service-orders.css';

const keys: ServiceOrderStatus[] = ['received', 'in_progress', 'ready', 'completed', 'cancelled'];

export function ServiceOrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const location = useLocation();
  const { user, hasRole } = useSession();
  // Cada respuesta queda atada al identificador y a la revisión que la originó.
  // Un 403, 404 o resultado de otra orden nunca se muestra durante la navegación.
  const [revision, setRevision] = useState(0);
  const detailKey = makeQueryKey(id ?? '', revision);
  const [storedDetail, setStoredDetail] = useState<KeyedQueryState<ServiceOrderDetail, Failure>>({ key: '', status: 'loading' });
  const activeDetail = activeQuery(storedDetail, detailKey);
  const order = activeDetail.status === 'ready' ? activeDetail.data ?? null : null;
  const loading = activeDetail.status === 'loading';
  const missing = activeDetail.status === 'missing';
  const [actionForbiddenKey, setActionForbiddenKey] = useState<string | null>(null);
  const forbidden = activeDetail.status === 'forbidden' || actionForbiddenKey === detailKey;
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState<EditDraft | null>(null);
  const [promisedAtDraft, setPromisedAtDraft] = useState('');
  const [originalPromisedLocal, setOriginalPromisedLocal] = useState('');
  const nextDraftId = useRef(0);
  // An async mutation belongs to the route on which it was started.
  const activeRouteKey = useRef(detailKey);
  activeRouteKey.current = detailKey;
  const showcaseRequestId = useRef(0);
  const [storedShowcase, setStoredShowcase] = useState<{ key: string; data: PublicServiceChoice[]; error: string | null } | null>(null);
  const showcase = storedShowcase?.key === detailKey ? storedShowcase.data : [];
  const showcaseError = storedShowcase?.key === detailKey ? storedShowcase.error : null;
  const [target, setTarget] = useState<ServiceOrderStatus | null>(null);
  const [pending, setPending] = useState<'take' | 'unassign' | null>(null);
  const [busy, setBusy] = useState(false);
  const flight = useRef<string | null>(null);
  const [actionFailure, setActionFailure] = useState<{ key: string; value: Failure } | null>(null);
  const failure = activeDetail.status === 'error'
    ? activeDetail.failure ?? null
    : actionFailure?.key === detailKey ? actionFailure.value : null;

  useEffect(() => {
    let live = true;
    ++showcaseRequestId.current;
    setEditing(false); setForm(null); setPromisedAtDraft(''); setOriginalPromisedLocal(''); setTarget(null); setPending(null);
    setStoredShowcase(null); setActionFailure(null); setActionForbiddenKey(null);
    setBusy(false);
    setStoredDetail({ key: detailKey, status: 'loading' });
    if (!id) {
      setStoredDetail({ key: detailKey, status: 'missing' });
      return () => { live = false; };
    }
    void serviceOrdersService.detail(id).then(value => {
      if (live) setStoredDetail({ key: detailKey, status: 'ready', data: value });
    }).catch(error => {
      if (!live) return;
      if (isApiError(error, 'NotFound')) setStoredDetail({ key: detailKey, status: 'missing' });
      else if (isApiError(error, 'Forbidden')) setStoredDetail({ key: detailKey, status: 'forbidden' });
      else setStoredDetail({ key: detailKey, status: 'error', failure: describe(error, 'cargar la orden') });
    });
    return () => { live = false; };
  }, [id, detailKey]);

  function reload() {
    // La revisión invalida sincrónicamente todo dato anterior, incluso antes de useEffect.
    ++showcaseRequestId.current;
    setEditing(false); setForm(null); setPromisedAtDraft(''); setOriginalPromisedLocal(''); setTarget(null); setPending(null);
    setStoredShowcase(null); setActionFailure(null); setActionForbiddenKey(null);
    setRevision(x => x + 1);
  }

  async function execute(action: () => Promise<unknown>, verb: string) {
    const requestKey = detailKey;
    // Bloqueo sincrónico: dos clics antes del rerender no duplican escrituras.
    if (!beginFlight(flight, requestKey)) return;
    setBusy(true); setActionFailure(null);
    try {
      await action();
      if (activeRouteKey.current === requestKey) reload();
    } catch (error) {
      if (activeRouteKey.current !== requestKey) return;
      // Si el usuario ya navegó a otra orden, una respuesta antigua no debe afectar su pantalla.
      if (isApiError(error, 'Forbidden')) setActionForbiddenKey(requestKey);
      else {
        const described = describe(error, verb);
        setActionFailure({ key: requestKey, value: isApiError(error, 'Conflict')
          ? { ...described, message: `${described.message} La orden pudo cambiar. Actualiza antes de volver a confirmar.` }
          : described });
      }
      setTarget(null); setPending(null);
    } finally {
      endFlight(flight, requestKey);
      if (activeRouteKey.current === requestKey) setBusy(false);
    }
  }

  function beginEdit() {
    if (!order || !mayEdit(order.currentStatus)) return;
    const requestKey = detailKey;
    const requestId = ++showcaseRequestId.current;
    const payload = updateFromDetail(order);
    setForm(withDraftKeys(payload));
    const initialPromise = asDateTimeLocal(order.promisedAt);
    setPromisedAtDraft(initialPromise);
    setOriginalPromisedLocal(initialPromise);
    setEditing(true); setActionFailure(null);
    setStoredShowcase({ key: requestKey, data: [], error: null });
    // Respuestas anteriores del selector no deben contaminar una orden distinta.
    void http.get<PublicServiceChoice[]>('/services').then(data => {
      if (requestId === showcaseRequestId.current)
        setStoredShowcase({ key: requestKey, data, error: null });
    }).catch(() => {
      if (requestId === showcaseRequestId.current)
        setStoredShowcase({ key: requestKey, data: [], error: 'No se pudo consultar la vitrina para añadir líneas. Las actuales siguen siendo editables.' });
    });
  }

  function save(event: FormEvent) {
    event.preventDefault();
    if (!id || !form || !order || !mayEdit(order.currentStatus)) return;
    const promised = resolvePromisedAt(order.promisedAt, originalPromisedLocal, promisedAtDraft, toIsoOrNull);
    const validation = promisedAtDraft.trim() && promised === null
      ? ['La fecha de compromiso es inválida.'] : [];
    const payload = toUpdateRequest(form, promised);
    validation.push(...checkUpdate(payload, order));
    // La vitrina no gobierna las líneas históricas: solo las incorporaciones nuevas.
    const hasNewLines = form.items.some(line => line.serviceOrderItemId === null);
    if (hasNewLines && showcaseError) validation.push('La vitrina no está disponible para añadir líneas. Reabre la edición y actualiza los servicios.');
    if (hasNewLines && !showcaseError && showcase.length === 0) validation.push('Consulta los servicios publicados antes de agregar líneas.');
    if (hasNewLines && showcase.length > 0) {
      const missing = unpublishedNewServiceLines(payload.items, new Set(showcase.map(service => service.id)));
      if (missing.length) validation.push(`Líneas ${missing.join(', ')}: servicio no disponible en la vitrina publicada.`);
    }
    if (validation.length > 0) {
      setActionFailure({ key: detailKey, value: { kind: 'validation', message: validation.join(' '), fieldErrors: null, blockedBy: null } });
      return;
    }
    void execute(() => serviceOrdersService.update(id, payload), 'actualizar la orden');
  }

  const back = `/admin/ordenes-servicio${location.search}`;
  if (forbidden) return <ForbiddenPage minimum="editor" />;
  if (loading) return <PageContainer title="Orden de servicio"><p role="status">Cargando detalle de la orden…</p></PageContainer>;
  if (missing) return <PageContainer title="Orden no encontrada"><p>Esa orden ya no está disponible.</p><Link to={back}>Volver a la bandeja</Link></PageContainer>;
  if (!order) return <PageContainer title="No se pudo cargar la orden"><FailureAlert failure={failure} /><Button onClick={reload}>Volver a intentar</Button></PageContainer>;
  const allowed = permittedTargets(order.currentStatus, hasRole('admin'));

  return <PageContainer title={`Orden ${order.visibleCode}`} description="Datos históricos y movimientos de la orden de servicio.">
    <div className="so5-stack">
      <div className="so5-row"><Link to={back}>← Volver a órdenes</Link><Button variant="secondary" onClick={reload} disabled={busy}>Actualizar</Button></div>
      <FailureAlert failure={failure} />
      <section className="so5-grid3">
        <div className="so5-panel"><small>Estado</small><div><Badge tone={statusTone(order.currentStatus)}>{SERVICE_ORDER_LABELS[order.currentStatus]}</Badge></div></div>
        <div className="so5-panel"><small>Contacto</small><h2>{order.customerName}</h2><p>{order.customerPhone || 'Sin teléfono'}</p></div>
        <div className="so5-panel"><small>Responsable</small><h2>{order.currentAssignee?.displayName ?? 'Sin responsable'}</h2><p>{order.currentAssignee ? 'Asignación registrada' : 'Pendiente de asignación'}</p></div>
      </section>
      <section className="so5-panel"><div className="so5-row"><h2>Datos y servicios históricos</h2>
        {mayEdit(order.currentStatus) && <Button variant="secondary" disabled={busy} onClick={beginEdit}>Editar orden</Button>}</div>
        <dl className="so5-meta"><div><dt>Recibida</dt><dd>{displayDate(order.receivedAt)}</dd></div>
          <div><dt>Compromiso</dt><dd>{displayDate(order.promisedAt)}</dd></div>
          <div><dt>Creada por</dt><dd>{order.createdBy.displayName}</dd></div>
          <div><dt>Total acordado</dt><dd>{displayAmount(order.totalAmount)}</dd></div></dl>
        <p>{order.receivedNotes || 'Sin notas de recepción'}</p>
        <div className="so5-table-scroll"><table className="so5-table"><thead><tr><th>Servicio congelado</th><th>Cantidad</th><th>Vitrina</th><th>Acordado</th><th>Detalle</th></tr></thead>
          <tbody>{order.items.map(line => <tr key={line.serviceOrderItemId}>
            <td>{line.serviceName}</td><td>{line.quantity}</td><td>{displayAmount(line.showcasePrice)}</td>
            <td>{displayAmount(line.agreedUnitPrice)}</td><td>{line.requestedDetails || 'Sin observaciones'}</td>
          </tr>)}</tbody></table></div>
      </section>
      {editing && form && <section className="so5-panel" aria-label="Editar orden">
        <h2>Editar contacto y líneas — A4</h2>
        <p className="so5-hint">No se modifican snapshots de servicio. Se envía el sello de concurrencia original.</p>
        <form className="so5-stack" onSubmit={save} aria-busy={busy}>
          <fieldset className="so5-fieldset" disabled={busy}>
          {form.customerId !== null && <div className="so5-row"><p className="so5-hint">Contacto vinculado a CRM. El servidor volverá a obtener su fotografía al guardar; los datos manuales no se editan mientras siga vinculado.</p>
            <Button type="button" variant="secondary" disabled={busy} onClick={() => setForm({ ...form, customerId: null })}>Usar contacto manual</Button></div>}
          <div className="so5-grid3">
            <label>Contacto<input className="ui-input" disabled={form.customerId !== null} value={form.customerName ?? ''} onChange={e => setForm({ ...form, customerName: e.target.value })} /></label>
            <label>Teléfono<input className="ui-input" disabled={form.customerId !== null} value={form.customerPhone ?? ''} onChange={e => setForm({ ...form, customerPhone: e.target.value || null })} /></label>
            <label>Correo<input className="ui-input" type="email" disabled={form.customerId !== null} value={form.customerEmail ?? ''} onChange={e => setForm({ ...form, customerEmail: e.target.value || null })} /></label>
          </div>
          <label>Compromiso<input className="ui-input" type="datetime-local" value={promisedAtDraft} onChange={e => setPromisedAtDraft(e.target.value)} /></label>
          <label>Notas de recepción<textarea className="ui-input so5-textarea" value={form.receivedNotes ?? ''} onChange={e => setForm({ ...form, receivedNotes: e.target.value || null })} /></label>
          {showcaseError && <p role="alert">{showcaseError}</p>}
          <p className="so5-hint">Los servicios ya confirmados son históricos: pueden editarse o reordenarse, pero nunca borrarse ni sustituirse por otro servicio. Solo las líneas nuevas pueden quitarse antes de guardar.</p>
          {form.items.map((item, index) => {
            const old = order.items.find(existing => existing.serviceOrderItemId === item.serviceOrderItemId);
            return <div className="so5-line" key={item.uiKey}>
              <div className="so5-row"><strong>{old?.serviceName ?? `Línea nueva ${index + 1}`}</strong>
                <Button type="button" variant="secondary" disabled={busy || index === 0}
                  onClick={() => setForm({ ...form, items: moveLine(form.items, index, -1) })}>Subir</Button>
                <Button type="button" variant="secondary" disabled={busy || index >= form.items.length - 1}
                  onClick={() => setForm({ ...form, items: moveLine(form.items, index, 1) })}>Bajar</Button>
                {item.serviceOrderItemId === null && <Button type="button" variant="secondary" disabled={busy}
                  onClick={() => setForm({ ...form, items: removeUnsavedLine(form.items, index) })}>Quitar línea nueva</Button>}
              </div>
              {item.serviceOrderItemId === null && <label>Servicio publicado *
                <select className="ui-input" required value={item.serviceId ?? ''} onChange={e => setForm({ ...form, items: form.items.map((v, j) => j === index ? { ...v, serviceId: Number(e.target.value) || null } : v) })}>
                  <option value="">Seleccionar servicio</option>{showcase.map(service => <option key={service.id} value={service.id}>{service.name}</option>)}
                </select></label>}
              <div className="so5-grid3">
                <label>Cantidad<input className="ui-input" type="number" step="0.001" min="0.001" value={item.quantity}
                  onChange={e => setForm({ ...form, items: form.items.map((v, j) => j === index ? { ...v, quantity: Number(e.target.value) } : v) })} /></label>
                <label>Precio acordado (vacío = pendiente)<input className="ui-input" type="number" step="0.01" min="0" value={item.agreedUnitPrice ?? ''}
                  onChange={e => setForm({ ...form, items: form.items.map((v, j) => j === index ? { ...v, agreedUnitPrice: e.target.value === '' ? null : Number(e.target.value) } : v) })} /></label>
                <label>Características<input className="ui-input" value={item.requestedDetails}
                  onChange={e => setForm({ ...form, items: form.items.map((v, j) => j === index ? { ...v, requestedDetails: e.target.value } : v) })} /></label>
              </div>
            </div>;
          })}
          <Button type="button" variant="secondary" disabled={busy || showcase.length === 0}
            onClick={() => setForm({ ...form, items: [...form.items, { ...newServiceOrderLine(0), uiKey: `new-${nextDraftId.current++}` }] })}>Agregar otra línea</Button>
          <div className="so5-row"><Button type="submit" disabled={busy} loading={busy}>Guardar cambios</Button>
            <Button type="button" variant="secondary" onClick={() => setEditing(false)} disabled={busy}>Cancelar</Button></div>
          </fieldset>
        </form>
      </section>}
      <section className="so5-panel"><h2>Asignación — A5</h2>
        <p>{order.currentAssignee ? `Responsable actual: ${order.currentAssignee.displayName}` : 'Sin responsable actual'}</p>
        <div className="so5-row">
          <Button variant="secondary" disabled={busy || !user || !mayAssign(order, user.id)}
            onClick={() => setPending('take')}>Asignarme</Button>
          <Button variant="secondary" disabled={busy || !user || !mayRelease(order, user.id)}
            onClick={() => setPending('unassign')}>Liberar mi asignación</Button>
        </div>
        <h3>Historial de asignaciones</h3>
        <ol>{order.assignmentHistory.map(ev => <li key={ev.assignmentEventId}>{ev.assignee.displayName} — {ev.action} — {displayDate(ev.occurredAt)}</li>)}</ol>
        {order.assignmentHistory.length === 0 && <p>Sin movimientos de asignación.</p>}
      </section>
      <section className="so5-panel"><h2>Cambiar estado — A6</h2>
        <p>Solo las transiciones permitidas aparecen aquí. El servidor verifica estado actual y permisos.</p>
        <div className="so5-row">{keys.filter(k => allowed.includes(k)).map(k => <Button key={k} variant="secondary" disabled={busy}
          onClick={() => setTarget(k)}>{k === 'cancelled' ? 'Cancelar orden' : `Pasar a ${SERVICE_ORDER_LABELS[k]}`}</Button>)}</div>
        {allowed.length === 0 && <p>Estado terminal: no admite más transiciones.</p>}
      </section>
      <section className="so5-panel"><h2>Historial autoritativo M05b</h2>
        <ol>{order.statusHistory.map(ev => <li key={ev.statusHistoryId}>
          {ev.fromStatus ? SERVICE_ORDER_LABELS[ev.fromStatus] : 'Inicio'} → {SERVICE_ORDER_LABELS[ev.toStatus]} — {displayDate(ev.occurredAt)}
          {ev.performedBy ? ` · ${ev.performedBy.displayName}` : ''}</li>)}</ol>
      </section>
      <ConfirmDialog open={pending !== null} title={pending === 'take' ? 'Confirmar asignación' : 'Confirmar liberación'}
        confirmLabel={pending === 'take' ? 'Asignarme' : 'Liberar'} busy={busy}
        onCancel={() => setPending(null)} onConfirm={() => {
          if (!id || !pending) return;
          const action = pending;
          void execute(() => action === 'take' ? serviceOrdersService.take(id, order.updatedAt)
            : serviceOrdersService.unassign(id, order.updatedAt), 'modificar la asignación');
        }}><p>{order.visibleCode}: se comprobará que la orden no cambió desde que la abriste.</p></ConfirmDialog>
      <ConfirmDialog open={target !== null} title="Confirmar cambio de estado"
        confirmLabel={target === 'cancelled' ? 'Cancelar orden' : 'Confirmar transición'} danger={target === 'cancelled'} busy={busy}
        onCancel={() => setTarget(null)} onConfirm={() => {
          if (id && target && permittedTargets(order.currentStatus, hasRole('admin')).includes(target)) {
            void execute(() => serviceOrdersService.transition(id, order.currentStatus, target), 'cambiar el estado');
          }
        }}><p>{order.visibleCode}: {SERVICE_ORDER_LABELS[order.currentStatus]} → {target ? SERVICE_ORDER_LABELS[target] : ''}.</p>
          {target === 'cancelled' && <p>Cancelar es terminal y requiere rol administrador.</p>}
      </ConfirmDialog>
    </div>
  </PageContainer>;
}
