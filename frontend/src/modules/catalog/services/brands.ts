import { http } from '../../../shared/http/client';

/**
 * Capa de servicios de marcas.
 *
 * Nada de `fetch` suelto en componentes: todo pasa por aquí y por el cliente
 * compartido, que es quien pone la cookie de sesión, el token CSRF y el
 * respeto al estado de la conexión.
 */

/** Una marca, tal como la devuelve `GET /api/admin/catalog/brands`. */
export interface Brand {
  id: string;
  name: string;
  /** Para la URL pública. **No se recalcula al cambiar el nombre** (regla 3 del SPEC). */
  slug: string;
  /** Logotipo en `core.media_assets`, o `null`. Nunca se muestra al usuario. */
  logoId: string | null;
  /** El mismo logotipo ya resuelto a una URL servible. */
  logoUrl: string | null;
  isActive: boolean;
}

/** Alta. El slug es opcional: si falta, el servidor lo genera del nombre. */
export interface CreateBrand {
  name: string;
  slug: string | null;
  logoId: string | null;
}

/** Modificación. Aquí el slug es obligatorio y viaja tal cual. */
export interface UpdateBrand {
  name: string;
  slug: string;
  logoId: string | null;
  isActive: boolean;
}

const BASE = '/admin/catalog/brands';

export const brandsService = {
  list: () => http.get<Brand[]>(BASE),

  create: (brand: CreateBrand) => http.post<Brand>(BASE, brand),

  update: (id: string, brand: UpdateBrand) =>
    http.put<Brand>(`${BASE}/${encodeURIComponent(id)}`, brand),

  /** Baja lógica. Sus productos siguen existiendo y no pierden la marca. */
  deactivate: (id: string) => http.delete<Brand>(`${BASE}/${encodeURIComponent(id)}`),
};
