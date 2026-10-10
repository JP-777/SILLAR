import { Alert, Button, EmptyState, Spinner } from '../../../shared/ui';

export function PortalLoading({ label }: { label: string }) {
  return (
    <section className="portal-loading" aria-busy="true" aria-label={label}>
      <Spinner size="lg" label={label} />
      <div className="portal-loading__placeholder" aria-hidden="true" />
      <div className="portal-loading__placeholder portal-loading__placeholder--short" aria-hidden="true" />
    </section>
  );
}

export function PortalRetryState({
  message,
  onRetry,
}: {
  message: string;
  onRetry: () => void;
}) {
  return (
    <div className="portal-state">
      <Alert tone="danger" title="No pudimos cargar esta información">
        {message}
      </Alert>
      <Button variant="secondary" onClick={onRetry}>
        Reintentar
      </Button>
    </div>
  );
}

export function PortalUnavailableState({ message }: { message: string }) {
  return (
    <EmptyState
      title="Contenido no disponible"
      description={message}
    />
  );
}

export function PortalNotFoundState({
  message,
  action,
}: {
  message: string;
  action: React.ReactNode;
}) {
  return (
    <EmptyState
      title={message}
      description="Revisa el enlace o vuelve al listado."
      action={action}
    />
  );
}

export function PortalAccessState() {
  return (
    <Alert tone="warning" title="Tu sesión ya no está disponible">
      Vuelve a entrar para consultar información de tu cuenta.
    </Alert>
  );
}
