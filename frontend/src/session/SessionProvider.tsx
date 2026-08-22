import { createContext, useCallback, useMemo, useState, type ReactNode } from 'react';
import { http } from '../shared/http/client';

/** Roles administrativos, de menor a mayor. */
export const ROLES = ['editor', 'admin', 'super_admin'] as const;

export type Role = (typeof ROLES)[number];

/** Usuario en sesión. Nunca incluye el hash de la contraseña. */
export interface AuthenticatedUser {
  id: number;
  fullName: string;
  email: string;
  role: Role;
}

interface LoginResponse {
  user: AuthenticatedUser;
  csrfToken: string;
}

export interface SessionValue {
  user: AuthenticatedUser | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<AuthenticatedUser>;
  logout: () => Promise<void>;
  /** Vuelve a leer la sesión del servidor. La usa la reconexión. */
  refresh: () => Promise<void>;
  /** Limpia la sesión en memoria, sin llamar al servidor. */
  clear: () => void;
  /** Comprueba el rol respetando la jerarquía. */
  hasRole: (minimum: Role) => boolean;
}

export const SessionContext = createContext<SessionValue | null>(null);

/**
 * Recupera la sesión del servidor.
 *
 * Un 401 aquí es el caso corriente —nadie ha entrado todavía—, no un error: por
 * eso se pide con `allowUnauthorized` y no dispara la redirección al login.
 */
export async function fetchSession(): Promise<AuthenticatedUser | null> {
  try {
    // **Responde siempre 200**, con el usuario o con nulo: preguntar «quién
    // soy» sin sesión no es un error, y un 401 aquí dejaba un error de consola
    // en cada visita a la tienda.
    const user = await http.get<AuthenticatedUser | null>('/admin/auth/me', {
      allowUnauthorized: true,
    });

    // Falsy y no `=== null`: un cuerpo vacío llega como `undefined`, y
    // «no hay sesión» es lo mismo de las dos formas.
    if (!user) {
      http.setCsrfToken(null);
      return null;
    }

    // El token CSRF también hay que recuperarlo: vive en memoria y una recarga
    // se la lleva. Desde la entrega 2.1 pedirlo es idempotente y devuelve
    // siempre el mismo valor (ADR-012), así que esto no invalida nada ni pelea
    // con otras pestañas.
    //
    // **Solo si hay sesión.** Sin ella devolvería 401, que es correcto —el
    // token es de una sesión— pero pedirlo sabiendo que no la hay es provocar
    // un error a propósito.
    const { csrfToken } = await http.get<{ csrfToken: string }>('/admin/auth/csrf', {
      allowUnauthorized: true,
    });

    http.setCsrfToken(csrfToken);

    return user;
  } catch {
    http.setCsrfToken(null);
    return null;
  }
}

/**
 * Sesión en memoria.
 *
 * Ni `localStorage` ni `sessionStorage`. El token de sesión es una cookie
 * `httpOnly` que JavaScript no debe tocar, y guardar el CSRF en el
 * almacenamiento del navegador desharía parte de esa protección: un XSS que no
 * puede leer la cookie sí podría leer el almacenamiento.
 *
 * Al recargar se recupera del servidor, que es donde vive la verdad.
 */
export function SessionProvider({
  initialUser,
  children,
}: {
  initialUser: AuthenticatedUser | null;
  children: ReactNode;
}) {
  const [user, setUser] = useState(initialUser);

  const login = useCallback(async (email: string, password: string) => {
    const response = await http.post<LoginResponse>('/admin/auth/login', { email, password });

    http.setCsrfToken(response.csrfToken);
    setUser(response.user);

    return response.user;
  }, []);

  const logout = useCallback(async () => {
    try {
      // Lo que cierra la sesión de verdad es la revocación en el servidor;
      // limpiar aquí solo ordena el navegador.
      await http.post('/admin/auth/logout');
    } finally {
      http.setCsrfToken(null);
      setUser(null);
    }
  }, []);

  const refresh = useCallback(async () => {
    setUser(await fetchSession());
  }, []);

  const clear = useCallback(() => {
    http.setCsrfToken(null);
    setUser(null);
  }, []);

  const value = useMemo<SessionValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      login,
      logout,
      refresh,
      clear,
      hasRole: (minimum: Role) => satisfiesRole(user?.role, minimum),
    }),
    [user, login, logout, refresh, clear],
  );

  return <SessionContext value={value}>{children}</SessionContext>;
}

/**
 * Jerarquía de roles: `super_admin` > `admin` > `editor`.
 *
 * Misma regla que el backend. Aquí sirve para no enseñar lo que no se puede
 * usar; quien decide de verdad es el servidor.
 */
export function satisfiesRole(role: string | undefined, minimum: Role): boolean {
  if (!role) {
    return false;
  }

  const actual = ROLES.indexOf(role as Role);
  const needed = ROLES.indexOf(minimum);

  return actual >= 0 && actual >= needed;
}
