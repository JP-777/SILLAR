import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import { FeaturedProjectForm } from '../components/FeaturedProjectForm';
import { featuredProjectsService, type FeaturedProjectAdmin } from '../services/featuredProjects';
import { buildCmsReorderRequest } from '../state/reorder';

/** Administración funcional de los trabajos destacados. */
export function FeaturedProjectsPage() {
  const load = useCallback(() => featuredProjectsService.list(), []);
  const { state, reload } = useResource(load, 'cargar los trabajos destacados');
  const { hasRole } = useSession();
  const { toasts, show } = useToasts();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<FeaturedProjectAdmin | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<FeaturedProjectAdmin | null>(null);
  const [busy, setBusy] = useState(false);
  const [reordering, setReordering] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const projects = state.status === 'ready' ? state.data : [];
  const showLoading = useDelayedFlag(state.status === 'loading');
  const canDeactivate = hasRole('admin');

  async function deactivate() {
    if (!pendingDeactivation) return;
    setBusy(true);
    setFailure(null);
    try {
      await featuredProjectsService.deactivate(pendingDeactivation.id);
      show('El trabajo quedó inactivo y ya no se publica.');
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'desactivar el trabajo destacado'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  async function move(fromIndex: number, toIndex: number) {
    setReordering(true);
    setFailure(null);
    try {
      await featuredProjectsService.reorder(buildCmsReorderRequest(projects, fromIndex, toIndex));
      show('Se actualizó el orden de los trabajos destacados.');
      await reload();
    } catch (error) {
      setFailure(describe(error, 'reordenar los trabajos destacados'));
    } finally {
      setReordering(false);
    }
  }

  const columns: Column<FeaturedProjectAdmin>[] = [
    {
      key: 'content',
      header: 'Trabajo',
      render: (project) => (
        <div>
          <div style={{ fontWeight: 560 }}>{project.title}</div>
          {project.description && <div style={{ color: 'var(--text-subtle)' }}>{project.description}</div>}
          {!project.isComplete && <Badge tone="warning">Incompleto</Badge>}
        </div>
      ),
    },
    {
      key: 'editorial',
      header: 'Editorial',
      render: (project) => project.isActive
        ? <Badge tone="success">Activo</Badge>
        : <Badge tone="neutral">Inactivo</Badge>,
    },
    {
      key: 'order',
      header: 'Orden',
      render: (project) => {
        const index = projects.findIndex((candidate) => candidate.id === project.id);
        return (
          <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
            <Button
              size="sm"
              variant="secondary"
              disabled={reordering || index === 0}
              onClick={() => void move(index, index - 1)}
            >
              Subir
            </Button>
            <Button
              size="sm"
              variant="secondary"
              disabled={reordering || index === projects.length - 1}
              onClick={() => void move(index, index + 1)}
            >
              Bajar
            </Button>
          </div>
        );
      },
    },
    {
      key: 'actions',
      header: 'Acciones',
      align: 'right',
      render: (project) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => setEditing(project)}>Editar</Button>
          {canDeactivate && project.isActive && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(project)}>
              Desactivar
            </Button>
          )}
        </div>
      ),
    },
  ];

  if (state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;

  return (
    <PageContainer
      title="Trabajos destacados"
      description="Proyectos que aparecen en la galería pública, con su orden editorial."
      actions={<Button onClick={() => setCreating(true)}>Nuevo trabajo</Button>}
    >
      <FailureAlert failure={failure} />
      {state.status === 'error' ? (
        <>
          <FailureAlert failure={state.failure} />
          <Button variant="secondary" onClick={() => void reload()}>Volver a intentar</Button>
        </>
      ) : (
        <Table
          columns={columns}
          rows={projects}
          rowKey={(project) => project.id}
          dimmed={(project) => !project.isActive}
          loading={showLoading}
          empty={
            <EmptyState
              title="Todavía no hay trabajos destacados"
              description="Crea el primero para preparar la galería pública."
              action={<Button onClick={() => setCreating(true)}>Crear el primer trabajo</Button>}
            />
          }
        />
      )}

      {(creating || editing) && (
        <FeaturedProjectForm
          open
          key={editing?.id ?? 'nuevo'}
          project={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSaved={(_, message) => {
            setCreating(false);
            setEditing(null);
            show(message);
            void reload();
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDeactivation !== null}
        title="Desactivar trabajo destacado"
        confirmLabel="Desactivar trabajo"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setPendingDeactivation(null)}
      >
        <p>Dejará de aparecer en la galería. La fila y su posición se conservan en administración.</p>
      </ConfirmDialog>
      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
