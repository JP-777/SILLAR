import { http } from '../../../shared/http/client';

/**
 * Capa de servicios de productos.
 *
 * **Aquí vive la variante invisible.** El SPEC manda que la palabra «variante»
 * no exista mientras el producto tenga una sola, y el API ya está diseñado
 * para eso al crear: `code` y `barcode` viajan como campos del producto y el
 * servidor los coloca en la variante que crea solo.
 *
 * Al **editar** no: el contrato de actualización del producto no los lleva, y
 * hay que tocar la variante por su propia ruta. Esa costura se resuelve aquí,
 * en una función, y no en el formulario — si la resolviera la pantalla, cada
 * pantalla futura tendría que volver a saber que existe una variante.
 */

/** Una variante. La pantalla solo mira la única mientras `items.length === 1`. */
export interface ProductItem {
  id: string;
  productId: string;
  /** Nulo en la variante única: es la señal de que no hay nada que enseñar. */
  variantValue: string | null;
  code: string | null;
  barcode: string | null;
  /** Precio propio. Nulo significa «usa el del producto», no «gratis». */
  priceOverride: number | null;
  effectivePrice: number | null;
  imageId: string | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface ProductImage {
  id: string;
  mediaAssetId: string;
  url: string;
  altText: string | null;
  sortOrder: number;
  isPrimary: boolean;
}

/** Fila del listado de administración. */
export interface ProductListItem {
  id: string;
  name: string;
  slug: string;
  brandName: string | null;
  /** Nulo es «consultar precio». Cero es gratis. No son lo mismo. */
  listPrice: number | null;
  isPublic: boolean;
  isActive: boolean;
}

/** Ficha completa, para administrar. */
export interface Product {
  id: string;
  name: string;
  slug: string;
  shortDescription: string | null;
  description: string | null;
  primaryCategoryId: string | null;
  brandId: string | null;
  listPrice: number | null;
  saleUnit: string | null;
  variantLabel: string | null;
  isPublic: boolean;
  isActive: boolean;
  categoryIds: string[];
  /** Siempre al menos una. La interfaz decide qué enseñar según cuántas hay. */
  items: ProductItem[];
  images: ProductImage[];
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface ProductQuery {
  [key: string]: string | number | boolean | undefined;
  q?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

/** Alta. Nunca menciona una variante: el servidor crea la única. */
export interface CreateProduct {
  name: string;
  slug: string | null;
  shortDescription: string | null;
  description: string | null;
  primaryCategoryId: string | null;
  categoryIds: string[];
  brandId: string | null;
  listPrice: number | null;
  saleUnit: string | null;
  variantLabel: string | null;
  code: string | null;
  barcode: string | null;
}

/**
 * Modificación del producto.
 *
 * Lleva `code` y `barcode` **solo si el producto tiene una presentación**:
 * ver el comentario de `update`. Con varias, `singleVariantFieldsPresent` va
 * en `false` y esos campos no viajan.
 */
export interface UpdateProduct {
  name: string;
  slug: string;
  shortDescription: string | null;
  description: string | null;
  brandId: string | null;
  listPrice: number | null;
  saleUnit: string | null;
  variantLabel: string | null;
  isPublic: boolean;
  isActive: boolean;
  code: string | null;
  barcode: string | null;
  singleVariantFieldsPresent: boolean;
}

const BASE = '/admin/catalog/products';

export const productsService = {
  list: (query: ProductQuery) => http.get<Paged<ProductListItem>>(BASE, { query }),

  get: (id: string) => http.get<Product>(`${BASE}/${encodeURIComponent(id)}`),

  create: (product: CreateProduct) => http.post<Product>(BASE, product),

  deactivate: (id: string) => http.delete<Product>(`${BASE}/${encodeURIComponent(id)}`),

  /**
   * Guarda el producto y, **si solo tiene una variante**, también sus campos.
   *
   * Dos peticiones porque el contrato son dos, no porque la pantalla quiera
   * dos. Con más de una variante no se toca ninguna: entonces esos campos ya
   * no son del producto y los edita la tabla de variantes (04D).
   *
   * **Una sola petición, atómica.** Antes eran dos —el contrato del producto
   * no aceptaba el código ni el código de barras— y un choque de código
   * dejaba media edición aplicada. El `PUT` los acepta ahora **solo cuando
   * hay exactamente una presentación**; con varias los rechaza con una frase,
   * porque entonces son de cada una y aplicarlos a la primera sería inventar.
   */
  update: (id: string, product: UpdateProduct) =>
    http.put<Product>(`${BASE}/${encodeURIComponent(id)}`, product),

  /**
   * Fija el conjunto de categorías del producto y cuál es la principal.
   *
   * **Sustituye al conjunto anterior**, no acumula: es lo que hace el
   * servidor y lo que la pantalla enseña. La principal tiene que estar entre
   * ellas (regla 6) — la interfaz lo garantiza, y el servidor lo vuelve a
   * comprobar, que es lo correcto.
   */
  setCategories: (id: string, categoryIds: readonly string[], primaryCategoryId: string | null) =>
    http.put<Product>(`${BASE}/${encodeURIComponent(id)}/categories`, {
      categoryIds,
      primaryCategoryId,
    }),

  /**
   * Sincroniza las presentaciones de un producto con lo que dice la tabla.
   *
   * Se hace campo a campo con los endpoints que ya existen: crear las nuevas,
   * actualizar las que cambiaron y desactivar las que se quitaron. **No es
   * atómico**, y con el contrato de hoy no puede serlo — pero cada operación
   * es de una presentación, así que un fallo deja el resto en pie y dice cuál
   * falló, en vez de dejar media tabla sin saber cuál.
   */
  async saveVariants(
    productId: string,
    filas: readonly {
      id: string | null;
      variantValue: string | null;
      code: string | null;
      barcode: string | null;
      priceOverride: number | null;
    }[],
    anteriores: readonly ProductItem[],
  ): Promise<void> {
    const vivas = new Set(filas.map((fila) => fila.id).filter((id): id is string => id !== null));

    for (const fila of filas) {
      if (fila.id === null) {
        await http.post(`${BASE}/${encodeURIComponent(productId)}/items`, {
          variantValue: fila.variantValue,
          code: fila.code,
          barcode: fila.barcode,
          priceOverride: fila.priceOverride,
          imageId: null,
        });
        continue;
      }

      const antes = anteriores.find((item) => item.id === fila.id);

      await http.put(`/admin/catalog/items/${encodeURIComponent(fila.id)}`, {
        variantValue: fila.variantValue,
        code: fila.code,
        barcode: fila.barcode,
        priceOverride: fila.priceOverride,
        imageId: antes?.imageId ?? null,
        sortOrder: antes?.sortOrder ?? 0,
        isActive: true,
      });
    }

    // Las que ya no están en la tabla se dan de baja. Si es la última activa
    // el servidor lo impide con su frase, que propone desactivar el producto.
    for (const antes of anteriores) {
      if (!vivas.has(antes.id)) {
        await http.delete(`/admin/catalog/items/${encodeURIComponent(antes.id)}`);
      }
    }
  },

  /** Asocia una imagen de la galería de CORE. */
  addImage: (id: string, mediaAssetId: string, altText: string | null) =>
    http.post<Product>(`${BASE}/${encodeURIComponent(id)}/images`, {
      mediaAssetId,
      altText,
      isPrimary: false,
    }),

  removeImage: (id: string, imageId: string) =>
    http.delete<Product>(`${BASE}/${encodeURIComponent(id)}/images/${encodeURIComponent(imageId)}`),

  reorderImages: (id: string, orderedImageIds: string[], primaryImageId: string | null) =>
    http.put<Product>(`${BASE}/${encodeURIComponent(id)}/images/order`, {
      orderedImageIds,
      primaryImageId,
    }),
};

/**
 * Un precio, tal como se escribe y se lee en un formulario.
 *
 * **Nulo y cero no son lo mismo y no se pueden parecer.** Nulo es «consultar
 * precio» —un plato de menú del día, algo que se cotiza—; cero es gratis. En
 * un campo numérico los dos se ven igual de vacíos si nadie lo impide, y ese
 * es el error que el SPEC señala como el más fácil de cometer.
 *
 * La cadena vacía significa nulo. Un «0» escrito significa cero.
 */
export function priceToInput(value: number | null): string {
  return value === null ? '' : String(value);
}

export function priceFromInput(text: string): number | null {
  const trimmed = text.trim();
  return trimmed === '' ? null : Number(trimmed);
}

/** Cómo se lee un precio en una lista, sin confundir los dos casos. */
export function priceLabel(value: number | null): string {
  if (value === null) {
    return 'Consultar';
  }

  return value === 0
    ? 'Gratis'
    : value.toLocaleString('es-PE', { style: 'currency', currency: 'PEN' });
}
