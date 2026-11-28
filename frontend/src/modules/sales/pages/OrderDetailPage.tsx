import { useCallback, useRef, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { useModulePermissions, useSession } from '../../../session';
import { describe } from '../../../shared/errors/messages';
import { isApiError } from '../../../shared/http/errors';
import { Button } from '../../../shared/ui';
import { amount, date, orderStates } from '../format';
import { sales, type ManualPayment, type OrderDetail } from '../service';
import { useSalesResource } from '../useSalesResource';

export function OrderDetailPage() {
  const { code = '' } = useParams(); const { user } = useSession();
  const load = useCallback((signal: AbortSignal) => sales.detail(code, signal), [code]);
  const { state, reload } = useSalesResource(`${user?.id}:${code}`, load);
  return <PageContainer title={`Pedido ${code}`} description="Recojo en tienda. Las existencias se comprueban durante la atención.">
    <Link to="/admin/pedidos">Volver a pedidos</Link>
    {state.status === 'loading' && <p role="status">Cargando el pedido…</p>}
    {state.status === 'error' && <div role="alert"><p>{state.message}</p><Button onClick={reload}>Reintentar consulta</Button></div>}
    {state.status === 'ready' && <Detail key={`${user?.id}:${code}`} data={state.data} reload={reload} />}
  </PageContainer>;
}
function Detail({ data, reload }: { data: OrderDetail; reload: () => void }) {
  const p = data.order; const { hasOperation } = useModulePermissions();
  const [reason, setReason] = useState(''); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false);
  const flight = useRef(false);
  async function change(action: string) {
    if (flight.current) return; flight.current = true; setBusy(true); setMessage('');
    try { await sales.change(p.orderCode, action, p.version, reason.trim() || null); reload(); }
    catch (error) { setMessage(describe(error, 'cambiar el estado').message || 'No se pudo confirmar el cambio. Actualiza el pedido antes de continuar.'); }
    finally { flight.current = false; setBusy(false); }
  }
  const paid = data.payments.length > 0 && p.currencyCode !== null && data.payments.every(x => x.currencyCode === p.currencyCode)
    ? data.payments.reduce((sum, x) => sum + x.amount, 0) : null;
  return <div className="sa-stack">
    <section className="sa-panel"><h2>{orderStates[p.status] ?? 'Estado sin reconocer'}</h2>
      {p.needsAttention && <p role="status" className="sa-attention">Coordina la atención con el cliente. Un pago registrado no garantiza existencias.</p>}
      <p>{p.customerName} · {data.customerEmail}{data.customerPhone ? ` · ${data.customerPhone}` : ''}</p>
      <p>Total: <strong>{amount(p.totalAmount, p.currencyCode)}</strong></p><p>Plazo de pago: {date(p.paymentDueAt)} (America/Lima)</p>
      <ul>{data.lines.map((line, i) => <li key={i}>{line.quantity} × {line.productName}{line.variantValue ? ` · ${line.variantValue}` : ''} — {amount(line.subtotal, p.currencyCode)}</li>)}</ul>
    </section>
    {hasOperation('sales', 'pay') && !['cancelled', 'delivered'].includes(p.status) && <PaymentForm data={data} reload={reload} />}
    {hasOperation('sales', 'manage') && <section className="sa-panel sa-stack"><h2>Atención del pedido</h2>
      {message && <p role="alert">{message}</p>}
      <div className="sa-row">
        {['pending_payment', 'payment_to_verify', 'expired'].includes(p.status) && paid !== null && paid >= p.totalAmount && <Button disabled={busy} onClick={() => void change('prepare')}>Confirmar atención y preparar</Button>}
        {p.status === 'preparing' && <Button disabled={busy} onClick={() => void change('ready')}>Marcar listo para recoger</Button>}
        {p.status === 'ready_for_pickup' && <Button disabled={busy} onClick={() => void change('deliver')}>Registrar entrega</Button>}
        <Button disabled={busy} variant="secondary" onClick={reload}>Actualizar detalle</Button>
      </div>
      {!['cancelled', 'delivered'].includes(p.status) && <form className="sa-stack" onSubmit={e => { e.preventDefault(); void change('cancel'); }}>
        <label>Motivo de cancelación<textarea className="ui-input" maxLength={1000} required value={reason} disabled={busy} onChange={e => setReason(e.target.value)} /></label>
        <p className="sa-note">Cancelar conserva los pagos. Coordina cualquier devolución con el cliente.</p>
        <Button variant="danger" type="submit" disabled={busy || !reason.trim()}>Cancelar pedido con este motivo</Button>
      </form>}
    </section>}
    <section className="sa-panel"><h2>Pagos registrados</h2>
      {data.payments.length === 0 ? <p>No hay pagos registrados.</p> : <ul className="sa-list">{data.payments.map((payment, i) => <li key={i}>
        <strong>{amount(payment.amount, payment.currencyCode)} · {payment.method === 'yape' ? 'Yape' : 'Efectivo'}</strong>
        <p>{date(payment.registeredAt)} · Registró {payment.registeredBy}{payment.wasLate ? ' · Pago tardío' : ''}</p>
        {payment.reference && <p>Referencia: {payment.reference}</p>}
        {payment.canFulfill === false && <p>Al cobrar quedó pendiente coordinar la atención.</p>}
      </li>)}</ul>}
    </section>
    <section className="sa-panel"><h2>Historial</h2><ol>{data.history.map((h, i) => <li key={i}>
      <strong>{orderStates[h.toStatus] ?? 'Estado sin reconocer'}</strong> · {date(h.changedAt)}
      {h.changedBy && <span> · {h.changedBy}</span>}{h.reason && <p>{h.reason}</p>}
    </li>)}</ol></section>
  </div>;
}
function PaymentForm({ data, reload }: { data: OrderDetail; reload: () => void }) {
  const p = data.order;
  const known = p.currencyCode !== null && data.payments.every(x => x.currencyCode === p.currencyCode);
  const balance = known ? Math.max(0, p.totalAmount - data.payments.reduce((sum, x) => sum + x.amount, 0)) : null;
  const [value, setValue] = useState(balance?.toFixed(2) ?? ''); const [method, setMethod] = useState('yape');
  const [reference, setReference] = useState(''); const [canFulfill, setCanFulfill] = useState(false);
  const [attempt, setAttempt] = useState<ManualPayment | null>(null); const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState(''); const flight = useRef(false);
  async function submit(e: FormEvent) {
    e.preventDefault(); if (flight.current) return; flight.current = true; setBusy(true); setMessage('');
    const input = attempt ?? { requestId: crypto.randomUUID(), method, amount: Number(value), reference: reference.trim() || null, canFulfill };
    setAttempt(input);
    try { await sales.pay(p.orderCode, input); reload(); }
    catch (error) {
      if (isApiError(error, 'Conflict')) { setAttempt(null); setMessage(describe(error, 'registrar el pago').message); }
      else setMessage('No se pudo confirmar el resultado. Reintenta este mismo pago: conservará su identidad para evitar duplicarlo.');
    } finally { flight.current = false; setBusy(false); }
  }
  return <section className="sa-panel sa-stack"><h2>Registrar un pago verificado</h2>
    <p>Comprueba el abono en tu cuenta antes de registrarlo. El aviso del cliente no acredita un pago.</p>
    {!known ? <p role="alert">Hay importes sin moneda acreditada. Revisa el historial antes de cobrar.</p>
      : balance === 0 && data.payments.length > 0 ? <p>El total del pedido ya está registrado.</p> : <form onSubmit={e => void submit(e)} className="sa-stack">
        <fieldset disabled={busy || attempt !== null}><legend>Datos del pago</legend><div className="sa-row">
          <label>Medio<select className="ui-input" value={method} onChange={e => setMethod(e.target.value)}><option value="yape">Yape</option><option value="efectivo">Efectivo</option></select></label>
          <label>Importe ({p.currencyCode})<input className="ui-input" type="number" step="0.01" min={p.totalAmount === 0 ? 0 : 0.01} max={balance ?? undefined} required value={value} onChange={e => setValue(e.target.value)} /></label>
          <label>Referencia de la operación<input className="ui-input" maxLength={200} value={reference} onChange={e => setReference(e.target.value)} /></label>
        </div><label><input type="checkbox" checked={canFulfill} onChange={e => setCanFulfill(e.target.checked)} /> Comprobé que podemos atender este pedido</label></fieldset>
        <p className="sa-note">Si no puedes atenderlo todavía, registra el pago y coordina con el cliente. Se conservará como pendiente de atención.</p>
        {message && <p role="alert">{message}</p>}<Button type="submit" loading={busy}>{attempt ? 'Reintentar el mismo pago' : p.totalAmount === 0 ? 'Confirmar pedido sin importe' : 'Registrar pago verificado'}</Button>
      </form>}
  </section>;
}
