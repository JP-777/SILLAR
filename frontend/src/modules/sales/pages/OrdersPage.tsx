import { useCallback, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { useSession } from '../../../session';
import { Button, EmptyState } from '../../../shared/ui';
import { amount, date, orderStates } from '../format';
import { sales } from '../service';
import { useSalesResource } from '../useSalesResource';

export function OrdersPage() {
  const [status, setStatus] = useState(''); const [attention, setAttention] = useState(false);
  const [code, setCode] = useState(''); const { user } = useSession(); const navigate = useNavigate();
  const load = useCallback((signal: AbortSignal) => sales.list(status, attention, signal), [status, attention]);
  const { state, reload } = useSalesResource(`${user?.id}:${status}:${attention}`, load);
  function find(e: FormEvent) { e.preventDefault(); if (code.trim()) navigate(`/admin/pedidos/${encodeURIComponent(code.trim())}`); }
  return <PageContainer title="Pedidos en línea" description="Confirma los pagos y coordina el recojo en tienda.">
    <div className="sa-stack">
      <div className="sa-panel sa-row">
        <label>Estado<select className="ui-input" value={status} onChange={e => setStatus(e.target.value)}>
          <option value="">Todos</option>{Object.entries(orderStates).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></label>
        <label><input type="checkbox" checked={attention} onChange={e => setAttention(e.target.checked)} /> Requieren atención</label>
        <Button variant="secondary" onClick={reload}>Actualizar pedidos</Button>
        <form className="sa-row" onSubmit={find}><label>Código de pedido<input className="ui-input" value={code} onChange={e => setCode(e.target.value)} maxLength={120} placeholder="2026-0147" required /></label><Button type="submit" variant="secondary">Abrir pedido</Button></form>
      </div>
      <p className="sa-note">Los pedidos vencidos siguen abiertos. Revisa su atención antes de confirmar un pago tardío. No se reservan existencias.</p>
      {state.status === 'loading' && <p role="status">Cargando pedidos…</p>}
      {state.status === 'error' && <div role="alert"><p>{state.message}</p><Button onClick={reload}>Reintentar consulta</Button></div>}
      {state.status === 'ready' && (state.data.length === 0 ? <EmptyState title="No hay pedidos para estos filtros" description="Los pedidos que recibas aparecerán aquí." /> : <>
        <p>Hasta 100 pedidos más recientes. Para otro pedido, introduce su código.</p>
        <ul className="sa-list">{state.data.map(p => <li className="sa-panel" key={p.orderCode}>
          <div className="sa-row"><Link to={`/admin/pedidos/${encodeURIComponent(p.orderCode)}`}>Pedido {p.orderCode}</Link><strong>{orderStates[p.status] ?? 'Estado sin reconocer'}</strong></div>
          <p>{p.customerName} · {amount(p.totalAmount, p.currencyCode)}</p><p className="sa-note">Recibido: {date(p.placedAt)} · Plazo de pago: {date(p.paymentDueAt)}</p>
          {p.needsAttention && <p className="sa-attention">Requiere atención del personal</p>}
        </li>)}</ul>
      </>)}
    </div>
  </PageContainer>;
}
