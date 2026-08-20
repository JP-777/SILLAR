import { useEffect, useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { ImagePicker } from '../../../shared/media/ImagePicker';
import { Button, Field, Input, Switch } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { brandsService, type Brand } from '../services/brands';
import { categoriesService, type Category } from '../services/categories';
import { CategoryAssigner } from './CategoryAssigner';
import { VariantsTable, draftToPayload, type VariantDraft } from './VariantsTable';
import {
  priceFromInput,
  priceToInput,
  productsService,
  type Product,
} from '../services/products';

interface ProductFormProps {
  open: boolean;
  /** Producto a editar, o `null` para crear uno nuevo. */
  product: Product | null;
  onClose: () => void;
  onSaved: (message: string) => void;
}

/**
 * Alta y edición de un producto.
 *
 * **La variante es invisible mientras haya una sola.** Código, código de
 * barras y precio propio se editan aquí como si fueran campos del producto
 * —lo son, de su variante única— y en ningún sitio aparece la palabra
 * «variante». Obligar a pensar en variantes para dar de alta un plato de menú
 * es cargarle a todo el mundo la complejidad de unos pocos.
 *
 * **Forma preparada para 04D.** Esos tres campos viven juntos en su propio
 * bloque, alimentados por `único`, y el formulario ya distingue el caso de una
 * variante del de varias. Cuando llegue la tabla de variantes, sustituye ese
 * bloque: no hay que recolocar el resto del formulario ni mover estado.
 */
export function ProductForm({ open, product, onClose, onSaved }: ProductFormProps) {
  const editing = product !== null;

  // Una sola variante: sus campos son campos del producto. Con más de una,
  // dejan de serlo y los edita la tabla de 04D.
  const único = editing && product.items.length === 1 ? product.items[0] : null;

  const [name, setName] = useState(product?.name ?? '');
  const [slug, setSlug] = useState(product?.slug ?? '');
  const [shortDescription, setShortDescription] = useState(product?.shortDescription ?? '');
  const [description, setDescription] = useState(product?.description ?? '');
  const [brandId, setBrandId] = useState<string | null>(product?.brandId ?? null);
  const [listPrice, setListPrice] = useState(priceToInput(product?.listPrice ?? null));
  const [saleUnit, setSaleUnit] = useState(product?.saleUnit ?? '');
  const [isPublic, setIsPublic] = useState(product?.isPublic ?? false);
  const [isActive, setIsActive] = useState(product?.isActive ?? true);

  /**
   * Las categorías del producto y cuál es la principal.
   *
   * Van juntas porque la regla 6 las ata: la principal tiene que ser una de
   * ellas. Separarlas en dos estados permite que se contradigan entre un
   * render y el siguiente.
   */
  const [categoryIds, setCategoryIds] = useState<string[]>(product?.categoryIds ?? []);
  const [primaryCategoryId, setPrimaryCategoryId] = useState<string | null>(
    product?.primaryCategoryId ?? null,
  );
  const [categories, setCategories] = useState<Category[]>([]);

  const [code, setCode] = useState(único?.code ?? '');
  const [barcode, setBarcode] = useState(único?.barcode ?? '');
  const [variantLabel, setVariantLabel] = useState(product?.variantLabel ?? '');

  /**
   * Las presentaciones, cuando hay varias. `null` mientras hay una sola.
   *
   * El estado empieza donde estaba el producto, así que abrir la ficha de uno
   * con tres presentaciones ya enseña la tabla.
   */
  const [variants, setVariants] = useState<VariantDraft[] | null>(
    editing && product.items.length > 1
      ? product.items.map((item) => ({
          id: item.id,
          variantValue: item.variantValue ?? '',
          code: item.code ?? '',
          barcode: item.barcode ?? '',
          priceOverride: item.priceOverride === null ? '' : String(item.priceOverride),
        }))
      : null,
  );

  /** Lo que anuncia el momento, sin robar el foco. */
  const [aviso, setAviso] = useState('');

  /** Si la tabla acaba de abrirse en este mismo instante. */
  const [reciénAbierto, setReciénAbierto] = useState(false);

  /**
   * El momento: de una presentación a varias.
   *
   * El orden es el que fija la entrega, y el argumento importa más que la
   * animación: **lo que impide leerlo como pérdida no es el movimiento, es
   * que los valores estén ahí.** Un campo con «PLU-ART-PG» dentro no se lee
   * como borrado, se anime o no — y con movimiento reducido no queda nada de
   * la animación, así que si la seguridad dependiera de ella, para esas
   * personas no habría ninguna.
   *
   * 1. Los valores se quedan donde estaban, convertidos en la primera fila.
   * 2. Un aviso `role="status"` lo dice con palabras, sin llevarse el foco.
   * 3. El cursor entra en la segunda fila, que convierte el aviso en
   *    instrucción.
   */
  function abrirPresentaciones() {
    setVariants([
      {
        id: único?.id ?? null,
        variantValue: '',
        code,
        barcode,
        priceOverride: único?.priceOverride === null || único === null ? '' : String(único.priceOverride),
      },
      { id: null, variantValue: '', code: '', barcode: '', priceOverride: '' },
    ]);

    setReciénAbierto(true);
    setAviso(
      'Lo que habías escrito es ahora la primera presentación. Escribe la segunda debajo.',
    );
  }

  /** La vuelta atrás: se queda con la primera y devuelve sus datos arriba. */
  function cerrarPresentaciones() {
    const primera = variants?.[0];

    if (primera) {
      setCode(primera.code);
      setBarcode(primera.barcode);
    }

    setVariants(null);
    setReciénAbierto(false);
    setAviso('Vuelve a tener una sola presentación. Sus datos están arriba.');
  }

  const [brands, setBrands] = useState<Brand[]>([]);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    // Las marcas activas, para poder elegir una. Si falla, el campo se queda
    // vacío y el resto del formulario sigue funcionando: no tener marcas no
    // impide dar de alta un producto.
    brandsService
      .list()
      .then((all) => setBrands(all.filter((brand) => brand.isActive)))
      .catch(() => setBrands([]));

    // Todas, activas o no: el propio control decide cuáles enseña, porque una
    // categoría dada de baja que el producto ya tiene sigue asignada.
    categoriesService
      .list()
      .then(setCategories)
      .catch(() => setCategories([]));
  }, []);

  /** Si el conjunto o la principal difieren de lo que tenía el producto. */
  function categoríasCambiaron() {
    const antes = product?.categoryIds ?? [];
    return (
      primaryCategoryId !== (product?.primaryCategoryId ?? null) ||
      antes.length !== categoryIds.length ||
      categoryIds.some((id) => !antes.includes(id))
    );
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);
    setFieldErrors({});

    const trimmedSlug = slug.trim();
    const vacíoANulo = (texto: string) => (texto.trim() === '' ? null : texto.trim());

    try {
      if (editing) {
        // Con la tabla abierta, las presentaciones se guardan primero: si
        // alguna choca por código, el producto no queda a medio guardar.
        if (variants !== null) {
          await productsService.saveVariants(
            product.id,
            variants.map(draftToPayload).map((fila, i) => ({ ...fila, id: variants[i].id })),
            product.items,
          );
        }

        await productsService.update(
          product.id,
          {
            name: name.trim(),
            slug: trimmedSlug,
            shortDescription: vacíoANulo(shortDescription),
            description: vacíoANulo(description),
            brandId,
            listPrice: priceFromInput(listPrice),
            saleUnit: vacíoANulo(saleUnit),
            variantLabel: variantLabel.trim() === '' ? null : variantLabel.trim(),
            isPublic,
            isActive,
            // El contrato atómico: solo cuando hay exactamente una. Con la
            // tabla abierta no se mandan, y el servidor los rechazaría.
            code: variants === null ? vacíoANulo(code) : null,
            barcode: variants === null ? vacíoANulo(barcode) : null,
            singleVariantFieldsPresent: variants === null,
          },
        );

        // Las categorías son su propio endpoint —el `PUT` del producto no
        // las toca (regla 3)—, así que van aparte. **Solo si cambiaron**: una
        // petición que no cambia nada gasta una escritura y una línea de
        // auditoría diciendo que se actualizó algo que no se actualizó.
        if (categoríasCambiaron()) {
          await productsService.setCategories(product.id, categoryIds, primaryCategoryId);
        }

        onSaved(`Se guardaron los cambios de «${name.trim()}».`);
        return;
      }

      await productsService.create({
        name: name.trim(),
        slug: trimmedSlug === '' ? null : trimmedSlug,
        shortDescription: vacíoANulo(shortDescription),
        description: vacíoANulo(description),
        primaryCategoryId,
        categoryIds,
        brandId,
        listPrice: priceFromInput(listPrice),
        saleUnit: vacíoANulo(saleUnit),
        variantLabel: null,
        code: vacíoANulo(code),
        barcode: vacíoANulo(barcode),
      });

      onSaved(`Se creó «${name.trim()}».`);
    } catch (error) {
      const described = describe(error, editing ? 'guardar el producto' : 'crear el producto');
      setFailure(described);

      if (described.fieldErrors) {
        setFieldErrors(described.fieldErrors);
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? `Editar ${product.name}` : 'Nuevo producto'}
      description={
        editing
          ? 'Cambiar el nombre no cambia la dirección web: se edita aparte, a propósito.'
          : undefined
      }
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Cancelar
          </Button>
          <Button type="submit" form="formulario-producto" loading={busy}>
            {editing ? 'Guardar cambios' : 'Crear producto'}
          </Button>
        </>
      }
    >
      <form id="formulario-producto" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure?.kind === 'inline' ? failure : null} />

        <Field label="Nombre" required error={fieldErrors.producto ?? null}>
          {(props) => (
            <Input
              {...props}
              value={name}
              onChange={(event) => setName(event.target.value)}
              maxLength={200}
            />
          )}
        </Field>

        <Field
          label="Dirección web"
          hint={
            editing
              ? 'Cambiarla rompe los enlaces que ya circulen a este producto.'
              : 'Si la dejas vacía se genera del nombre.'
          }
          required={editing}
        >
          {(props) => (
            <Input
              {...props}
              value={slug}
              onChange={(event) => setSlug(event.target.value)}
              maxLength={200}
              placeholder="cuaderno-universitario"
            />
          )}
        </Field>

        <Field label="Marca">
          {(props) => (
            <select
              {...props}
              className="ui-input"
              value={brandId ?? ''}
              onChange={(event) => setBrandId(event.target.value || null)}
            >
              <option value="">Sin marca</option>
              {brands.map((brand) => (
                <option key={brand.id} value={brand.id}>
                  {brand.name}
                </option>
              ))}
            </select>
          )}
        </Field>

        <CategoryAssigner
          categories={categories}
          selected={categoryIds}
          primary={primaryCategoryId}
          onChange={(elegidas, principal) => {
            setCategoryIds(elegidas);
            setPrimaryCategoryId(principal);
          }}
          // El mismo `role="status"` que usa el momento de las
          // presentaciones: dos regiones vivas en la misma pantalla se pisan.
          onAnnounce={setAviso}
        />

        <Field label="Descripción corta" hint="Una o dos líneas, para la tarjeta del listado.">
          {(props) => (
            <Input
              {...props}
              value={shortDescription}
              onChange={(event) => setShortDescription(event.target.value)}
              maxLength={300}
            />
          )}
        </Field>

        <Field label="Descripción" hint="La ficha completa, en la web.">
          {(props) => (
            <textarea
              {...props}
              className="ui-input"
              rows={4}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              maxLength={2000}
            />
          )}
        </Field>

        {/* --- Los campos que en realidad son de la variante única -----------
            No se anuncian como tales. Cuando llegue 04D, este bloque entero se
            sustituye por la tabla de variantes y nada de arriba se mueve. */}
        {/* El aviso del momento: `role="status"` lo lee un lector de pantalla
            sin que nadie mueva el foco. Vive fuera de la tabla para que no
            desaparezca con ella. */}
        <div role="status" aria-live="polite" aria-atomic="true">
          {aviso !== '' && <p className="cat-variants__announce">{aviso}</p>}
        </div>

        {variants !== null ? (
          <VariantsTable
            rows={variants}
            onChange={setVariants}
            listPrice={priceFromInput(listPrice)}
            label={variantLabel}
            onLabelChange={setVariantLabel}
            onCollapse={cerrarPresentaciones}
            // El cursor en la segunda: el aviso dice qué pasó, el cursor dice
            // dónde seguir. Solo al abrir el momento, no al editar una ficha
            // que ya tenía varias.
            initialFocusRow={reciénAbierto ? 1 : undefined}
          />
        ) : (
          <>
            <Field
              label="Precio"
              hint="Déjalo vacío si el precio se consulta. Un cero significa que es gratis."
            >
              {(props) => (
                <Input
                  {...props}
                  type="number"
                  inputMode="decimal"
                  min={0}
                  step="0.01"
                  value={listPrice}
                  onChange={(event) => setListPrice(event.target.value)}
                  placeholder="Consultar"
                />
              )}
            </Field>

            <Field label="Código" hint="El que se teclea en caja.">
              {(props) => (
                <Input
                  {...props}
                  value={code}
                  onChange={(event) => setCode(event.target.value)}
                  maxLength={60}
                />
              )}
            </Field>

            <Field label="Código de barras">
              {(props) => (
                <Input
                  {...props}
                  value={barcode}
                  onChange={(event) => setBarcode(event.target.value)}
                  maxLength={60}
                />
              )}
            </Field>
          </>
        )}

        {variants === null && (
          <Button size="sm" variant="ghost" onClick={abrirPresentaciones}>
            Este producto viene en varias presentaciones
          </Button>
        )}

        <Field label="Unidad de venta" hint="«Por unidad», «por docena», «por metro».">
          {(props) => (
            <Input
              {...props}
              value={saleUnit}
              onChange={(event) => setSaleUnit(event.target.value)}
              maxLength={60}
            />
          )}
        </Field>

        {editing && <ImageList product={product} onChanged={() => onSaved('')} />}

        <Switch
          checked={isPublic}
          onChange={setIsPublic}
          label={isPublic ? 'Visible en la web' : 'Solo en el panel'}
        />

        {editing && (
          <Switch
            checked={isActive}
            onChange={setIsActive}
            label={isActive ? 'A la venta' : 'Dado de baja'}
          />
        )}
      </form>
    </Drawer>
  );
}

/**
 * Galería del producto.
 *
 * Las imágenes se asocian y se quitan **de una en una y al momento**, no al
 * guardar: son operaciones propias del API y encolarlas dentro del formulario
 * obligaría a rehacer aquí lo que el servidor ya sabe hacer.
 */
function ImageList({ product, onChanged }: { product: Product; onChanged: () => void }) {
  const [busy, setBusy] = useState(false);

  async function add(mediaAssetId: string | null) {
    if (!mediaAssetId || busy) {
      return;
    }

    setBusy(true);
    try {
      await productsService.addImage(product.id, mediaAssetId, null);
      onChanged();
    } finally {
      setBusy(false);
    }
  }

  async function remove(imageId: string) {
    setBusy(true);
    try {
      await productsService.removeImage(product.id, imageId);
      onChanged();
    } finally {
      setBusy(false);
    }
  }

  return (
    <Field label="Imágenes" hint="Se eligen de Archivos. Quitarlas aquí no borra el archivo.">
      {() => (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--s3)' }}>
          {product.images.length > 0 && (
            <ul className="cat-images">
              {product.images.map((image) => (
                <li key={image.id} className="cat-images__item">
                  <img src={image.url} alt={image.altText ?? ''} />
                  <Button size="sm" variant="ghost" onClick={() => void remove(image.id)}>
                    Quitar
                  </Button>
                </li>
              ))}
            </ul>
          )}

          <ImagePicker value={null} previewUrl={null} onChange={(id) => void add(id)} />
        </div>
      )}
    </Field>
  );
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
