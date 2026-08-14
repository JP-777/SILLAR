import { createContext, useContext } from 'react';
import { http } from '../shared/http/client';

/** Configuración pública: pares clave-valor tal como los sirve el API. */
export type PublicSettings = Record<string, string>;

export interface PublicSettingsValue {
  /** Devuelve un valor, o `null` si la clave no está publicada. */
  get: (key: string) => string | null;
  all: PublicSettings;
}

export const PublicSettingsContext = createContext<PublicSettingsValue | null>(null);

/**
 * Lee la configuración pública.
 *
 * Solo las claves marcadas como públicas llegan aquí; el backend no sirve las
 * demás. Alimenta el nombre del negocio de la barra superior y, más adelante, la
 * web pública.
 */
export function fetchPublicSettings(): Promise<PublicSettings> {
  return http.get<PublicSettings>('/settings/public');
}

export function usePublicSettings(): PublicSettingsValue {
  const value = useContext(PublicSettingsContext);

  if (!value) {
    throw new Error('usePublicSettings se usó fuera de su proveedor.');
  }

  return value;
}
