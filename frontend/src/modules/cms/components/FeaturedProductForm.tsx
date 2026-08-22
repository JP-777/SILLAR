import { useState, type FormEvent, type KeyboardEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { Alert, Badge, Button, Field, Input, Spinner } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { useFeaturedProductPicker, type FeaturedProductPicker } from '../hooks/useFeaturedProductPicker';
import {
  featuredProductsCatalogService,
  featuredProductsService,
  type FeaturedProductAdmin,
  type FeaturedProductPickerItem,
} from '../services/featuredProducts';
import { CmsPublicationFields } from './CmsPublicationFields';
import { formatFeaturedProductPrice } from './featuredProductPresentation';

type FeaturedProductFormMode = 'create' | 'edit' | 'relink';

interface FeaturedProductFormProps {
  open: boolean;
  mode: FeaturedProductFormMode;
  product: FeaturedProductAdmin | null;
  catalogAvailable: boolean;
  onClose: () => void;
  onSaved: (product: FeaturedProductAdmin, message: string) => void;
}

/** Alta, vigencia y reenlace; cada modo conserva la frontera de su endpoint. */
export function FeaturedProductForm({
  open,
  mode,
  product,
  catalogAvailable,
  onClose,
  onSaved,
}: FeaturedProductFormProps) {
  const picker = useFeaturedProductPicker({ catalogAvailable });
  const [startsAt, setStartsAt] = useState<string | null>(product?.startsAt ?? null);
  const [endsAt, setEndsAt] = useState<string | null>(product?.endsAt ?? null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const needsSelection = mode !== 'edit';

  async function submit(event: FormEvent) {
    event.preventDefault();
    setFailure(null);

    if (needsSelection && !picker.selected) {
      return;
    }

    setBusy(true);
    try {
      if (mode === 'create') {
        const saved = await featuredProductsCatalogService.create({
          productId: picker.selected!.productId,
          startsAt,
          endsAt,
        });
        onSaved(saved, `Se destacó «${saved.productName}».`);
        return;
      }

      if (!product) {
        return;
      }

      if (mode === 'relink') {
        const saved = await featuredProductsCatalogService.relink(product.id, {
          productId: picker.selected!.productId,
        });
        onSaved(saved, `«${product.productName}» se volvió a enlazar con «${saved.productName}».`);
        return;
      }

      const saved = await featuredProductsService.update(product.id, { startsAt, endsAt });
      onSaved(saved, `Se actualizó la vigencia de «${saved.productName}».`);
    } catch (error) {
      setFailure(describe(error, contextFor(mode)));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={titleFor(mode, product)}
      description={descriptionFor(mode)}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>Cancelar</Button>
          <Button
            type="submit"
            form="formulario-producto-destacado-cms"
            loading={busy}
            disabled={needsSelection && picker.selected === null}
          >
            {submitLabelFor(mode)}
          </Button>
        </>
      }
    >
      <form id="formulario-producto-destacado-cms" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure} />

        {needsSelection && <FeaturedProductPickerControl picker={picker} />}

        {mode !== 'relink' && (
          <CmsPublicationFields
            startsAt={startsAt}
            endsAt={endsAt}
            onStartsAtChange={setStartsAt}
            onEndsAtChange={setEndsAt}
          />
        )}
      </form>
    </Drawer>
  );
}

function FeaturedProductPickerControl({ picker }: { picker: FeaturedProductPicker }) {
  const [term, setTerm] = useState('');

  function search() {
    void picker.search(term);
  }

  function submitSearch(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault();
      search();
    }
  }

  return (
    <div style={pickerStyle}>
      <Field
        label="Buscar producto"
        hint="Escribe una palabra completa: la búsqueda de Catálogo no encuentra prefijos."
        required
      >
        {(props) => (
          <div style={searchStyle}>
            <Input
              {...props}
              value={term}
              onChange={(event) => setTerm(event.target.value)}
              onKeyDown={submitSearch}
              placeholder="lapiz"
            />
            <Button
              variant="secondary"
              onClick={search}
              disabled={term.trim() === '' || picker.state.status === 'searching'}
            >
              Buscar
            </Button>
          </div>
        )}
      </Field>

      {picker.selected && (
        <Alert tone="success" title="Producto elegido">
          {picker.selected.name}
        </Alert>
      )}

      <PickerResults picker={picker} />
    </div>
  );
}

function PickerResults({ picker }: { picker: FeaturedProductPicker }) {
  switch (picker.state.status) {
    case 'unavailable':
      return <Alert tone="warning">Catálogo no está disponible; no se puede elegir un producto.</Alert>;
    case 'idle':
      return <Alert>Escribe un término y ejecuta la búsqueda.</Alert>;
    case 'searching':
      return <Spinner size="sm" label={`Buscando «${picker.state.query}»`} />;
    case 'empty':
      return (
        <Alert tone="warning">
          No hay resultados para «{picker.state.query}». Prueba escribiendo la palabra completa.
        </Alert>
      );
    case 'error':
      return <FailureAlert failure={picker.state.failure} />;
    case 'results':
      return (
        <ul style={resultsStyle}>
          {picker.state.items.map((item) => (
            <li key={item.productId}>
              <PickerResult
                item={item}
                selected={picker.selected?.productId === item.productId}
                onSelect={() => picker.select(item)}
              />
            </li>
          ))}
        </ul>
      );
  }
}

function PickerResult({
  item,
  selected,
  onSelect,
}: {
  item: FeaturedProductPickerItem;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <div style={resultStyle}>
      <div>
        <div style={{ fontWeight: 560 }}>{item.name}</div>
        <div>{item.primaryCategoryName ?? 'Sin categoría'}</div>
        <div>
          {formatFeaturedProductPrice(item.price)}
          {item.priceVaries && <Badge tone="neutral">Precio variable</Badge>}
        </div>
        {!item.isPublic && <Badge tone="warning">No público</Badge>}
      </div>
      <Button
        size="sm"
        variant={selected ? 'primary' : 'secondary'}
        aria-pressed={selected}
        onClick={onSelect}
      >
        {selected ? 'Elegido' : 'Elegir'}
      </Button>
    </div>
  );
}

function titleFor(mode: FeaturedProductFormMode, product: FeaturedProductAdmin | null): string {
  if (mode === 'create') return 'Nuevo producto destacado';
  if (mode === 'relink') return `Volver a enlazar ${product?.productName ?? 'producto'}`;
  return `Editar vigencia de ${product?.productName ?? 'producto'}`;
}

function descriptionFor(mode: FeaturedProductFormMode): string {
  if (mode === 'create') return 'El producto se elige explícitamente y CMS copia su snapshot.';
  if (mode === 'relink') return 'El reenlace sustituye el snapshot, pero conserva la fila editorial.';
  return 'Editar cambia únicamente la ventana temporal; nunca cambia el producto enlazado.';
}

function submitLabelFor(mode: FeaturedProductFormMode): string {
  if (mode === 'create') return 'Destacar producto';
  if (mode === 'relink') return 'Volver a enlazar';
  return 'Guardar vigencia';
}

function contextFor(mode: FeaturedProductFormMode): string {
  if (mode === 'create') return 'destacar el producto';
  if (mode === 'relink') return 'volver a enlazar el producto destacado';
  return 'guardar la vigencia del producto destacado';
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
const pickerStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s3)' };
const searchStyle = { display: 'flex', alignItems: 'center', gap: 'var(--s2)' };
const resultsStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s2)', padding: 0, listStyle: 'none' };
const resultStyle = { display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 'var(--s3)' };
