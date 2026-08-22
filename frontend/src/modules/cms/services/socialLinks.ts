import { http } from '../../../shared/http/client';
import type { ReorderCmsRequest } from './contracts';

/** Enlace social activo publicado para el footer. */
export interface PublicSocialLink {
  id: number;
  platform: string;
  url: string;
}

/** Enlace social visto desde administración. */
export interface SocialLinkAdmin {
  id: number;
  platform: string;
  url: string;
  displayOrder: number;
  isActive: boolean;
}

export interface CreateSocialLink {
  platform: string | null;
  url: string | null;
}

/** Editar no cambia el estado activo; reactivar es una operación admin separada. */
export interface UpdateSocialLink {
  platform: string | null;
  url: string | null;
}

const PUBLIC_BASE = '/cms/social-links';
const ADMIN_BASE = '/admin/cms/social-links';

export const publicSocialLinksService = {
  list: () => http.get<PublicSocialLink[]>(PUBLIC_BASE),
};

export const socialLinksService = {
  list: () => http.get<SocialLinkAdmin[]>(ADMIN_BASE),

  get: (id: number) => http.get<SocialLinkAdmin>(`${ADMIN_BASE}/${id}`),

  create: (link: CreateSocialLink) => http.post<SocialLinkAdmin>(ADMIN_BASE, link),

  update: (id: number, link: UpdateSocialLink) =>
    http.put<SocialLinkAdmin>(`${ADMIN_BASE}/${id}`, link),

  /** El payload contiene el conjunto completo; construirlo pertenece a la página. */
  reorder: (request: ReorderCmsRequest) =>
    http.put<number[]>(`${ADMIN_BASE}/order`, request),

  deactivate: (id: number) => http.delete<SocialLinkAdmin>(`${ADMIN_BASE}/${id}`),

  /** Operación admin separada de editar; conserva identidad, contenido y orden. */
  reactivate: (id: number) =>
    http.put<SocialLinkAdmin>(`${ADMIN_BASE}/${id}/reactivate`),
};
