import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import { CmsPublicationStateBadge, formatCmsDateTime } from '../components/CmsPublicationFields';
import { PromotionForm } from '../components/PromotionForm';
import { promotionsService, type PromotionAdmin } from '../services/promotions';
import { buildCmsReorderRequest } from '../state/reorder';

/** Administración funcional de las promociones de portada. */
export function PromotionsPage() {
  const load = useCallback(() => promotionsService.list(), []);
  const { state, reload } = useResource(load, 'cargar las promociones');
  const { hasRole } = useSession();
  const { toasts, show } = useToasts();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<PromotionAdmin | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<PromotionAdmin | null>(null);
  const [busy, setBusy] = useState(false);
  const [reordering, setReordering] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const promotions = state.status === 'ready' ? state.data : [];
  const showLoading = useDelayedFlag(state.status === 'loading');
  const canDeactivate = hasRole('admin');

  async function deactivate() {
    if (!pendingDeactivation) return;
    setBusy(true);
    setFailure(null);
    try {
      await promotionsService.deactivate(pendingDeactivation.id);
      show('La promoción quedó inactiva y ya no se publica.');
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'desactivar la promoción'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  async function move(fromIndex: number, toIndex: number) {
    setReordering(true);
    setFailure(null);
    try {
      await promotionsService.reorder(buildCmsReorderRequest(promotions, fromIndex, toIndex));
      show('Se actualizó el orden de las promociones.');
      await reload();
    } catch (error) {
      setFailure(describe(error, 'reordenar las promociones'));
    } finally {
      setReordering(false);
    }
  }

  const columns: Column<PromotionAdmin>[] = [
    {
      key: 'content',
      header: 'Promoción',
      render: (promotion) => (
        <div>
          <div style={{ fontWeight: 560 }}>{promotion.title ?? `Promoción #${promotion.id}`}</div>
          {promotion.subtitle && <div style={{ color: 'var(--text-subtle)' }}>{promotion.subtitle}</div>}
          {promotion.badgeText && <Badge tone="neutral">{promotion.badgeText}</Badge>}
        </div>
      ),
    },
    {
      key: 'publication',
      header: 'Publicación',
      render: (promotion) => <CmsPublicationStateBadge state={promotion.publicationState} />,
    },
    {
      key: 'editorial',
      header: 'Editorial',
      render: (promotion) => promotion.isActive
        ? <Badge tone="success">Activa</Badge>
        : <Badge tone="neutral">Inactiva</Badge>,
    },
    {
      key: 'window',
      header: 'Vigencia',
      render: (promotion) => (
        <div>
          <div>Inicio: {formatCmsDateTime(promotion.startsAt, 'sin inicio')}</div>
          <div>Fin: {formatCmsDateTime(promotion.endsAt, 'sin fin')}</div>
        </div>
      ),
    },
    {
      key: 'order',
      header: 'Orden',
      render: (promotion) => {
        const index = promotions.findIndex((candidate) => candidate.id === promotion.id);
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
              disabled={reordering || index === promotions.length - 1}
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
      render: (promotion) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => setEditing(promotion)}>Editar</Button>
          {canDeactivate && promotion.isActive && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(promotion)}>
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
      title="Promociones"
      description="Mensajes promocionales de la portada, con vigencia y orden editorial."
      actions={<Button onClick={() => setCreating(true)}>Nueva promoción</Button>}
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
          rows={promotions}
          rowKey={(promotion) => promotion.id}
          dimmed={(promotion) => !promotion.isActive}
          loading={showLoading}
          empty={
            <EmptyState
              title="Todavía no hay promociones"
              description="Crea la primera para preparar esta sección de la portada."
              action={<Button onClick={() => setCreating(true)}>Crear la primera promoción</Button>}
            />
          }
        />
      )}

      {(creating || editing) && (
        <PromotionForm
          open
          key={editing?.id ?? 'nueva'}
          promotion={editing}
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
        title="Desactivar promoción"
        confirmLabel="Desactivar promoción"
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
