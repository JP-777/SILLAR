import { http } from '../../../shared/http/client';
import type { Role } from '../../../session';
export interface ModulePermission {
  moduleCode: string; moduleName: string; operationCode: string; operationName: string;
  minimumRole: Role; allowed: boolean | null; effective: boolean;
}
export const modulePermissionsService = {
  list: (id: number) => http.get<ModulePermission[]>(`/admin/users/${id}/module-permissions`),
  set: (id: number, module: string, operation: string, allowed: boolean | null) =>
    http.put<void>(`/admin/users/${id}/module-permissions/${encodeURIComponent(module)}/${encodeURIComponent(operation)}`, { allowed }),
};
