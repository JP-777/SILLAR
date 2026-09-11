import { useCallback } from 'react';
import { settingsService } from '../modules/core/services/settings';
import { useResource } from '../shared/hooks/useResource';
import { useCapability } from '../capabilities/useCapability';
import { useSession } from '../session';
import { PageContainer } from '../layout/PageContainer';
import { Alert, Badge, Card } from '../shared/ui';

/**
 * Inicio del panel.
 *
 * Resumen operativo del panel: muestra módulos activos y la configuración
 * que todavía requiere atención.
 */
export function HomePage() {
  const { modules, version } = useCapability();
  const { user } = useSession();
  const loadSettings = useCallback(() => settingsService.list(), []);
  const { state: settingsState } = useResource(
    loadSettings,
    'cargar la configuración del inicio',
  );

  const pending =
    settingsState.status === 'ready'
      ? settingsState.data.filter((setting) => setting.needsSetup)
      : [];

  return (
    <PageContainer
      title={`Hola, ${user?.fullName.split(' ')[0] ?? ''}`}
      description="Este es el estado de tu instalación."
    >
      {pending.length > 0 && (
        <Alert tone="warning" title="Falta configurar el negocio">
          {pending.length} dato{pending.length === 1 ? '' : 's'} sin completar:{' '}
          {pending
            .map((setting) => setting.description?.trim() || setting.key)
            .join(', ')}. Se configuran desde la pantalla de configuración.
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

      <Card title="Administración disponible">
        <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
          Desde este panel puedes gestionar módulos, usuarios, configuración, auditoría y archivos
          según tu rol y los módulos activos.
        </p>
      </Card>
    </PageContainer>
  );
}
