import { http } from '../../../shared/http/client';
import type { PublicationState, ReorderCmsRequest } from './contracts';

/** Banner que el backend ya decidió publicar. */
export interface PublicBanner {
  id: number;
  title: string | null;
  subtitle: string | null;
  imageDesktopUrl: string;
  imageMobileUrl: string | null;
  altText: string;
  linkUrl: string | null;
  linkLabel: string | null;
}

/** Banner visto desde administración. Las fechas permanecen como cadenas ISO. */
export interface BannerAdmin {
  id: number;
  title: string | null;
  subtitle: string | null;
  imageDesktopId: string | null;
  imageDesktopUrl: string | null;
  imageMobileId: string | null;
  imageMobileUrl: string | null;
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
  isComplete: boolean;
}

export interface CreateBanner {
  title: string | null;
  subtitle: string | null;
  imageDesktopId: string | null;
  imageMobileId: string | null;
  altText: string | null;
  linkUrl: string | null;
  linkLabel: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

export interface UpdateBanner {
  title: string | null;
  subtitle: string | null;
  imageDesktopId: string | null;
  imageMobileId: string | null;
  altText: string | null;
  linkUrl: string | null;
  linkLabel: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

const PUBLIC_BASE = '/cms/banners';
const ADMIN_BASE = '/admin/cms/banners';

export const publicBannersService = {
  list: () => http.get<PublicBanner[]>(PUBLIC_BASE),
};

export const bannersService = {
  list: () => http.get<BannerAdmin[]>(ADMIN_BASE),

  get: (id: number) => http.get<BannerAdmin>(`${ADMIN_BASE}/${id}`),

  create: (banner: CreateBanner) => http.post<BannerAdmin>(ADMIN_BASE, banner),

  update: (id: number, banner: UpdateBanner) =>
    http.put<BannerAdmin>(`${ADMIN_BASE}/${id}`, banner),

  /** El payload contiene el conjunto completo; construirlo pertenece a la página. */
  reorder: (request: ReorderCmsRequest) =>
    http.put<number[]>(`${ADMIN_BASE}/order`, request),

  deactivate: (id: number) => http.delete<BannerAdmin>(`${ADMIN_BASE}/${id}`),
};
