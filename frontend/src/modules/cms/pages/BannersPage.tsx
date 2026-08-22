import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import { BannerForm } from '../components/BannerForm';
import { CmsPublicationStateBadge, formatCmsDateTime } from '../components/CmsPublicationFields';
import { bannersService, type BannerAdmin } from '../services/banners';
import { buildCmsReorderRequest } from '../state/reorder';

/** Administración funcional de los banners de portada. */
export function BannersPage() {
  const load = useCallback(() => bannersService.list(), []);
  const { state, reload } = useResource(load, 'cargar los banners');
  const { hasRole } = useSession();
  const { toasts, show } = useToasts();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<BannerAdmin | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<BannerAdmin | null>(null);
  const [busy, setBusy] = useState(false);
  const [reordering, setReordering] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const banners = state.status === 'ready' ? state.data : [];
  const showLoading = useDelayedFlag(state.status === 'loading');
  const canDeactivate = hasRole('admin');

  async function deactivate() {
    if (!pendingDeactivation) return;
    setBusy(true);
    setFailure(null);
    try {
      await bannersService.deactivate(pendingDeactivation.id);
      show('El banner quedó inactivo y ya no se publica.');
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'desactivar el banner'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  async function move(fromIndex: number, toIndex: number) {
    setReordering(true);
    setFailure(null);
    try {
      await bannersService.reorder(buildCmsReorderRequest(banners, fromIndex, toIndex));
      show('Se actualizó el orden de los banners.');
      await reload();
    } catch (error) {
      setFailure(describe(error, 'reordenar los banners'));
    } finally {
      setReordering(false);
    }
  }

  const columns: Column<BannerAdmin>[] = [
    {
      key: 'content',
      header: 'Banner',
      render: (banner) => (
        <div>
          <div style={{ fontWeight: 560 }}>{banner.title ?? `Banner #${banner.id}`}</div>
          {banner.subtitle && <div style={{ color: 'var(--text-subtle)' }}>{banner.subtitle}</div>}
          {!banner.isComplete && <Badge tone="warning">Incompleto</Badge>}
        </div>
      ),
    },
    {
      key: 'publication',
      header: 'Publicación',
      render: (banner) => <CmsPublicationStateBadge state={banner.publicationState} />,
    },
    {
      key: 'editorial',
      header: 'Editorial',
      render: (banner) => banner.isActive
        ? <Badge tone="success">Activo</Badge>
        : <Badge tone="neutral">Inactivo</Badge>,
    },
    {
      key: 'window',
      header: 'Vigencia',
      render: (banner) => (
        <div>
          <div>Inicio: {formatCmsDateTime(banner.startsAt, 'sin inicio')}</div>
          <div>Fin: {formatCmsDateTime(banner.endsAt, 'sin fin')}</div>
        </div>
      ),
    },
    {
      key: 'order',
      header: 'Orden',
      render: (banner) => {
        const index = banners.findIndex((candidate) => candidate.id === banner.id);
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
              disabled={reordering || index === banners.length - 1}
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
      render: (banner) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => setEditing(banner)}>Editar</Button>
          {canDeactivate && banner.isActive && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(banner)}>
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
      title="Banners"
      description="Contenido principal de la portada, con vigencia y orden editorial."
      actions={<Button onClick={() => setCreating(true)}>Nuevo banner</Button>}
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
          rows={banners}
          rowKey={(banner) => banner.id}
          dimmed={(banner) => !banner.isActive}
          loading={showLoading}
          empty={
            <EmptyState
              title="Todavía no hay banners"
              description="Crea el primero para preparar la portada."
              action={<Button onClick={() => setCreating(true)}>Crear el primer banner</Button>}
            />
          }
        />
      )}

      {(creating || editing) && (
        <BannerForm
          open
          key={editing?.id ?? 'nuevo'}
          banner={editing}
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
        title="Desactivar banner"
        confirmLabel="Desactivar banner"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setPendingDeactivation(null)}
      >
        <p>Dejará de aparecer en la portada. La fila y su posición se conservan en administración.</p>
      </ConfirmDialog>
      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
