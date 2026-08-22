import { http } from '../../../shared/http/client';
import type { PublicationState, ReorderCmsRequest } from './contracts';

/** Promoción que el backend ya decidió publicar. */
export interface PublicPromotion {
  id: number;
  title: string | null;
  subtitle: string | null;
  description: string | null;
  badgeText: string | null;
  imageUrl: string | null;
  altText: string | null;
  linkUrl: string | null;
  linkLabel: string | null;
}

/** Promoción vista desde administración. Las fechas permanecen como cadenas ISO. */
export interface PromotionAdmin {
  id: number;
  title: string | null;
  subtitle: string | null;
  description: string | null;
  badgeText: string | null;
  imageId: string | null;
  imageUrl: string | null;
  altText: string | null;
  linkUrl: string | null;
  linkLabel: string | null;
  displayOrder: number;
  startsAt: string | null;
  endsAt: string | null;
  isActive: boolean;
  /** Compatibilidad con el contrato existente; la pantalla usa publicationState. */
  isCurrent: boolean;
  publicationState: PublicationState;
}

export interface CreatePromotion {
  title: string | null;
  subtitle: string | null;
  description: string | null;
  badgeText: string | null;
  imageId: string | null;
  altText: string | null;
  linkUrl: string | null;
  linkLabel: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

export interface UpdatePromotion {
  title: string | null;
  subtitle: string | null;
  description: string | null;
  badgeText: string | null;
  imageId: string | null;
  altText: string | null;
  linkUrl: string | null;
  linkLabel: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

const PUBLIC_BASE = '/cms/promotions';
const ADMIN_BASE = '/admin/cms/promotions';

export const publicPromotionsService = {
  list: () => http.get<PublicPromotion[]>(PUBLIC_BASE),
};

export const promotionsService = {
  list: () => http.get<PromotionAdmin[]>(ADMIN_BASE),

  get: (id: number) => http.get<PromotionAdmin>(`${ADMIN_BASE}/${id}`),

  create: (promotion: CreatePromotion) =>
    http.post<PromotionAdmin>(ADMIN_BASE, promotion),

  update: (id: number, promotion: UpdatePromotion) =>
    http.put<PromotionAdmin>(`${ADMIN_BASE}/${id}`, promotion),

  /** El payload contiene el conjunto completo; construirlo pertenece a la página. */
  reorder: (request: ReorderCmsRequest) =>
    http.put<number[]>(`${ADMIN_BASE}/order`, request),

  deactivate: (id: number) => http.delete<PromotionAdmin>(`${ADMIN_BASE}/${id}`),
};
