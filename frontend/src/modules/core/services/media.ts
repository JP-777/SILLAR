import { http } from '../../../shared/http/client';
import type { Page } from './audit';

/** Un archivo, tal como lo lista la galería. */
export interface MediaAsset {
  mediaAssetId: number;
  url: string;
  originalName: string | null;
  mimeType: string;
  sizeBytes: number;
  width: number | null;
  height: number | null;
  altText: string | null;
  ownerModuleCode: string | null;
  /** Su módulo ya no está instalado. */
  isOrphan: boolean;
  isActive: boolean;
  createdAt: string;
}

/** Resultado de una subida. */
export interface MediaUploadResult {
  mediaAssetId: number;
  url: string;
  originalName: string | null;
  mimeType: string;
  sizeBytes: number;
  width: number | null;
  height: number | null;
  /**
   * Identificador de un archivo activo con el mismo contenido.
   *
   * **No es un error**: el archivo se subió. La entrega 3b decidió detectar
   * duplicados sin fusionarlos, así que esto es un aviso.
   */
  duplicateOf: number | null;
}

export type MediaQuery = {
  ownerModuleCode?: string;
  isOrphan?: boolean;
  mimeType?: string;
  from?: string;
  to?: string;
  includeInactive?: boolean;
  page?: number;
  pageSize?: number;
}

/** Tipos que el servidor acepta. */
export const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

/** Tamaño máximo, el mismo que aplica el servidor. */
export const MAX_SIZE_BYTES = 5 * 1024 * 1024;

export const mediaService = {
  list: (query: MediaQuery) => http.get<Page<MediaAsset>>('/admin/media', { query }),

  upload: (file: File, ownerModuleCode: string, altText?: string) => {
    const form = new FormData();
    form.append('file', file);
    form.append('ownerModuleCode', ownerModuleCode);

    if (altText) {
      form.append('altText', altText);
    }

    // El token CSRF lo pone el cliente compartido: subir un archivo es una
    // escritura como cualquier otra y multipart no exime.
    return http.upload<MediaUploadResult>('/admin/media', form);
  },

  /** Baja lógica. El binario se conserva, pero deja de servirse. */
  deactivate: (id: number) => http.delete<void>(`/admin/media/${id}`),
};

/**
 * Comprueba el archivo antes de enviarlo.
 *
 * **Es cortesía, no control.** La validación que manda es la del servidor, que
 * mira los bytes iniciales del contenido; esta solo mira lo que el navegador
 * declara, que quien sube puede falsear. Sirve para no esperar a que suban 5 MB
 * para nada.
 *
 * @returns El motivo del rechazo, o `null` si conviene intentarlo.
 */
export function precheck(file: File): string | null {
  if (file.size > MAX_SIZE_BYTES) {
    return `«${file.name}» pesa ${formatSize(file.size)} y el máximo son 5 MB.`;
  }

  if (file.size === 0) {
    return `«${file.name}» está vacío.`;
  }

  // El SVG se nombra por su formato: quien sube un logo vectorial merece saber
  // que es *ese* formato el que no entra, no leer una lista y deducirlo.
  if (file.type === 'image/svg+xml' || file.name.toLowerCase().endsWith('.svg')) {
    return 'Los archivos SVG no se admiten por seguridad. Conviértelo a PNG o WebP.';
  }

  if (file.type && !ACCEPTED_TYPES.includes(file.type as (typeof ACCEPTED_TYPES)[number])) {
    return `«${file.name}» es ${file.type}. Se aceptan JPEG, PNG y WebP.`;
  }

  return null;
}

/** Tamaño legible. */
export function formatSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const kb = bytes / 1024;

  return kb < 1024 ? `${Math.round(kb)} kB` : `${(kb / 1024).toFixed(1)} MB`;
}
