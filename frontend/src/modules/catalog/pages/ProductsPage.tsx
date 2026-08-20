import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState, Field, Input } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { ProductForm } from '../components/ProductForm';
import { priceLabel, productsService, type Product, type ProductListItem } from '../services/products';
import '../components/catalog.css';

/**
 * Productos del catálogo.
 *
 * Es la pantalla que el módulo existe para tener. Lo que **no** aparece por
 * ninguna parte es la palabra «variante»: mientras un producto tenga una
 * sola, sus campos son campos del producto (SPEC §11, regla 2).
 */
export function ProductsPage() {
  const { toasts, show } = useToasts();

  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);

  const load = useCallback(
    () => productsService.list({ q: q.trim() || undefined, page, pageSize: 10 }),
    [q, page],
  );
  const { state, reload } = useResource(load, 'cargar los productos');

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<ProductListItem | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  const result = state.status === 'ready' ? state.data : null;
  const showLoading = useDelayedFlag(state.status === 'loading');

  async function abrirFicha(id: string) {
    // La ficha completa no está en el listado: se pide al abrir. El listado
    // lleva lo que se ve en una fila, y nada más.
    try {
      setEditing(await productsService.get(id));
    } catch (error) {
      setFailure(describe(error, 'abrir el producto'));
    }
  }

  async function deactivate() {
    if (!pendingDeactivation) {
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      await productsService.deactivate(pendingDeactivation.id);
      show(`«${pendingDeactivation.name}» ya no está a la venta.`);
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'dar de baja el producto'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  const columns: Column<ProductListItem>[] = [
    {
      key: 'name',
      header: 'Producto',
      render: (product) => (
        <div>
          <div style={{ fontWeight: 560 }}>{product.name}</div>
          <div className="cat-brand__slug">{product.slug}</div>
        </div>
      ),
    },
    {
      key: 'brand',
      header: 'Marca',
      render: (product) =>
        product.brandName ?? <span style={{ color: 'var(--text-subtle)' }}>Sin marca</span>,
    },
    {
      key: 'price',
      header: 'Precio',
      align: 'right',
      // «Consultar» y «Gratis» son cosas distintas, y en una lista se
      // distinguen con palabras, no con un hueco frente a un 0.
      render: (product) => priceLabel(product.listPrice),
    },
    {
      key: 'status',
      header: 'Estado',
      render: (product) =>
        !product.isActive ? (
          <Badge tone="neutral">De baja</Badge>
        ) : product.isPublic ? (
          <Badge tone="success">En la web</Badge>
        ) : (
          <Badge tone="neutral">Solo en el panel</Badge>
        ),
    },
    {
      key: 'actions',
      header: 'Acciones',
      align: 'right',
      render: (product) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => void abrirFicha(product.id)}>
            Editar
          </Button>
          {product.isActive && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(product)}>
              Dar de baja
            </Button>
          )}
        </div>
      ),
    },
  ];

  if (state.status === 'forbidden') {
    return <ForbiddenPage minimum="editor" />;
  }

  return (
    <PageContainer
      title="Productos"
      description="Lo que vendes. Cada producto tiene su ficha, su precio y sus imágenes."
      actions={<Button onClick={() => setCreating(true)}>Nuevo producto</Button>}
    >
      <FailureAlert failure={failure} />

      <Field label="Buscar" hint="Por nombre. Se ignoran mayúsculas y tildes.">
        {(props) => (
          <Input
            {...props}
            value={q}
            onChange={(event) => {
              setQ(event.target.value);
              setPage(1);
            }}
            placeholder="cuaderno"
          />
        )}
      </Field>

      <Table
        columns={columns}
        rows={result?.items ?? []}
        rowKey={(product) => product.id}
        dimmed={(product) => !product.isActive}
        loading={showLoading}
        empty={
          q.trim() === '' ? (
            <EmptyState
              title="Todavía no hay productos"
              description="Crea el primero y aparecerá aquí, listo para publicarlo."
              action={<Button onClick={() => setCreating(true)}>Crear el primer producto</Button>}
            />
          ) : (
            // Buscar y no encontrar no es lo mismo que no tener nada: la
            // segunda invita a crear, la primera invita a probar otra cosa.
            <EmptyState
              title={`Ningún producto coincide con «${q.trim()}»`}
              description="Prueba con menos palabras o revisa cómo está escrito."
            />
          )
        }
        pagination={
          result && result.totalPages > 1
            ? {
                page: result.page,
                totalPages: result.totalPages,
                totalItems: result.totalItems,
                onChange: setPage,
              }
            : undefined
        }
      />

      {(creating || editing) && (
        <ProductForm
          open
          key={editing?.id ?? 'nuevo'}
          product={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSaved={(message) => {
            if (message === '') {
              // Cambió una imagen: la ficha se recarga y el panel sigue abierto.
              if (editing) {
                void abrirFicha(editing.id);
              }
              return;
            }

            setCreating(false);
            setEditing(null);
            show(message);
            void reload();
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDeactivation !== null}
        title={`Dar de baja ${pendingDeactivation?.name ?? ''}`}
        confirmLabel="Dar de baja"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setPendingDeactivation(null)}
      >
        <p>
          Deja de aparecer en la web y de poder venderse.{' '}
          <strong>Sus pedidos y su historial se conservan.</strong>
        </p>
        <p>No se borra nada: puedes volver a ponerlo a la venta cuando quieras.</p>
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
