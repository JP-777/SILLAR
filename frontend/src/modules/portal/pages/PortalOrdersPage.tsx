import { useCallback } from 'react';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { EmptyState } from '../../../shared/ui';
import { OrderCard } from '../components/PortalCards';
import { PortalPageHeader, PortalShell } from '../components/PortalShell';
import {
  PortalAccessState,
  PortalLoading,
  PortalRetryState,
  PortalUnavailableState,
} from '../components/PortalStates';
import { useCustomerResource } from '../hooks/useCustomerResource';
import { portalService } from '../services/portal';

export function PortalOrdersPage() {
  useDocumentTitle('Mis pedidos');
  const load = useCallback((signal: AbortSignal) => portalService.overview(20, signal), []);
  const { state, reload } = useCustomerResource('orders-20', load);

  return (
    <PortalShell>
      <PortalPageHeader
        title="Mis pedidos"
        description="Revisa tus pedidos recientes y abre su detalle."
      />

      {state.status === 'loading' && <PortalLoading label="Cargando tus pedidos" />}
      {(state.status === 'unauthorized' || state.status === 'forbidden') && <PortalAccessState />}
      {(state.status === 'error' || state.status === 'not-found' || state.status === 'unavailable') && (
        <PortalRetryState message="No pudimos cargar tus pedidos. Reintenta." onRetry={reload} />
      )}

      {state.status === 'ready' && state.data.orders.state === 'available' && (
        <div className="portal-list portal-list--page">
          {state.data.orders.items.map((order) => (
            <OrderCard key={order.orderCode} order={order} />
          ))}
        </div>
      )}
      {state.status === 'ready' && state.data.orders.state === 'empty' && (
        <EmptyState
          title="Todavía no tienes pedidos"
          description="Cuando realices uno, aparecerá aquí."
        />
      )}
      {state.status === 'ready' && state.data.orders.state === 'unavailable' && (
        <PortalUnavailableState message="Los pedidos no están disponibles en esta instalación." />
      )}
      {state.status === 'ready' && state.data.orders.state === 'error' && (
        <PortalRetryState message="No pudimos cargar tus pedidos. Reintenta." onRetry={reload} />
      )}
    </PortalShell>
  );
}
