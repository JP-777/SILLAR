import { useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useCapability } from '../../../capabilities/useCapability';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { Badge, Card } from '../../../shared/ui';
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
  formatDateTime,
  formatQuantity,
  workStatus,
} from '../format';
import {
  ProviderUnavailableError,
  useCustomerResource,
} from '../hooks/useCustomerResource';
import { portalService } from '../services/portal';

export function PortalWorkDetailPage() {
  const { visibleCode = '' } = useParams();
  const { has } = useCapability();
  const trackingAvailable = has('tracking');
  useDocumentTitle(visibleCode ? `Trabajo ${visibleCode}` : 'Trabajo');

  const load = useCallback(
    (signal: AbortSignal) => {
      if (!trackingAvailable) {
        return Promise.reject(new ProviderUnavailableError());
      }

      return portalService.work(visibleCode, signal);
    },
    [trackingAvailable, visibleCode],
  );
  const { state, reload } = useCustomerResource(`work:${visibleCode}`, load);

  return (
    <PortalShell>
      {state.status === 'loading' && <PortalLoading label="Cargando el trabajo" />}
      {state.status === 'unavailable' && (
        <>
          <PortalPageHeader title="Trabajo" backTo="/mi-portal/trabajos" backLabel="Volver a trabajos" />
          <PortalUnavailableState message="El seguimiento de trabajos no está disponible en esta instalación." />
        </>
      )}
      {state.status === 'not-found' && (
        <>
          <PortalPageHeader title="Trabajo" backTo="/mi-portal/trabajos" backLabel="Volver a trabajos" />
          <PortalNotFoundState
            message="No encontramos ese trabajo."
            action={<Link to="/mi-portal/trabajos">Volver a mis trabajos</Link>}
          />
        </>
      )}
      {(state.status === 'unauthorized' || state.status === 'forbidden') && <PortalAccessState />}
      {state.status === 'error' && (
        <>
          <PortalPageHeader title="Trabajo" backTo="/mi-portal/trabajos" backLabel="Volver a trabajos" />
          <PortalRetryState message="No pudimos cargar este trabajo. Reintenta." onRetry={reload} />
        </>
      )}

      {state.status === 'ready' && <WorkDetailContent detail={state.data} />}
    </PortalShell>
  );
}

function WorkDetailContent({ detail }: { detail: Awaited<ReturnType<typeof portalService.work>> }) {
  const status = workStatus(detail.currentStatus);

  return (
    <>
      <PortalPageHeader
        title={`Trabajo ${detail.visibleCode}`}
        description="Avance público del servicio recibido."
        backTo="/mi-portal/trabajos"
        backLabel="Volver a trabajos"
      />

      <Card title="Resumen">
        <div className="portal-detail-status">
          <Badge tone={status.tone}>{status.label}</Badge>
        </div>
        <dl className="portal-detail-grid">
          <div>
            <dt>Recibido</dt>
            <dd>{formatDate(detail.receivedAt)}</dd>
          </div>
          <div>
            <dt>Fecha prometida</dt>
            <dd>{detail.promisedAt ? formatDate(detail.promisedAt) : 'Todavía sin fecha'}</dd>
          </div>
          <div>
            <dt>Última actualización</dt>
            <dd>{formatDateTime(detail.lastStatusChangedAt)}</dd>
          </div>
        </dl>
      </Card>

      <Card title="Servicios">
        <div className="portal-lines">
          {detail.items.map((item, index) => (
            <article className="portal-line" key={`${item.serviceName}-${index}`}>
              <div>
                <h3>{item.serviceName}</h3>
                {item.publicDescription && <p>{item.publicDescription}</p>}
              </div>
              <p>
                {formatQuantity(item.quantity)} {item.saleUnit ?? 'unidades'}
              </p>
            </article>
          ))}
        </div>
      </Card>
    </>
  );
}
