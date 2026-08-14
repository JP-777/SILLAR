import { useCapability } from '../capabilities/useCapability';
import { useSession } from '../session';
import { PageContainer } from '../layout/PageContainer';
import { usePublicSettings } from './usePublicSettings';
import { Alert, Badge, Card } from '../shared/ui';

/**
 * Inicio del panel.
 *
 * Provisional: la sustituirá la entrega de pantallas de CORE. Muestra lo mínimo
 * útil —qué está activo y qué falta configurar— para que la instalación recién
 * hecha no reciba a nadie con una pantalla vacía.
 */
export function HomePage() {
  const { modules, version } = useCapability();
  const { user } = useSession();
  const settings = usePublicSettings();

  const pending = Object.entries(settings.all)
    .filter(([, value]) => value === 'PENDIENTE_DEFINIR')
    .map(([key]) => key);

  return (
    <PageContainer
      title={`Hola, ${user?.fullName.split(' ')[0] ?? ''}`}
      description="Este es el estado de tu instalación."
    >
      {pending.length > 0 && (
        <Alert tone="warning" title="Falta configurar el negocio">
          {pending.length} dato{pending.length === 1 ? '' : 's'} sin completar:{' '}
          {pending.join(', ')}. Se configuran desde la pantalla de configuración, que llega en la
          siguiente entrega.
        </Alert>
      )}

      <Card title="Módulos activos" subtitle={`SILLAR ${version}`}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 'var(--s2)' }}>
          {modules.map((module) => (
            <Badge key={module.code} tone="success">
              {module.code} {module.version}
            </Badge>
          ))}
        </div>
      </Card>

      <Card title="Qué viene ahora">
        <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
          Las pantallas de módulos, usuarios, configuración, auditoría y archivos llegan en la
          entrega de administración de CORE. El API ya las sirve; falta la interfaz.
        </p>
      </Card>
    </PageContainer>
  );
}
