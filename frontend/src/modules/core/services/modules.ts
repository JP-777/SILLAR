import { http } from '../../../shared/http/client';

/**
 * Capa de servicios del módulo CORE, parte de módulos.
 *
 * Nada de `fetch` suelto en componentes: todo pasa por aquí y por el cliente
 * compartido.
 */

/** Un módulo del catálogo, tal como lo devuelve `GET /api/admin/modules`. */
export interface AdminModule {
  code: string;
  displayName: string;
  description: string;
  version: string;
  isCore: boolean;
  isActive: boolean;
  activatedAt: string | null;
  deactivatedAt: string | null;
  /** Vencimiento de la licencia. Se muestra pero **no se evalúa** (fase 5). */
  expiresAt: string | null;
  displayOrder: number;
  hardDependencies: string[];
  softDependencies: string[];
  canActivate: boolean;
  canDeactivate: boolean;
  /**
   * Qué lo impide: las dependencias duras inactivas si no se puede activar, o
   * los módulos activos que dependen de él si no se puede desactivar.
   *
   * Lo calcula el servidor. El frontend **no rehace el análisis del grafo**: si
   * lo hiciera, habría dos implementaciones de la misma regla y una se quedaría
   * atrás.
   */
  blockedBy: string[];
  /**
   * Si esta instalación reinicia el host sola tras activar o desactivar.
   *
   * Igual en las seis tarjetas: es un dato del despliegue, no del módulo. El
   * diálogo de confirmación lo usa para no prometer un reinicio automático que
   * esta instalación no hace — se muestra antes de la operación, así que no
   * puede leerlo de su respuesta todavía.
   */
  restartsAutomatically: boolean;
}

/** Resultado de activar o desactivar. */
export interface ModuleActivationResult {
  code: string;
  isActive: boolean;
  /** `scheduled`, `required` o `none`. */
  restart: 'scheduled' | 'required' | 'none';
  message: string;
}

export const modulesService = {
  list: () => http.get<AdminModule[]>('/admin/modules'),

  activate: (code: string) =>
    http.post<ModuleActivationResult>(`/admin/modules/${encodeURIComponent(code)}/activate`),

  deactivate: (code: string) =>
    http.post<ModuleActivationResult>(`/admin/modules/${encodeURIComponent(code)}/deactivate`),
};
