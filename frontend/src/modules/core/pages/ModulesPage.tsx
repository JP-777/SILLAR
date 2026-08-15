import { useCallback, useMemo, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { describe, type Failure } from '../../../shared/errors/messages';
import { connection } from '../../../shared/http/connection';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, EmptyState, Spinner } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Toasts, useToasts } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { ModuleCard } from '../components/ModuleCard';
import { modulesService, type AdminModule } from '../services/modules';
import '../components/modules.css';

/**
 * Pantalla de módulos.
 *
 * La más importante del producto. No es una tabla de configuración: es donde el
 * negocio ve sus bloques y entiende por qué uno está bloqueado.
 */
export function ModulesPage() {
  const load = useCallback(() => modulesService.list(), []);
  const { state, reload } = useResource(load, 'cargar los módulos');

  const { toasts, show } = useToasts();
  const [pending, setPending] = useState<AdminModule | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [highlighted, setHighlighted] = useState<string | null>(null);

  const modules = state.status === 'ready' ? state.data : [];

  const displayNames = useMemo(
    () => new Map(modules.map((module) => [module.code, module.displayName])),
    [modules],
  );

  /** Lleva a la tarjeta del módulo que bloquea, y la resalta al llegar. */
  const jumpTo = useCallback((code: string) => {
    setHighlighted(code);
    document.getElementById(`modulo-${code}`)?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    setTimeout(() => setHighlighted(null), 2500);
  }, []);

  async function confirm() {
    if (!pending) {
      return;
    }

    const activating = !pending.isActive;

    setBusy(true);
    setFailure(null);

    try {
      const result = activating
        ? await modulesService.activate(pending.code)
        : await modulesService.deactivate(pending.code);

      setPending(null);

      if (result.restart === 'scheduled') {
        // El host va a detenerse. Se entra en el estado de reconexión global de
        // F-08 §8, que bloquea la interfaz y sondea hasta que vuelva.
        connection.expectRestart(result.message);
        return;
      }

      show(result.message);
      await reload();
    } catch (error) {
      // Un 409 no se muestra como «error»: el servidor ya redactó la frase que
      // dice qué lo impide y qué hacer.
      setFailure(describe(error, activating ? 'activar el módulo' : 'desactivar el módulo'));
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  if (state.status === 'forbidden') {
    return <ForbiddenPage minimum="admin" />;
  }

  return (
    <PageContainer
      title="Módulos"
      description="Los bloques que componen tu sistema. Activar o desactivar uno reinicia el servicio unos segundos."
    >
      <FailureAlert failure={failure} />

      {state.status === 'error' && <Alert tone="danger">{state.failure.message}</Alert>}

      {state.status === 'loading' && (
        <div style={{ display: 'flex', justifyContent: 'center', padding: 'var(--s7)' }}>
          <Spinner size="lg" label="Cargando módulos" />
        </div>
      )}

      {state.status === 'ready' && (
        <div className="mod-grid">
          {modules.map((module) => (
            <ModuleCard
              key={module.code}
              module={module}
              displayNames={displayNames}
              highlighted={highlighted === module.code}
              busy={busy}
              onToggle={setPending}
              onJumpTo={jumpTo}
            />
          ))}

          {/* Con solo CORE la pantalla debe verse intencionada, no rota. */}
          {modules.length <= 1 && (
            <p className="mod__note">
              Aquí aparecerán los módulos conforme los vayas contratando: catálogo, ventas,
              contenido web, clientes y los demás. Cada uno se activa y se desactiva desde esta
              pantalla.
            </p>
          )}

          {modules.length === 0 && (
            <EmptyState
              title="No hay módulos"
              description="Ni siquiera el núcleo aparece, lo cual no debería pasar. Revisa el registro del servidor."
            />
          )}
        </div>
      )}

      <ConfirmDialog
        open={pending !== null}
        title={
          pending?.isActive
            ? `Desactivar ${pending.displayName}`
            : `Activar ${pending?.displayName ?? ''}`
        }
        // El botón nombra la acción. Nunca «Aceptar».
        confirmLabel={
          pending?.isActive
            ? `Desactivar ${pending.displayName}`
            : `Activar ${pending?.displayName ?? ''}`
        }
        danger={pending?.isActive ?? false}
        busy={busy}
        onConfirm={() => void confirm()}
        onCancel={() => setPending(null)}
      >
        {/* El aviso del reinicio no es cortesía: es la única operación del panel
            que interrumpe el servicio, incluida la web pública del negocio. */}
        <p>
          <strong>El sistema se reiniciará unos segundos.</strong> Durante ese rato ni el panel ni
          la web pública responderán.
        </p>
        <p>Tu sesión seguirá abierta y volverás aquí automáticamente.</p>
        {pending?.isActive && (
          <p>Las pantallas de este módulo dejarán de aparecer en el menú.</p>
        )}
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
