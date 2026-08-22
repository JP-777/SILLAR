import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import { SocialLinkForm } from '../components/SocialLinkForm';
import { socialLinksService, type SocialLinkAdmin } from '../services/socialLinks';
import { buildCmsReorderRequest } from '../state/reorder';

/** Administración funcional de los enlaces sociales del footer. */
export function SocialLinksPage() {
  const load = useCallback(() => socialLinksService.list(), []);
  const { state, reload } = useResource(load, 'cargar las redes sociales');
  const { hasRole } = useSession();
  const { toasts, show } = useToasts();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<SocialLinkAdmin | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<SocialLinkAdmin | null>(null);
  const [busy, setBusy] = useState(false);
  const [reactivatingId, setReactivatingId] = useState<number | null>(null);
  const [reordering, setReordering] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const links = state.status === 'ready' ? state.data : [];
  const showLoading = useDelayedFlag(state.status === 'loading');
  const canManageLifecycle = hasRole('admin');

  async function deactivate() {
    if (!pendingDeactivation) return;
    setBusy(true);
    setFailure(null);
    try {
      await socialLinksService.deactivate(pendingDeactivation.id);
      show(`${pendingDeactivation.platform} quedó inactiva y ya no aparece en el footer.`);
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'desactivar la red social'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  async function reactivate(link: SocialLinkAdmin) {
    setReactivatingId(link.id);
    setFailure(null);
    try {
      await socialLinksService.reactivate(link.id);
      show(`${link.platform} volvió a estar activa.`);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'reactivar la red social'));
    } finally {
      setReactivatingId(null);
    }
  }

  async function move(fromIndex: number, toIndex: number) {
    setReordering(true);
    setFailure(null);
    try {
      await socialLinksService.reorder(buildCmsReorderRequest(links, fromIndex, toIndex));
      show('Se actualizó el orden de las redes sociales.');
      await reload();
    } catch (error) {
      setFailure(describe(error, 'reordenar las redes sociales'));
    } finally {
      setReordering(false);
    }
  }

  const columns: Column<SocialLinkAdmin>[] = [
    {
      key: 'platform',
      header: 'Red social',
      render: (link) => <div style={{ fontWeight: 560 }}>{link.platform}</div>,
    },
    {
      key: 'url',
      header: 'Dirección',
      render: (link) => <span>{link.url}</span>,
    },
    {
      key: 'editorial',
      header: 'Editorial',
      render: (link) => link.isActive
        ? <Badge tone="success">Activa</Badge>
        : <Badge tone="neutral">Inactiva</Badge>,
    },
    {
      key: 'order',
      header: 'Orden',
      render: (link) => {
        const index = links.findIndex((candidate) => candidate.id === link.id);
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
              disabled={reordering || index === links.length - 1}
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
      render: (link) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => setEditing(link)}>Editar</Button>
          {canManageLifecycle && link.isActive && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(link)}>
              Desactivar
            </Button>
          )}
          {canManageLifecycle && !link.isActive && (
            <Button
              size="sm"
              variant="secondary"
              loading={reactivatingId === link.id}
              disabled={reactivatingId !== null && reactivatingId !== link.id}
              onClick={() => void reactivate(link)}
            >
              Reactivar
            </Button>
          )}
        </div>
      ),
    },
  ];

  if (state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;

  return (
    <PageContainer
      title="Redes sociales"
      description="Enlaces públicos administrados por CMS y ordenados para el footer."
      actions={<Button onClick={() => setCreating(true)}>Nueva red social</Button>}
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
          rows={links}
          rowKey={(link) => link.id}
          dimmed={(link) => !link.isActive}
          loading={showLoading}
          empty={
            <EmptyState
              title="Todavía no hay redes sociales"
              description="Añade la primera para preparar los enlaces públicos."
              action={<Button onClick={() => setCreating(true)}>Añadir la primera red</Button>}
            />
          }
        />
      )}

      {(creating || editing) && (
        <SocialLinkForm
          open
          key={editing?.id ?? 'nueva'}
          link={editing}
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
        title="Desactivar red social"
        confirmLabel="Desactivar red social"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setPendingDeactivation(null)}
      >
        <p>Dejará de aparecer públicamente. Podrás reactivar esta misma fila sin recrearla.</p>
      </ConfirmDialog>
      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
