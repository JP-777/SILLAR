import { useCallback, useState } from 'react';
import { useCapability } from '../../../capabilities/useCapability';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import { FeaturedProductForm } from '../components/FeaturedProductForm';
import { CmsPublicationStateBadge, formatCmsDateTime } from '../components/CmsPublicationFields';
import { formatFeaturedProductPrice } from '../components/featuredProductPresentation';
import {
  featuredProductsCatalogService,
  featuredProductsService,
  type FeaturedProductAdmin,
  type FeaturedProductRefreshResult,
} from '../services/featuredProducts';
import { buildCmsReorderRequest } from '../state/reorder';

/** Administración funcional de los snapshots editoriales de productos destacados. */
export function FeaturedProductsPage() {
  const load = useCallback(() => featuredProductsService.list(), []);
  const { state, reload } = useResource(load, 'cargar los productos destacados');
  const { has } = useCapability();
  const { hasRole } = useSession();
  const { toasts, show } = useToasts();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<FeaturedProductAdmin | null>(null);
  const [relinking, setRelinking] = useState<FeaturedProductAdmin | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<FeaturedProductAdmin | null>(null);
  const [busy, setBusy] = useState(false);
  const [reordering, setReordering] = useState(false);
  const [refreshingId, setRefreshingId] = useState<number | null>(null);
  const [reconciling, setReconciling] = useState(false);
  const [reconciliation, setReconciliation] = useState<FeaturedProductRefreshResult | null>(null);
  const [failure, setFailure] = useState<Failure | null>(null);
  const products = state.status === 'ready' ? state.data : [];
  const showLoading = useDelayedFlag(state.status === 'loading');
  const catalogAvailable = has('catalog');
  const canDeactivate = hasRole('admin');

  async function deactivate() {
    if (!pendingDeactivation) return;
    setBusy(true);
    setFailure(null);
    try {
      await featuredProductsService.deactivate(pendingDeactivation.id);
      show('El producto destacado quedó inactivo y ya no se publica.');
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'desactivar el producto destacado'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  async function move(fromIndex: number, toIndex: number) {
    setReordering(true);
    setFailure(null);
    try {
      await featuredProductsService.reorder(buildCmsReorderRequest(products, fromIndex, toIndex));
      show('Se actualizó el orden de los productos destacados.');
      await reload();
    } catch (error) {
      setFailure(describe(error, 'reordenar los productos destacados'));
    } finally {
      setReordering(false);
    }
  }

  async function refresh(product: FeaturedProductAdmin) {
    setRefreshingId(product.id);
    setFailure(null);
    try {
      await featuredProductsCatalogService.refresh(product.id);
      show(`Se actualizaron los datos de «${product.productName}».`);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'actualizar los datos del producto destacado'));
    } finally {
      setRefreshingId(null);
    }
  }

  async function reconcile() {
    setReconciling(true);
    setFailure(null);
    setReconciliation(null);
    try {
      const result = await featuredProductsCatalogService.refreshAll();
      setReconciliation(result);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'actualizar todos los productos destacados'));
    } finally {
      setReconciling(false);
    }
  }

  const columns: Column<FeaturedProductAdmin>[] = [
    {
      key: 'product',
      header: 'Producto',
      render: (product) => (
        <div>
          <div style={{ fontWeight: 560 }}>{product.productName}</div>
          <div style={{ color: 'var(--text-subtle)' }}>{product.productCategory ?? 'Sin categoría'}</div>
          <div>
            {formatFeaturedProductPrice(product.productPrice)}
            {product.productPriceVaries && <Badge tone="neutral">Precio variable</Badge>}
          </div>
        </div>
      ),
    },
    {
      key: 'catalog',
      header: 'Catálogo',
      render: (product) => (
        <div>
          {product.productId === null
            ? <Badge tone="danger">Vínculo perdido</Badge>
            : <Badge tone="success">Vinculado</Badge>}
          {product.pendingRelink && <Badge tone="warning">Pendiente de reenlace</Badge>}
          {product.productIsActive
            ? <Badge tone="success">Producto activo</Badge>
            : <Badge tone="neutral">Producto desactivado</Badge>}
          {product.productIsPublic
            ? <Badge tone="success">Producto público</Badge>
            : <Badge tone="warning">Producto no público</Badge>}
        </div>
      ),
    },
    {
      key: 'publication',
      header: 'Publicación',
      render: (product) => <CmsPublicationStateBadge state={product.publicationState} />,
    },
    {
      key: 'editorial',
      header: 'Editorial',
      render: (product) => product.isActive
        ? <Badge tone="success">Destacado activo</Badge>
        : <Badge tone="neutral">Destacado inactivo</Badge>,
    },
    {
      key: 'window',
      header: 'Vigencia',
      render: (product) => (
        <div>
          <div>Inicio: {formatCmsDateTime(product.startsAt, 'sin inicio')}</div>
          <div>Fin: {formatCmsDateTime(product.endsAt, 'sin fin')}</div>
        </div>
      ),
    },
    {
      key: 'order',
      header: 'Orden',
      render: (product) => {
        const index = products.findIndex((candidate) => candidate.id === product.id);
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
              disabled={reordering || index === products.length - 1}
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
      render: (product) => {
        const needsRelink = product.pendingRelink || product.productId === null;
        return (
          <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
            <Button size="sm" variant="secondary" onClick={() => setEditing(product)}>
              Editar vigencia
            </Button>
            {catalogAvailable && needsRelink && (
              <Button size="sm" variant="secondary" onClick={() => setRelinking(product)}>
                Volver a enlazar
              </Button>
            )}
            {catalogAvailable && !needsRelink && (
              <Button
                size="sm"
                variant="secondary"
                loading={refreshingId === product.id}
                disabled={refreshingId !== null && refreshingId !== product.id}
                onClick={() => void refresh(product)}
              >
                Actualizar datos
              </Button>
            )}
            {canDeactivate && product.isActive && (
              <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(product)}>
                Desactivar
              </Button>
            )}
          </div>
        );
      },
    },
  ];

  if (state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;

  return (
    <PageContainer
      title="Productos destacados"
      description="Snapshots editoriales del Catálogo preparados para la portada."
      actions={catalogAvailable ? (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button onClick={() => setCreating(true)}>Destacar producto</Button>
          <Button variant="secondary" loading={reconciling} onClick={() => void reconcile()}>
            Actualizar todos
          </Button>
        </div>
      ) : undefined}
    >
      {!catalogAvailable && (
        <Alert tone="warning">
          Catálogo no está disponible. Los snapshots siguen editables y ordenables, pero no se pueden elegir, reenlazar ni actualizar productos.
        </Alert>
      )}
      {reconciliation && (
        <Alert tone="success" title="Actualización terminada">
          Actualizados: {reconciliation.refreshedCount}. Pendientes de reenlace: {reconciliation.pendingRelinkCount}.
        </Alert>
      )}
      <FailureAlert failure={failure} />

      {state.status === 'error' ? (
        <>
          <FailureAlert failure={state.failure} />
          <Button variant="secondary" onClick={() => void reload()}>Volver a intentar</Button>
        </>
      ) : (
        <Table
          columns={columns}
          rows={products}
          rowKey={(product) => product.id}
          dimmed={(product) => !product.isActive}
          loading={showLoading}
          empty={
            <EmptyState
              title="Todavía no hay productos destacados"
              description={catalogAvailable
                ? 'Elige el primero desde Catálogo para preparar la portada.'
                : 'Los destacados existentes aparecerán aquí cuando CMS vuelva a tenerlos.'}
              action={catalogAvailable
                ? <Button onClick={() => setCreating(true)}>Destacar el primer producto</Button>
                : undefined}
            />
          }
        />
      )}

      {creating && catalogAvailable && (
        <FeaturedProductForm
          open
          mode="create"
          product={null}
          catalogAvailable
          onClose={() => setCreating(false)}
          onSaved={(_, message) => {
            setCreating(false);
            show(message);
            void reload();
          }}
        />
      )}

      {editing && (
        <FeaturedProductForm
          open
          key={editing.id}
          mode="edit"
          product={editing}
          catalogAvailable={catalogAvailable}
          onClose={() => setEditing(null)}
          onSaved={(_, message) => {
            setEditing(null);
            show(message);
            void reload();
          }}
        />
      )}

      {relinking && catalogAvailable && (
        <FeaturedProductForm
          open
          key={`relink-${relinking.id}`}
          mode="relink"
          product={relinking}
          catalogAvailable
          onClose={() => setRelinking(null)}
          onSaved={(_, message) => {
            setRelinking(null);
            show(message);
            void reload();
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDeactivation !== null}
        title="Desactivar producto destacado"
        confirmLabel="Desactivar destacado"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setPendingDeactivation(null)}
      >
        <p>Dejará de aparecer en la portada. El snapshot y su posición se conservan en administración.</p>
      </ConfirmDialog>
      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
