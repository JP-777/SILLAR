import { useCallback, useMemo, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { describe } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Card, Spinner } from '../../../shared/ui';
import { ConfirmDialog, Toasts, useToasts } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { useSession } from '../../../session';
import { EmailTestPanel } from '../components/EmailTestPanel';
import { SettingRow } from '../components/SettingRow';
import {
  groupSettings,
  isMailSettingKey,
  settingsService,
  type Setting,
} from '../services/settings';
import '../components/settings.css';

/**
 * Configuración del sitio.
 *
 * La pantalla que quita llamadas de teléfono: el número de WhatsApp, el horario
 * y la dirección se cambian aquí y no tocando código.
 */
export function SettingsPage() {
  const load = useCallback(() => settingsService.list(), []);
  const { state, reload } = useResource(load, 'cargar la configuración');
  const { hasRole, user } = useSession();
  const { toasts, show } = useToasts();

  const canPublish = hasRole('super_admin');

  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [pendingVisibility, setPendingVisibility] = useState<Setting | null>(null);

  const settings = state.status === 'ready' ? state.data : [];
  const groups = useMemo(() => groupSettings(settings), [settings]);
  const pendingCount = settings.filter((setting) => setting.needsSetup).length;

  async function save(setting: Setting, value: string, isPublic?: boolean) {
    setBusyKey(setting.key);
    setErrors((current) => ({ ...current, [setting.key]: '' }));

    try {
      await settingsService.update(setting.key, value, isPublic);
      show(`Se guardó «${setting.description ?? setting.key}».`);
      setPendingVisibility(null);
      await reload();
    } catch (error) {
      const failure = describe(error, 'guardar la configuración');

      if (failure.kind !== 'silent') {
        setErrors((current) => ({ ...current, [setting.key]: failure.message }));
      }

      setPendingVisibility(null);
    } finally {
      setBusyKey(null);
    }
  }

  if (state.status === 'forbidden') {
    return <ForbiddenPage minimum="admin" />;
  }

  return (
    <PageContainer
      title="Configuración"
      description="Los datos de tu negocio que aparecen en la web y en el panel."
    >
      {/* Se destacan y se cuentan: una instalación recién hecha debe leerse como
          una lista de tareas, no como un formulario mudo. */}
      {pendingCount > 0 && (
        <Alert tone="warning" title={`Faltan ${pendingCount} datos por completar`}>
          Están marcados abajo. Hasta que los rellenes, la web pública los mostrará en blanco.
        </Alert>
      )}

      {state.status === 'error' && <Alert tone="danger">{state.failure.message}</Alert>}

      {state.status === 'loading' && (
        <div style={{ display: 'flex', justifyContent: 'center', padding: 'var(--s7)' }}>
          <Spinner size="lg" label="Cargando configuración" />
        </div>
      )}

      {state.status === 'ready' &&
        groups.map((group) => (
          <Card key={group.title} title={group.title} subtitle={group.description}>
            <div className="set-group__items">
              {group.items.map((setting) => {
                const mailSetting = isMailSettingKey(setting.key);

                return (
                  <SettingRow
                    key={setting.key}
                    setting={setting}
                    canPublish={canPublish}
                    canEdit={!mailSetting || canPublish}
                    editHint={
                      mailSetting && !canPublish
                        ? 'Editar la configuración de correo exige el rol de administrador principal.'
                        : undefined
                    }
                    visibilityLockedReason={
                      mailSetting
                        ? 'La configuración SMTP siempre es privada.'
                        : undefined
                    }
                    busy={busyKey === setting.key}
                    error={errors[setting.key] || null}
                    onSave={(value) => void save(setting, value)}
                    onTogglePublic={() => setPendingVisibility(setting)}
                  />
                );
              })}
            </div>
          </Card>
        ))}

      {canPublish && <EmailTestPanel defaultRecipient={user?.email ?? ''} />}

      <ConfirmDialog
        open={pendingVisibility !== null}
        title={
          pendingVisibility?.isPublic
            ? 'Dejar de publicar este dato'
            : 'Publicar este dato en la web'
        }
        confirmLabel={pendingVisibility?.isPublic ? 'Dejar de publicar' : 'Publicar dato'}
        danger={!pendingVisibility?.isPublic}
        busy={busyKey !== null}
        onConfirm={() => {
          if (pendingVisibility) {
            void save(pendingVisibility, pendingVisibility.value, !pendingVisibility.isPublic);
          }
        }}
        onCancel={() => setPendingVisibility(null)}
      >
        {pendingVisibility?.isPublic ? (
          <p>
            «{pendingVisibility.description ?? pendingVisibility.key}» dejará de aparecer en la web
            pública. Las páginas que lo muestren lo verán vacío.
          </p>
        ) : (
          <p>
            «{pendingVisibility?.description ?? pendingVisibility?.key}» pasará a ser{' '}
            <strong>visible para cualquiera en la web pública, sin necesidad de entrar</strong>.
          </p>
        )}
        <p>Puedes deshacerlo cuando quieras.</p>
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
