import { http } from '../../../shared/http/client';
import type { ReorderCmsRequest } from './contracts';

/** Trabajo que el backend ya decidió publicar. */
export interface PublicFeaturedProject {
  id: number;
  title: string;
  description: string | null;
  imageUrl: string;
  altText: string;
}

/** Trabajo visto desde administración. */
export interface FeaturedProjectAdmin {
  id: number;
  title: string;
  description: string | null;
  imageId: string | null;
  imageUrl: string | null;
  altText: string | null;
  displayOrder: number;
  isActive: boolean;
  isComplete: boolean;
}

export interface CreateFeaturedProject {
  title: string | null;
  description: string | null;
  imageId: string | null;
  altText: string | null;
}

export interface UpdateFeaturedProject {
  title: string | null;
  description: string | null;
  imageId: string | null;
  altText: string | null;
}

const PUBLIC_BASE = '/cms/featured-projects';
const ADMIN_BASE = '/admin/cms/featured-projects';

export const publicFeaturedProjectsService = {
  list: () => http.get<PublicFeaturedProject[]>(PUBLIC_BASE),
};

export const featuredProjectsService = {
  list: () => http.get<FeaturedProjectAdmin[]>(ADMIN_BASE),

  get: (id: number) => http.get<FeaturedProjectAdmin>(`${ADMIN_BASE}/${id}`),

  create: (project: CreateFeaturedProject) =>
    http.post<FeaturedProjectAdmin>(ADMIN_BASE, project),

  update: (id: number, project: UpdateFeaturedProject) =>
    http.put<FeaturedProjectAdmin>(`${ADMIN_BASE}/${id}`, project),

  /** El payload contiene el conjunto completo; construirlo pertenece a la página. */
  reorder: (request: ReorderCmsRequest) =>
    http.put<number[]>(`${ADMIN_BASE}/order`, request),

  deactivate: (id: number) =>
    http.delete<FeaturedProjectAdmin>(`${ADMIN_BASE}/${id}`),
};
