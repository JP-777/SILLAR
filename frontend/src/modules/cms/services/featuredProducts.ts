import { http } from '../../../shared/http/client';
import type { PublicationState, ReorderCmsRequest } from './contracts';

/** Producto destacado que el backend ya decidió publicar desde su snapshot. */
export interface PublicFeaturedProduct {
  id: number;
  productName: string;
  productSlug: string | null;
  imageUrl: string | null;
  /** null = consultar, 0 = gratis, positivo = importe. */
  productPrice: number | null;
  productPriceVaries: boolean;
  productCategory: string | null;
  productIsPublic: boolean;
  productIsActive: boolean;
}

/** Snapshot editorial visible aunque el producto ya no exista en Catálogo. */
export interface FeaturedProductAdmin {
  id: number;
  /** null significa vínculo perdido y pendiente de volver a enlazar. */
  productId: string | null;
  productName: string;
  productSlug: string | null;
  imageId: string | null;
  imageUrl: string | null;
  /** null = consultar, 0 = gratis, positivo = importe. */
  productPrice: number | null;
  productPriceVaries: boolean;
  productCategory: string | null;
  productIsPublic: boolean;
  /** Estado del producto de Catálogo, no del destacado editorial. */
  productIsActive: boolean;
  displayOrder: number;
  startsAt: string | null;
  endsAt: string | null;
  /** Estado del destacado editorial de CMS. */
  isActive: boolean;
  /** Compatibilidad con el contrato existente; la pantalla usa publicationState. */
  isCurrent: boolean;
  publicationState: PublicationState;
  pendingRelink: boolean;
}

export interface CreateFeaturedProduct {
  productId: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

/** Solo modifica la vigencia; nunca altera ni reactiva el snapshot. */
export interface UpdateFeaturedProduct {
  startsAt: string | null;
  endsAt: string | null;
}

export interface RelinkFeaturedProduct {
  productId: string | null;
}

/** Producto activo de Catálogo disponible para el selector. */
export interface FeaturedProductPickerItem {
  productId: string;
  name: string;
  slug: string;
  imageUrl: string | null;
  primaryCategoryName: string | null;
  /** null = consultar, 0 = gratis, positivo = importe. */
  price: number | null;
  priceVaries: boolean;
  isPublic: boolean;
  isActive: boolean;
}

export interface FeaturedProductPickerQuery {
  [key: string]: string | number | undefined;
  q?: string;
  limit?: number;
}

export interface FeaturedProductRefreshResult {
  refreshedCount: number;
  pendingRelinkCount: number;
}

const PUBLIC_BASE = '/cms/featured-products';
const ADMIN_BASE = '/admin/cms/featured-products';

export const publicFeaturedProductsService = {
  list: () => http.get<PublicFeaturedProduct[]>(PUBLIC_BASE),
};

/** Operaciones disponibles aunque Catálogo esté inactivo. */
export const featuredProductsService = {
  list: () => http.get<FeaturedProductAdmin[]>(ADMIN_BASE),

  get: (id: number) => http.get<FeaturedProductAdmin>(`${ADMIN_BASE}/${id}`),

  update: (id: number, product: UpdateFeaturedProduct) =>
    http.put<FeaturedProductAdmin>(`${ADMIN_BASE}/${id}`, product),

  /** El payload contiene el conjunto completo; construirlo pertenece a la página. */
  reorder: (request: ReorderCmsRequest) =>
    http.put<number[]>(`${ADMIN_BASE}/order`, request),

  deactivate: (id: number) =>
    http.delete<FeaturedProductAdmin>(`${ADMIN_BASE}/${id}`),
};

/**
 * Operaciones que el backend solo monta cuando está disponible el contrato de Catálogo.
 * La capacidad guía si se ofrecen; la autorización real continúa en el backend.
 */
export const featuredProductsCatalogService = {
  search: (query: FeaturedProductPickerQuery) =>
    http.get<FeaturedProductPickerItem[]>(`${ADMIN_BASE}/catalog`, { query }),

  create: (product: CreateFeaturedProduct) =>
    http.post<FeaturedProductAdmin>(ADMIN_BASE, product),

  relink: (id: number, product: RelinkFeaturedProduct) =>
    http.put<FeaturedProductAdmin>(`${ADMIN_BASE}/${id}/relink`, product),

  refresh: (id: number) =>
    http.put<FeaturedProductAdmin>(`${ADMIN_BASE}/${id}/refresh`),

  refreshAll: () =>
    http.put<FeaturedProductRefreshResult>(`${ADMIN_BASE}/refresh`),
};
