import { http } from '../../../shared/http/client';

/** Una entrada del registro de auditoría. */
export interface AuditEntry {
  id: number;
  occurredAt: string;
  /** Nulo si la cuenta fue eliminada, o si actuó el sistema. */
  adminUserId: number | null;
  /**
   * Correo de quien actuó, guardado como snapshot.
   *
   * Sobrevive al borrado de la cuenta: un registro de auditoría que pierde la
   * identidad de quien actuó no sirve de nada.
   */
  adminUserEmail: string | null;
  moduleCode: string | null;
  entityType: string | null;
  entityId: string | null;
  action: string;
  summary: string | null;
  ipAddress: string | null;
}

/** Una página de resultados, con la paginación de `Sillar.Shared`. */
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNext: boolean;
}

export type AuditQuery = {
  from?: string;
  to?: string;
  adminUserId?: number;
  moduleCode?: string;
  action?: string;
  page?: number;
  pageSize?: number;
}

/** Acciones que registra el sistema, con su nombre visible. */
export const AUDIT_ACTIONS: readonly { value: string; label: string }[] = [
  { value: 'create', label: 'Alta' },
  { value: 'update', label: 'Modificación' },
  { value: 'delete', label: 'Baja' },
  { value: 'activate', label: 'Activación de módulo' },
  { value: 'deactivate', label: 'Desactivación de módulo' },
  { value: 'login', label: 'Acceso' },
  { value: 'login_failed', label: 'Acceso fallido' },
  { value: 'logout', label: 'Cierre de sesión' },
  { value: 'setup', label: 'Instalación' },
];

/** Nombre visible de una acción, o el código si no se reconoce. */
export function actionLabel(action: string): string {
  return AUDIT_ACTIONS.find((known) => known.value === action)?.label ?? action;
}

/**
 * Consulta del registro.
 *
 * **Solo lectura.** No hay alta, ni modificación, ni baja: los registros se
 * escriben desde dentro del backend y no se editan ni se borran desde el API
 * (SPEC §8.15). Que este servicio solo sepa consultar es parte de esa garantía.
 */
export const auditService = {
  query: (query: AuditQuery) => http.get<Page<AuditEntry>>('/admin/audit', { query }),
};
