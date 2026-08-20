import { http } from '../http/client';

/**
 * Acceso a la galería de medios de CORE, para cualquier módulo que necesite
 * elegir una imagen.
 *
 * **Por qué vive en `shared/` y no en un módulo.** Un módulo no importa de
 * otro. Marcas fue el primer caso y declaró lo suyo; categorías es el
 * segundo, y productos será el tercero — que es exactamente cuando toca
 * extraer en vez de copiar. Copiar en el segundo se convierte en silencio en
 * copiar en el tercero y el cuarto, y para entonces hay tres versiones que ya
 * divergieron.
 *
 * CORE es dependencia dura de los módulos que la usan, así que llamar a su
 * **API** es legítimo; lo que no se cruza es el código.
 */

/** Una imagen de la galería, con lo justo para elegirla. */
export interface GalleryImage {
  /** `uuid`. Viaja en el contrato y **nunca se muestra en pantalla**. */
  mediaAssetId: string;
  url: string;
  originalName: string | null;
  altText: string | null;
}

interface Paged<T> {
  items: T[];
  totalItems: number;
  totalPages: number;
}

export const galleryService = {
  /** Imágenes activas, la primera página. Suficiente para elegir una. */
  list: (page = 1) =>
    http.get<Paged<GalleryImage>>('/admin/media', { query: { page, pageSize: 24 } }),
};
