import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { Card, EmptyState } from '../../../shared/ui';
import { OrderCard, WorkCard } from '../components/PortalCards';
import { PortalPageHeader, PortalShell } from '../components/PortalShell';
import {
  PortalAccessState,
  PortalLoading,
  PortalRetryState,
  PortalUnavailableState,
} from '../components/PortalStates';
import { useCustomerResource } from '../hooks/useCustomerResource';
import type {
  CustomerOrderSummary,
  CustomerTrackingSummary,
  PortalSection,
} from '../services/contracts';
import { portalService } from '../services/portal';

export function PortalOverviewPage() {
  useDocumentTitle('Mi portal');
  const load = useCallback((signal: AbortSignal) => portalService.overview(3, signal), []);
  const { state, reload } = useCustomerResource('overview-3', load);

  return (
    <PortalShell>
      <PortalPageHeader
        title="Mi portal"
        description="Consulta tus pedidos, trabajos y datos de cuenta desde un solo lugar."
      />

      {state.status === 'loading' && <PortalLoading label="Cargando Mi portal" />}
      {(state.status === 'unauthorized' || state.status === 'forbidden') && <PortalAccessState />}
      {(state.status === 'error' || state.status === 'not-found' || state.status === 'unavailable') && (
        <PortalRetryState
          message="No pudimos cargar Mi portal. Reintenta."
          onRetry={reload}
        />
      )}

      {state.status === 'ready' && (
        <>
          <Card title="Tu cuenta">
            <dl className="portal-profile">
              <div>
                <dt>Nombre</dt>
                <dd>{state.data.profile.fullName}</dd>
              </div>
              <div>
                <dt>Correo</dt>
                <dd>{state.data.profile.email}</dd>
              </div>
              {state.data.profile.phone && (
                <div>
                  <dt>Teléfono</dt>
                  <dd>{state.data.profile.phone}</dd>
                </div>
              )}
            </dl>
            <Link className="portal-action-link" to="/mi-cuenta">
              Administrar mi cuenta
            </Link>
          </Card>

          <div className="portal-overview-grid">
            <OverviewOrders section={state.data.orders} onRetry={reload} />
            <OverviewWork section={state.data.work} onRetry={reload} />
          </div>
        </>
      )}
    </PortalShell>
  );
}

function OverviewOrders({
  section,
  onRetry,
}: {
  section: PortalSection<CustomerOrderSummary>;
  onRetry: () => void;
}) {
  return (
    <Card title="Pedidos recientes">
      {section.state === 'available' && (
        <>
          <div className="portal-list">
            {section.items.slice(0, 3).map((order) => (
              <OrderCard key={order.orderCode} order={order} />
            ))}
          </div>
          <Link className="portal-action-link" to="/mi-portal/pedidos">
            Ver todos los pedidos
          </Link>
        </>
      )}
      {section.state === 'empty' && (
        <EmptyState
          title="Todavía no tienes pedidos"
          description="Cuando realices uno, aparecerá aquí."
        />
      )}
      {section.state === 'unavailable' && (
        <PortalUnavailableState message="Los pedidos no están disponibles en esta instalación." />
      )}
      {section.state === 'error' && (
        <PortalRetryState message="No pudimos cargar tus pedidos. Reintenta." onRetry={onRetry} />
      )}
    </Card>
  );
}

function OverviewWork({
  section,
  onRetry,
}: {
  section: PortalSection<CustomerTrackingSummary>;
  onRetry: () => void;
}) {
  return (
    <Card title="Trabajos recientes">
      {section.state === 'available' && (
        <>
          <div className="portal-list">
            {section.items.slice(0, 3).map((work) => (
              <WorkCard key={work.visibleCode} work={work} />
            ))}
          </div>
          <Link className="portal-action-link" to="/mi-portal/trabajos">
            Ver todos los trabajos
          </Link>
        </>
      )}
      {section.state === 'empty' && (
        <EmptyState
          title="Todavía no tienes trabajos"
          description="Los servicios recibidos aparecerán aquí."
        />
      )}
      {section.state === 'unavailable' && (
        <PortalUnavailableState message="El seguimiento de trabajos no está disponible en esta instalación." />
      )}
      {section.state === 'error' && (
        <PortalRetryState message="No pudimos cargar tus trabajos. Reintenta." onRetry={onRetry} />
      )}
    </Card>
  );
}
