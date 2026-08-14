import { createContext, useCallback, useMemo, useState, type ReactNode } from 'react';
import { http } from '../shared/http/client';

/** Un módulo activo, tal como lo devuelve el API. */
export interface ModuleCapability {
  code: string;
  version: string;
}

/** Respuesta de `GET /api/capabilities`. */
export interface Capabilities {
  product: string;
  version: string;
  modules: ModuleCapability[];
}

export interface CapabilitiesValue {
  /** Indica si un módulo está activo en esta instalación. */
  has: (code: string) => boolean;
  modules: ModuleCapability[];
  /** Versión del producto instalada. */
  version: string;
  /** Vuelve a consultar. Solo la pantalla de reconexión debería llamarlo. */
  refresh: () => Promise<void>;
}

export const CapabilitiesContext = createContext<CapabilitiesValue | null>(null);

/** Consulta las capacidades. Lo usa el arranque, una sola vez. */
export function fetchCapabilities(): Promise<Capabilities> {
  return http.get<Capabilities>('/capabilities');
}

/**
 * Guarda en memoria qué módulos están activos.
 *
 * Se consultan una vez al arrancar. No cambian mientras el host vive: activar o
 * desactivar un módulo lo reinicia, y de eso se encarga la pantalla de
 * reconexión, que es la única que llama a `refresh`.
 *
 * Las capacidades son una **guía de presentación**, nunca un control de acceso.
 * La autorización real vive en el backend, siempre. Quien manipule esta lista en
 * el navegador solo consigue ver un menú que no lleva a ninguna parte.
 */
export function CapabilitiesProvider({
  initial,
  children,
}: {
  initial: Capabilities;
  children: ReactNode;
}) {
  const [capabilities, setCapabilities] = useState(initial);

  const refresh = useCallback(async () => {
    setCapabilities(await fetchCapabilities());
  }, []);

  const value = useMemo<CapabilitiesValue>(() => {
    const active = new Set(capabilities.modules.map((module) => module.code));

    return {
      has: (code: string) => active.has(code),
      modules: capabilities.modules,
      version: capabilities.version,
      refresh,
    };
  }, [capabilities, refresh]);

  return <CapabilitiesContext value={value}>{children}</CapabilitiesContext>;
}
