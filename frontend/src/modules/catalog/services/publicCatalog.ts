import { http } from '../../../shared/http/client';

/**
 * Lo que la tienda pública lee.
 *
 * Sin sesión y sin CSRF: son lecturas anónimas. El servidor solo devuelve lo
 * que está **activo y publicado**, así que la pantalla no filtra nada — y un
 * producto despublicado responde **404, no 403**: contestar «existe pero no
 * puedes» sería decir que existe.
 */

export interface PublicBrand {
  slug: string;
  name: string;
  logoUrl: string | null;
}

/** Nodo del árbol público. Solo trae categorías activas. */
export interface PublicCategory {
  slug: string;
  name: string;
  imageUrl: string | null;
  children: PublicCategory[];
}

export interface PublicCard {
  slug: string;
  name: string;
  shortDescription: string | null;
  primaryImageUrl: string | null;
  /**
   * Lo que cuesta, **ya resuelto por el servidor**: el mínimo efectivo de sus
   * presentaciones activas, no el precio de lista. Nulo es «a consultar».
   * Cero es gratis. Nunca lo mismo.
   */
  price: number | null;
  /**
   * Si las presentaciones no cuestan lo mismo, y por tanto el precio es una
   * cota. La tarjeta no tiene selector de variante: sin decirlo, enseñaría un
   * número que solo se cobra por una de ellas.
   */
  priceVaries: boolean;
}

export interface BreadcrumbItem {
  slug: string;
  name: string;
}

export interface PublicVariant {
  /** Nulo cuando el producto tiene una sola: no hay nada que nombrar. */
  variantValue: string | null;
  code: string | null;
  barcode: string | null;
  price: number | null;
  imageUrl: string | null;
}

export interface PublicProduct {
  slug: string;
  name: string;
  shortDescription: string | null;
  description: string | null;
  brandName: string | null;
  brandSlug: string | null;
  /** Vacía si ninguna categoría suya está activa: nunca un enlace a algo invisible. */
  breadcrumb: BreadcrumbItem[];
  images: { url: string; altText: string | null; isPrimary: boolean }[];
  variants: PublicVariant[];
  saleUnit: string | null;
  /** Cómo se llama lo que varía. Solo significa algo con más de una variante. */
  variantLabel: string | null;
}

/** Lo que devuelve el detalle público de una categoría. Es plano, no anidado. */
export interface CategoryDetail {
  slug: string;
  name: string;
  breadcrumb: BreadcrumbItem[];
  imageUrl: string | null;
  products: PublicPage<PublicCard>;
}

export interface PublicPage<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface PublicQuery {
  [key: string]: string | number | boolean | undefined;
  category?: string;
  brand?: string;
  q?: string;
  page?: number;
  pageSize?: number;
}

export const publicCatalog = {
  categories: () => http.get<PublicCategory[]>('/catalog/categories'),

  brands: () => http.get<PublicBrand[]>('/catalog/brands'),

  products: (query: PublicQuery) => http.get<PublicPage<PublicCard>>('/catalog/products', { query }),

  category: (slug: string, page: number) =>
    http.get<CategoryDetail>(`/catalog/categories/${encodeURIComponent(slug)}`, {
      query: { page, pageSize: 12 },
    }),

  product: (slug: string) => http.get<PublicProduct>(`/catalog/products/${encodeURIComponent(slug)}`),
};

/**
 * Los tres estados del precio, decididos **a partir del dato** y no de una
 * cadena escrita a mano.
 *
 * Solo los dos casos raros se explican. Un número normal no lleva nota: si
 * todo lleva nota, la nota deja de significar algo.
 */
export type PriceKind = 'numero' | 'gratis' | 'consultar';

export function priceKind(value: number | null): PriceKind {
  if (value === null) {
    return 'consultar';
  }

  return value === 0 ? 'gratis' : 'numero';
}

export function formatPrice(value: number): string {
  return value.toLocaleString('es-PE', { style: 'currency', currency: 'PEN' });
}

/**
 * La frase que resume los precios de un grupo de variantes.
 *
 * **Sale del dato.** Escrita a mano —«todas cuestan lo mismo»— miente el día
 * que alguien le ponga a una un precio distinto, y nadie se entera.
 */
export function variantPriceNote(variants: readonly PublicVariant[]): string | null {
  if (variants.length < 2) {
    return null;
  }

  const distintos = new Set(variants.map((variant) => variant.price));

  return distintos.size === 1 ? 'Todas cuestan lo mismo.' : 'Cada opción tiene su propio precio.';
}
