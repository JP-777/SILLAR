import { useCallback, useMemo, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { CategoryForm } from '../components/CategoryForm';
import { asTree, categoriesService, type Category } from '../services/categories';
import '../components/catalog.css';

/** Fila del listado: la categoría más su profundidad en el árbol. */
type Row = { category: Category; depth: number };

/**
 * Categorías del catálogo.
 *
 * A diferencia de marcas, **es un árbol**: la lista se ordena y se sangra por
 * profundidad para no mentir sobre la jerarquía.
 */
export function CategoriesPage() {
  const load = useCallback(() => categoriesService.list(), []);
  const { state, reload } = useResource(load, 'cargar las categorías');
  const { toasts, show } = useToasts();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Category | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<Category | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  const categories = useMemo(() => (state.status === 'ready' ? state.data : []), [state]);
  const rows = useMemo(() => asTree(categories), [categories]);

  const showLoading = useDelayedFlag(state.status === 'loading');

  async function deactivate() {
    if (!pendingDeactivation) {
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      const result = await categoriesService.deactivate(pendingDeactivation.id);
      const affected = result.productsLosingThisCategory;

      show(
        affected === 0
          ? `«${pendingDeactivation.name}» ya no aparece en la web.`
          : `«${pendingDeactivation.name}» ya no aparece en la web. ${affected} producto${affected === 1 ? '' : 's'} sigue${affected === 1 ? '' : 'n'} activo${affected === 1 ? '' : 's'}, sin esta categoría.`,
      );

      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'dar de baja la categoría'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  const columns: Column<Row>[] = [
    {
      key: 'name',
      header: 'Categoría',
      render: ({ category, depth }) => (
        <div className="cat-tree" style={{ paddingLeft: `calc(${depth} * var(--s5))` }}>
          {/* La sangría dice de quién cuelga. El guion la hace legible cuando
              la columna es estrecha y la sangría sola se pierde. */}
          {depth > 0 && (
            <span className="cat-tree__mark" aria-hidden="true">
              └
            </span>
          )}

          {category.imageUrl ? (
            <img src={category.imageUrl} alt="" className="cat-brand__logo" />
          ) : (
            <div className="cat-brand__nologo" aria-hidden="true" />
          )}

          <div>
            <div style={{ fontWeight: 560 }}>{category.name}</div>
            <div className="cat-brand__slug">{category.slug}</div>
          </div>
        </div>
      ),
    },
    {
      key: 'products',
      header: 'Productos',
      align: 'right',
      render: ({ category }) =>
        category.productCount === 0 ? (
          <span style={{ color: 'var(--text-subtle)' }}>—</span>
        ) : (
          category.productCount
        ),
    },
    {
      key: 'status',
      header: 'Estado',
      render: ({ category }) =>
        category.isActive ? (
          <Badge tone="success">Visible</Badge>
        ) : (
          <Badge tone="neutral">Oculta</Badge>
        ),
    },
    {
      key: 'actions',
      header: 'Acciones',
      align: 'right',
      render: ({ category }) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => setEditing(category)}>
            Editar
          </Button>
          {category.isActive && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(category)}>
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

  const affected = pendingDeactivation?.productCount ?? 0;

  return (
    <PageContainer
      title="Categorías"
      description="Cómo se organiza tu catálogo. Una categoría puede colgar de otra."
      actions={<Button onClick={() => setCreating(true)}>Nueva categoría</Button>}
    >
      <FailureAlert failure={failure} />

      <Table
        columns={columns}
        rows={rows}
        rowKey={({ category }) => category.id}
        dimmed={({ category }) => !category.isActive}
        loading={showLoading}
        empty={
          <EmptyState
            title="Todavía no hay categorías"
            description="Crea la primera y podrás agrupar tus productos por ella."
            action={<Button onClick={() => setCreating(true)}>Crear la primera categoría</Button>}
          />
        }
      />

      {(creating || editing) && (
        <CategoryForm
          open
          key={editing?.id ?? 'nueva'}
          category={editing}
          all={categories}
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
        title={`Dar de baja ${pendingDeactivation?.name ?? ''}`}
        confirmLabel="Dar de baja"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setPendingDeactivation(null)}
      >
        {/* Aquí SÍ se cuenta, al revés que en marcas: la regla 9 del SPEC pide
            avisar cuántos productos se quedan sin esta categoría, para que la
            persona decida con el número delante. Por eso `productCount` viaja
            en el listado y no solo en la respuesta de la baja, que llegaría
            cuando ya está decidido. */}
        {affected === 0 ? (
          <p>Ninguna categoría deja de existir: esta no tiene productos todavía.</p>
        ) : (
          <p>
            <strong>
              {affected} producto{affected === 1 ? '' : 's'} se queda{affected === 1 ? '' : 'n'} sin
              esta categoría.
            </strong>{' '}
            Siguen activos y a la venta: <strong>no se desactiva ninguno</strong>.
          </p>
        )}
        <p>Sus subcategorías tampoco se dan de baja. No se borra nada: puedes deshacerlo.</p>
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
