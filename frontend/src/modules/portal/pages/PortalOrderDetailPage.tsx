import { useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useCapability } from '../../../capabilities/useCapability';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { Alert, Badge, Card } from '../../../shared/ui';
import { PortalPageHeader, PortalShell } from '../components/PortalShell';
import {
  PortalAccessState,
  PortalLoading,
  PortalNotFoundState,
  PortalRetryState,
  PortalUnavailableState,
} from '../components/PortalStates';
import {
  formatDate,
  formatMoney,
  orderStatus,
} from '../format';
import {
  ProviderUnavailableError,
  useCustomerResource,
} from '../hooks/useCustomerResource';
import { portalService } from '../services/portal';

export function PortalOrderDetailPage() {
  const { orderCode = '' } = useParams();
  const { has } = useCapability();
  const salesAvailable = has('sales');
  useDocumentTitle(orderCode ? `Pedido ${orderCode}` : 'Pedido');

  const load = useCallback(
    (signal: AbortSignal) => {
      if (!salesAvailable) {
        return Promise.reject(new ProviderUnavailableError());
      }

      return portalService.order(orderCode, signal);
    },
    [orderCode, salesAvailable],
  );
  const { state, reload } = useCustomerResource(`order:${orderCode}`, load);

  return (
    <PortalShell>
      {state.status === 'loading' && <PortalLoading label="Cargando el pedido" />}
      {state.status === 'unavailable' && (
        <>
          <PortalPageHeader title="Pedido" backTo="/mi-portal/pedidos" backLabel="Volver a pedidos" />
          <PortalUnavailableState message="Los pedidos no están disponibles en esta instalación." />
        </>
      )}
      {state.status === 'not-found' && (
        <>
          <PortalPageHeader title="Pedido" backTo="/mi-portal/pedidos" backLabel="Volver a pedidos" />
          <PortalNotFoundState
            message="No encontramos ese pedido."
            action={<Link to="/mi-portal/pedidos">Volver a mis pedidos</Link>}
          />
        </>
      )}
      {(state.status === 'unauthorized' || state.status === 'forbidden') && <PortalAccessState />}
      {state.status === 'error' && (
        <>
          <PortalPageHeader title="Pedido" backTo="/mi-portal/pedidos" backLabel="Volver a pedidos" />
          <PortalRetryState message="No pudimos cargar este pedido. Reintenta." onRetry={reload} />
        </>
      )}

      {state.status === 'ready' && <OrderDetailContent detail={state.data} />}
    </PortalShell>
  );
}

function OrderDetailContent({ detail }: { detail: Awaited<ReturnType<typeof portalService.order>> }) {
  const status = orderStatus(detail.status);

  return (
    <>
      <PortalPageHeader
        title={`Pedido ${detail.orderCode}`}
        description="Detalle del pedido y precios registrados al realizarlo."
        backTo="/mi-portal/pedidos"
        backLabel="Volver a pedidos"
      />

      {detail.status === 'expired' && (
        <Alert tone="warning" title="Plazo de pago vencido">
          El plazo para pagar venció. El pedido no está cancelado por ese motivo.
        </Alert>
      )}

      <Card title="Resumen">
        <div className="portal-detail-status">
          <Badge tone={status.tone}>{status.label}</Badge>
        </div>
        <dl className="portal-detail-grid">
          <div>
            <dt>Realizado</dt>
            <dd>{formatDate(detail.placedAt)}</dd>
          </div>
          <div>
            <dt>Plazo de pago</dt>
            <dd>{formatDate(detail.paymentDueAt)}</dd>
          </div>
          <div>
            <dt>Total</dt>
            <dd>{formatMoney(detail.totalAmount)}</dd>
          </div>
        </dl>
      </Card>

      <Card title="Productos">
        <div className="portal-lines">
          {detail.lines.map((line, index) => (
            <article className="portal-line" key={`${line.productName}-${index}`}>
              <div>
                <h3>{line.productName}</h3>
                {line.variantValue && <p>{line.variantValue}</p>}
                <p>
                  {line.quantity} {line.saleUnit ?? (line.quantity === 1 ? 'unidad' : 'unidades')}
                  {' × '}{formatMoney(line.unitPrice)}
                </p>
              </div>
              <strong>{formatMoney(line.subtotal)}</strong>
            </article>
          ))}
        </div>
      </Card>
    </>
  );
}
