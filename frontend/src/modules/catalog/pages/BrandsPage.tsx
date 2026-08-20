import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { BrandForm } from '../components/BrandForm';
import { brandsService, type Brand } from '../services/brands';
import '../components/catalog.css';

/**
 * Marcas del catálogo.
 *
 * Es el vertical más pequeño de M01 y el primero del paso 4, así que fija el
 * patrón que van a repetir categorías, productos y variantes.
 */
export function BrandsPage() {
  const load = useCallback(() => brandsService.list(), []);
  const { state, reload } = useResource(load, 'cargar las marcas');
  const { toasts, show } = useToasts();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Brand | null>(null);
  const [pendingDeactivation, setPendingDeactivation] = useState<Brand | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  const brands = state.status === 'ready' ? state.data : [];

  // Por debajo de un segundo no se enseña nada: un indicador que parpadea
  // hace que una respuesta rápida se perciba como lenta.
  const showLoading = useDelayedFlag(state.status === 'loading');

  async function deactivate() {
    if (!pendingDeactivation) {
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      await brandsService.deactivate(pendingDeactivation.id);
      show(`«${pendingDeactivation.name}» ya no aparece en la web.`);
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'dar de baja la marca'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  const columns: Column<Brand>[] = [
    {
      key: 'name',
      header: 'Marca',
      render: (brand) => (
        <div className="cat-brand">
          {/* Sin imagen y con imagen borrada se ven igual, y es correcto: en
              los dos casos la marca no tiene logotipo. El hueco es
              intencionado, no una etiqueta rota. */}
          {brand.logoUrl ? (
            <img src={brand.logoUrl} alt="" className="cat-brand__logo" />
          ) : (
            <div className="cat-brand__nologo" aria-hidden="true" />
          )}

          <div>
            <div style={{ fontWeight: 560 }}>{brand.name}</div>
            <div className="cat-brand__slug">{brand.slug}</div>
          </div>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Estado',
      render: (brand) =>
        brand.isActive ? (
          <Badge tone="success">Visible</Badge>
        ) : (
          <Badge tone="neutral">Oculta</Badge>
        ),
    },
    {
      key: 'actions',
      header: 'Acciones',
      align: 'right',
      render: (brand) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => setEditing(brand)}>
            Editar
          </Button>
          {brand.isActive && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(brand)}>
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
      title="Marcas"
      description="Los fabricantes de lo que vendes. Sirven para filtrar en la web y para agrupar el catálogo."
      actions={<Button onClick={() => setCreating(true)}>Nueva marca</Button>}
    >
      <FailureAlert failure={failure} />

      <Table
        columns={columns}
        rows={brands}
        rowKey={(brand) => brand.id}
        // Atenuadas, no ocultas: es baja lógica, y desaparecerlas haría creer
        // que se borraron.
        dimmed={(brand) => !brand.isActive}
        loading={showLoading}
        empty={
          // Estado vacío, no error: una instalación nueva no tiene marcas y la
          // pantalla tiene que decir qué hacer, no quedarse en blanco.
          <EmptyState
            title="Todavía no hay marcas"
            description="Crea la primera y podrás asignársela a tus productos."
            action={<Button onClick={() => setCreating(true)}>Crear la primera marca</Button>}
          />
        }
      />

      {(creating || editing) && (
        <BrandForm
          open
          // La clave fuerza un formulario limpio al cambiar de marca.
          key={editing?.id ?? 'nueva'}
          brand={editing}
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
        {/* Una frase, no un recuento: contar productos por marca es el mismo
            caso que contar referencias a un archivo, y se descartó por no
            tener segundo caso real (SPEC §6.8). La regla que importa es no
            ser silencioso, que es distinto de ser exacto. */}
        <p>
          Si esta marca tiene productos, <strong>seguirán existiendo y conservándola</strong>. Lo
          que cambia es que deja de aparecer en la web.
        </p>
        <p>No se borra nada: puedes volver a hacerla visible cuando quieras.</p>
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
