import { Link } from 'react-router-dom';
import { Badge } from '../../../shared/ui';
import {
  formatDate,
  formatMoney,
  orderStatus,
  workStatus,
} from '../format';
import type {
  CustomerOrderSummary,
  CustomerTrackingSummary,
} from '../services/contracts';

export function OrderCard({ order }: { order: CustomerOrderSummary }) {
  const status = orderStatus(order.status);

  return (
    <article className="portal-record" aria-labelledby={`pedido-${order.orderCode}`}>
      <div className="portal-record__heading">
        <h3 id={`pedido-${order.orderCode}`}>{order.orderCode}</h3>
        <Badge tone={status.tone}>{status.label}</Badge>
      </div>
      <dl className="portal-record__facts">
        <div>
          <dt>Fecha</dt>
          <dd>{formatDate(order.placedAt)}</dd>
        </div>
        <div>
          <dt>Total</dt>
          <dd>{formatMoney(order.totalAmount)}</dd>
        </div>
        <div>
          <dt>Contenido</dt>
          <dd>{order.lineCount === 1 ? '1 producto' : `${order.lineCount} productos`}</dd>
        </div>
      </dl>
      <Link className="portal-action-link" to={`/mi-portal/pedidos/${encodeURIComponent(order.orderCode)}`}>
        Ver pedido
      </Link>
    </article>
  );
}

export function WorkCard({ work }: { work: CustomerTrackingSummary }) {
  const status = workStatus(work.currentStatus);

  return (
    <article className="portal-record" aria-labelledby={`trabajo-${work.visibleCode}`}>
      <div className="portal-record__heading">
        <h3 id={`trabajo-${work.visibleCode}`}>{work.visibleCode}</h3>
        <Badge tone={status.tone}>{status.label}</Badge>
      </div>
      <dl className="portal-record__facts">
        <div>
          <dt>Recibido</dt>
          <dd>{formatDate(work.receivedAt)}</dd>
        </div>
        <div>
          <dt>Fecha prometida</dt>
          <dd>{work.promisedAt ? formatDate(work.promisedAt) : 'Todavía sin fecha'}</dd>
        </div>
      </dl>
      <Link className="portal-action-link" to={`/mi-portal/trabajos/${encodeURIComponent(work.visibleCode)}`}>
        Ver trabajo
      </Link>
    </article>
  );
}
