import { useCallback } from 'react';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { EmptyState } from '../../../shared/ui';
import { WorkCard } from '../components/PortalCards';
import { PortalPageHeader, PortalShell } from '../components/PortalShell';
import {
  PortalAccessState,
  PortalLoading,
  PortalRetryState,
  PortalUnavailableState,
} from '../components/PortalStates';
import { useCustomerResource } from '../hooks/useCustomerResource';
import { portalService } from '../services/portal';

export function PortalWorkPage() {
  useDocumentTitle('Mis trabajos');
  const load = useCallback((signal: AbortSignal) => portalService.overview(20, signal), []);
  const { state, reload } = useCustomerResource('work-20', load);

  return (
    <PortalShell>
      <PortalPageHeader
        title="Mis trabajos"
        description="Consulta el avance de los servicios que dejaste con nosotros."
      />

      {state.status === 'loading' && <PortalLoading label="Cargando tus trabajos" />}
      {(state.status === 'unauthorized' || state.status === 'forbidden') && <PortalAccessState />}
      {(state.status === 'error' || state.status === 'not-found' || state.status === 'unavailable') && (
        <PortalRetryState message="No pudimos cargar tus trabajos. Reintenta." onRetry={reload} />
      )}

      {state.status === 'ready' && state.data.work.state === 'available' && (
        <div className="portal-list portal-list--page">
          {state.data.work.items.map((work) => (
            <WorkCard key={work.visibleCode} work={work} />
          ))}
        </div>
      )}
      {state.status === 'ready' && state.data.work.state === 'empty' && (
        <EmptyState
          title="Todavía no tienes trabajos"
          description="Los servicios recibidos aparecerán aquí."
        />
      )}
      {state.status === 'ready' && state.data.work.state === 'unavailable' && (
        <PortalUnavailableState message="El seguimiento de trabajos no está disponible en esta instalación." />
      )}
      {state.status === 'ready' && state.data.work.state === 'error' && (
        <PortalRetryState message="No pudimos cargar tus trabajos. Reintenta." onRetry={reload} />
      )}
    </PortalShell>
  );
}
