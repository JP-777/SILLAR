import { anonymousHttp, http } from '../../../shared/http/client';

export type PublicationState = 'Draft' | 'Published' | 'Archived';

export interface PublicService {
  id: number;
  name: string;
  slug: string;
  shortDescription: string | null;
  description: string | null;
  price: number | null;
  saleUnit: string | null;
  imageUrl: string | null;
  imageAltText: string | null;
}

export interface AdminService extends PublicService {
  imageId: string | null;
  publicationState: PublicationState;
  displayOrder: number;
}

export interface SaveServiceRequest {
  name: string;
  slug: string;
  shortDescription: string | null;
  description: string | null;
  price: number | null;
  saleUnit: string | null;
  imageId: string | null;
  imageAltText: string | null;
}

export const servicesService = {
  listPublic: () => anonymousHttp.get<PublicService[]>('/services'),
  getPublic: (slug: string) => anonymousHttp.get<PublicService>(`/services/${encodeURIComponent(slug)}`),
  listAdmin: () => http.get<AdminService[]>('/admin/services'),
  create: (request: SaveServiceRequest) => http.post<AdminService>('/admin/services', request),
  update: (id: number, request: SaveServiceRequest) => http.put<AdminService>(`/admin/services/${id}`, request),
  publish: (id: number) => http.post<AdminService>(`/admin/services/${id}/publish`),
  unpublish: (id: number) => http.post<AdminService>(`/admin/services/${id}/unpublish`),
  archive: (id: number) => http.post<AdminService>(`/admin/services/${id}/archive`),
  reorder: (orderedIds: number[]) => http.put<AdminService[]>('/admin/services/order', { orderedIds }),
};

export function formatServicePrice(price: number | null): string {
  return price === null
    ? 'Precio a consultar'
    : new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'PEN' }).format(price);
}
