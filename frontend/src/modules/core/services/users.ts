import { http } from '../../../shared/http/client';
import type { Role } from '../../../session';

/** Un administrador, tal como lo lista el panel. Nunca incluye el hash. */
export interface AdminUser {
  id: number;
  fullName: string;
  email: string;
  role: Role;
  phone: string | null;
  isActive: boolean;
  lastLoginAt: string | null;
  lockedUntil: string | null;
}

export interface CreateAdminUser {
  fullName: string;
  email: string;
  password: string;
  role: Role;
  phone: string | null;
}

export interface UpdateAdminUser {
  fullName: string;
  role: Role;
  phone: string | null;
  isActive: boolean;
}

/** Una sesión abierta, para poder listarlas y cerrarlas desde el panel. */
export interface AdminSession {
  id: string;
  adminUserId: number;
  email: string;
  issuedAt: string;
  lastSeenAt: string;
  expiresAt: string;
  revokedAt: string | null;
  ipAddress: string | null;
  userAgent: string | null;
}

export const usersService = {
  list: () => http.get<AdminUser[]>('/admin/users'),

  create: (user: CreateAdminUser) => http.post<AdminUser>('/admin/users', user),

  update: (id: number, user: UpdateAdminUser) => http.put<AdminUser>(`/admin/users/${id}`, user),

  /** Baja lógica. El backend revoca además sus sesiones. */
  deactivate: (id: number) => http.delete<AdminUser>(`/admin/users/${id}`),

  listSessions: () => http.get<AdminSession[]>('/admin/sessions'),

  revokeSession: (id: string) => http.delete<void>(`/admin/sessions/${encodeURIComponent(id)}`),
};

/** Cambio de la contraseña propia. Acción sobre uno mismo, no administración. */
export const accountService = {
  changePassword: (currentPassword: string, newPassword: string) =>
    http.post<void>('/admin/auth/change-password', { currentPassword, newPassword }),
};
