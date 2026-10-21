import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { isApiError } from '../../../shared/http/errors';
import { http } from '../../../shared/http/client';
import { useCapability } from '../../../capabilities/useCapability';
import { useSession } from '../../../session';
import { Button } from '../../../shared/ui';
import { FailureAlert } from '../../../shared/ui/patterns';
import { checkCreate, unpublishedServiceLines } from '../rules';
import { moveLine } from '../edit-rules';
import { toIsoOrNull } from '../format';
import { serviceOrdersService } from '../service';
import type { CreateServiceOrderRequest } from '../contracts';
import { listAvailableCrmCustomers, type OrderCustomerOption } from '../optional-crm';
import { sameCreateAttempt, mayEditAfterFailure } from '../attempt-rules';
import { beginFlight, endFlight } from '../flight-guard';
import '../service-orders.css';

type Line = { key: number; serviceId: string; quantity: string; requestedDetails: string; agreedUnitPrice: string };
type PublishedService = { id: number; name: string; price: number | null };
const newLine = (key: number): Line => ({ key, serviceId: '', quantity: '1', requestedDetails: '', agreedUnitPrice: '' });

export function ServiceOrderCreatePage() {
  const navigate = useNavigate();
  const { has } = useCapability();
  const { hasRole } = useSession();
  const canChooseCrm = has('crm') && hasRole('admin');
  const nextKey = useRef(1);
  const crmRequestId = useRef(0);
  // La clave persiste durante reintentos del mismo borrador: nunca generar otra en el catch.
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID());
  const [customerName, setCustomerName] = useState('');
  const [customerId, setCustomerId] = useState<string | null>(null);
  const [crmQuery, setCrmQuery] = useState('');
  const [crmChoices, setCrmChoices] = useState<OrderCustomerOption[]>([]);
  const [crmError, setCrmError] = useState<string | null>(null);
  const [customerPhone, setCustomerPhone] = useState('');
  const [customerEmail, setCustomerEmail] = useState('');
  const [receivedNotes, setReceivedNotes] = useState('');
  const [promisedAt, setPromisedAt] = useState('');
  const [assignToMe, setAssignToMe] = useState(false);
  const [lines, setLines] = useState<Line[]>([newLine(0)]);
  const [services, setServices] = useState<PublishedService[]>([]);
  const [servicesFailure, setServicesFailure] = useState(false);
  const [servicesLoading, setServicesLoading] = useState(true);
  const [servicesRevision, setServicesRevision] = useState(0);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [busy, setBusy] = useState(false);
  const flight = useRef<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [sentAttempt, setSentAttempt] = useState<CreateServiceOrderRequest | null>(null);
  const [uncertain, setUncertain] = useState(false);

  useEffect(() => {
    let live = true;
    setServicesLoading(true); setServicesFailure(false); setServices([]);
    // No importa código de otro módulo: solo consume el contrato HTTP público de M05a.
    void http.get<PublishedService[]>('/services').then(s => { if (live) setServices(s); })
      .catch(() => { if (live) setServicesFailure(true); })
      .finally(() => { if (live) setServicesLoading(false); });
    return () => { live = false; };
  }, [servicesRevision]);

  useEffect(() => {
    if (!canChooseCrm) {
      crmRequestId.current++;
      setCustomerId(null); setCrmChoices([]); setCrmError(null);
    }
  }, [canChooseCrm]);

  async function findCrm() {
    if (!canChooseCrm) return;
    const requestId = ++crmRequestId.current;
    setCrmError(null); setCrmChoices([]); setCustomerId(null);
    try {
      const options = await listAvailableCrmCustomers(crmQuery);
      // Respuestas fuera de orden no deben volver a ofrecer clientes de otra búsqueda.
      if (requestId === crmRequestId.current) setCrmChoices(options.filter(item => item.isActive));
    } catch {
      if (requestId === crmRequestId.current) setCrmError('CRM no respondió. Sigue disponible el contacto manual.');
    }
  }

  function replaceLine(key: number, field: keyof Omit<Line, 'key'>, value: string) {
    setLines(items => items.map(line => line.key === key ? { ...line, [field]: value } : line));
  }

  async function submit(event: FormEvent) {
    event.preventDefault(); setFailure(null);
    const request: CreateServiceOrderRequest = {
      idempotencyKey, customerId, customerName: customerId ? null : customerName.trim() || null,
      customerPhone: customerId ? null : customerPhone.trim() || null, customerEmail: customerId ? null : customerEmail.trim() || null,
      receivedNotes: receivedNotes.trim() || null, receivedAt: null,
      promisedAt: toIsoOrNull(promisedAt), assignToMe,
      items: lines.map(line => ({
        serviceId: Number(line.serviceId), requestedDetails: line.requestedDetails.trim(),
        quantity: Number(line.quantity), agreedUnitPrice: line.agreedUnitPrice.trim() === '' ? null : Number(line.agreedUnitPrice),
      })),
    };
    const errors = checkCreate(request);
    if (promisedAt.trim() && request.promisedAt === null) errors.push('La fecha de compromiso es inválida.');
    // Un servicio que salió de la vitrina entre carga y alta no puede usarse silenciosamente.
    // Tampoco se omite la comprobación si la lista está vacía o recargándose.
    const published = new Set(services.map(service => service.id));
    if (servicesLoading || servicesFailure) errors.push('Consulta los servicios publicados antes de guardar.');
    else {
      const missing = unpublishedServiceLines(request.items, published);
      if (missing.length) errors.push(`Actualiza las líneas ${missing.join(', ')}: servicio no disponible en la vitrina.`);
    }
    if (errors.length) {
      setFailure({ kind: 'validation', message: errors.join(' '), fieldErrors: null, blockedBy: null });
      return;
    }
    if (sentAttempt !== null && !sameCreateAttempt(sentAttempt, request)) {
      setFailure({ kind: 'inline', message: 'El envío anterior tiene resultado incierto. Solo puedes reenviar el mismo borrador con la misma clave; no cambies sus datos.', fieldErrors: null, blockedBy: null });
      return;
    }
    // La referencia se marca antes del primer await, sin depender del render.
    if (!beginFlight(flight, 'create')) return;
    setBusy(true); setSentAttempt(request);
    try {
      const saved = await serviceOrdersService.create(request);
      setIdempotencyKey(crypto.randomUUID());
      navigate(`/admin/ordenes-servicio/${encodeURIComponent(saved.serviceOrderId)}`, { replace: true });
    } catch (error) {
      if (isApiError(error, 'Forbidden')) setForbidden(true);
      else setFailure(describe(error, 'crear la orden'));
      if (isApiError(error) && mayEditAfterFailure(error.kind)) {
        // El servidor rechazó explícitamente el alta: puede corregirse con una nueva clave.
        setSentAttempt(null); setIdempotencyKey(crypto.randomUUID()); setUncertain(false);
      } else setUncertain(true);
      // No se cambian ni las líneas ni idempotencyKey en 409, timeout o respuesta incierta.
    } finally { endFlight(flight, 'create'); setBusy(false); }
  }

  if (forbidden) return <ForbiddenPage minimum="editor" />;
  return <PageContainer title="Nueva orden de servicio" description="Recibe una orden con una o varias líneas y conserva los datos acordados.">
    <form className="so5-stack" onSubmit={e => void submit(e)} noValidate>
      {uncertain && <p role="alert">No hay confirmación de creación. No edites ni dupliques el borrador: reintenta exactamente el mismo envío para recuperar el resultado.</p>}
      <fieldset disabled={busy || uncertain} className="so5-fieldset">
      <FailureAlert failure={failure} />
      <section className="so5-panel"><h2>Contacto de recepción</h2>
        {canChooseCrm && <div className="so5-stack"><label>Buscar cliente CRM (opcional, solo admin+)
          <input className="ui-input" value={crmQuery} onChange={e => {
            // Una búsqueda anterior no puede asignar cliente tras cambiar el texto.
            ++crmRequestId.current; setCrmQuery(e.target.value);
            setCrmChoices([]); setCustomerId(null); setCrmError(null);
          }} placeholder="Nombre o correo" /></label>
          <Button type="button" variant="secondary" onClick={() => void findCrm()}>Buscar en CRM</Button>
          {crmError && <p role="alert">{crmError}</p>}
          <label>Cliente existente
            <select className="ui-input" value={customerId ?? ''} onChange={e => setCustomerId(e.target.value || null)}>
              <option value="">Contacto manual</option>
              {crmChoices.map(option => <option key={option.customerId} value={option.customerId}>{option.fullName} — {option.email}</option>)}
            </select></label>
          {customerId && <p className="so5-hint">El backend resolverá y congelará el contacto de CRM; los campos manuales se ignoran.</p>}
        </div>}
        <div className="so5-grid3">
          <label>Nombre del contacto *<input required className="ui-input" value={customerName} disabled={customerId !== null} onChange={e => setCustomerName(e.target.value)} /></label>
          <label>Teléfono<input className="ui-input" type="tel" value={customerPhone} disabled={customerId !== null} onChange={e => setCustomerPhone(e.target.value)} /></label>
          <label>Correo<input className="ui-input" type="email" value={customerEmail} disabled={customerId !== null} onChange={e => setCustomerEmail(e.target.value)} /></label>
        </div>
        <label>Fecha comprometida<input className="ui-input" type="datetime-local" value={promisedAt} onChange={e => setPromisedAt(e.target.value)} /></label>
        <label>Notas<textarea className="ui-input so5-textarea" value={receivedNotes} onChange={e => setReceivedNotes(e.target.value)} /></label>
        <p className="so5-hint">El contacto manual requiere al menos teléfono o correo. El contacto manual funciona sin CRM. Cuando CRM esté activo, usuarios admin+ también pueden vincular un cliente existente.</p>
      </section>
      <section className="so5-panel"><h2>Servicios solicitados</h2>
        {servicesLoading && <p role="status">Consultando servicios publicados…</p>}
        {servicesFailure && <div role="alert"><p>No se pudieron cargar los servicios publicados. La solicitud no se enviará mientras no estén disponibles.</p>
          <Button type="button" variant="secondary" onClick={() => setServicesRevision(n => n + 1)} disabled={busy}>Volver a consultar servicios</Button></div>}
        {lines.map((line, index) => <div key={line.key} className="so5-line">
          <div className="so5-row"><strong>Línea {index + 1}</strong>
            <div className="so5-row">
              <Button type="button" variant="secondary" disabled={busy || index === 0}
                onClick={() => setLines(all => moveLine(all, index, -1))}>Subir</Button>
              <Button type="button" variant="secondary" disabled={busy || index >= lines.length - 1}
                onClick={() => setLines(all => moveLine(all, index, 1))}>Bajar</Button>
              <Button type="button" variant="secondary" disabled={busy || lines.length === 1}
                onClick={() => setLines(all => all.filter(candidate => candidate.key !== line.key))}>Quitar</Button>
            </div></div>
          <div className="so5-grid3">
            <label>Servicio publicado *<select className="ui-input" value={line.serviceId}
              onChange={e => replaceLine(line.key, 'serviceId', e.target.value)} required>
              <option value="">Seleccionar</option>{services.map(s => <option value={s.id} key={s.id}>{s.name}</option>)}
            </select></label>
            <label>Cantidad *<input className="ui-input" type="number" min="0.001" step="0.001" value={line.quantity}
              onChange={e => replaceLine(line.key, 'quantity', e.target.value)} /></label>
            <label>Precio acordado (vacío = pendiente)<input className="ui-input" type="number" min="0" step="0.01" value={line.agreedUnitPrice}
              onChange={e => replaceLine(line.key, 'agreedUnitPrice', e.target.value)} /></label>
          </div>
          <label>Características solicitadas<textarea className="ui-input so5-textarea" value={line.requestedDetails}
            onChange={e => replaceLine(line.key, 'requestedDetails', e.target.value)} /></label>
        </div>)}
        <Button type="button" variant="secondary" disabled={busy} onClick={() => setLines(items => [...items, newLine(nextKey.current++)])}>Agregar línea</Button>
      </section>
      <section className="so5-panel"><label className="so5-inline"><input type="checkbox" checked={assignToMe} onChange={e => setAssignToMe(e.target.checked)} /> Asignarme al crear la orden</label>
        <div className="so5-row"><Button type="submit" loading={busy} disabled={servicesLoading || servicesFailure || uncertain}>Crear orden</Button>
          <Link to="/admin/ordenes-servicio">Cancelar</Link></div>
      </section>
      </fieldset>
      {uncertain && <div className="so5-panel"><Button type="button" variant="secondary" disabled={busy} onClick={() => {
        // Repetir el mismo request, sin rearmar el cuerpo ni regenerar idempotencia.
        if (!sentAttempt) return;
        if (!beginFlight(flight, 'create')) return;
        setBusy(true);
        void serviceOrdersService.create(sentAttempt).then(saved => {
          setUncertain(false); navigate(`/admin/ordenes-servicio/${encodeURIComponent(saved.serviceOrderId)}`, { replace: true });
        }).catch(error => {
          if (isApiError(error, 'Forbidden')) { setForbidden(true); return; }
          setFailure(describe(error, 'reconciliar el envío'));
          // Un rechazo definitivo del servidor descarta esta clave: se puede corregir la solicitud.
          if (isApiError(error) && mayEditAfterFailure(error.kind)) {
            setSentAttempt(null);
            setIdempotencyKey(crypto.randomUUID());
            setUncertain(false);
          }
          // 409, pérdida de red y 500 permanecen inciertos: no regenerar la clave.
        }).finally(() => { endFlight(flight, 'create'); setBusy(false); });
      }}>Reintentar el mismo envío</Button></div>}
    </form>
  </PageContainer>;
}
