import { useCallback, useEffect, useMemo, useState } from 'react';
import { BrowserRouter, useNavigate } from 'react-router-dom';
import { CapabilitiesProvider, fetchCapabilities, type Capabilities } from '../capabilities/CapabilitiesProvider';
import { http } from '../shared/http/client';
import { connection } from '../shared/http/connection';
import { isApiError } from '../shared/http/errors';
import { SessionProvider, fetchSession, type AuthenticatedUser } from '../session';
import { CustomerSessionProvider } from '../modules/crm/session';
import {
  fetchCustomerSession,
  type CustomerIdentity,
} from '../modules/crm/services/customerAuth';
import { PlatformErrorPage } from '../platform/PlatformErrorPage';
import { RouteFocus } from '../shared/a11y/RouteFocus';
import { ReconnectingOverlay } from '../platform/ReconnectingOverlay';
import { SetupPage } from '../platform/SetupPage';
import {
  PublicSettingsContext,
  fetchPublicSettings,
  type PublicSettings,
  type PublicSettingsValue,
} from '../platform/usePublicSettings';
import { Spinner } from '../shared/ui';
import { AppRoutes } from './routes';
import '../shared/styles/tokens.css';
import '../shared/styles/base.css';
import '../platform/platform.css';

/** Lo que el arranque necesita resolver antes de poder montar nada. */
type Boot =
  | { phase: 'loading' }
  | { phase: 'setup' }
  | { phase: 'failed'; detail: string }
  | {
      phase: 'ready';
      capabilities: Capabilities;
      user: AuthenticatedUser | null;
      customer: CustomerIdentity | null;
      settings: PublicSettings;
    };

/**
 * Arranque de la aplicación.
 *
 * El orden del documento F-08 §5 no es negociable:
 *
 * 1. `/api/setup/status` — si falta instalar, asistente y punto. No se pide nada más.
 * 2. `/api/capabilities` — si falla, no se puede continuar: sin saber qué módulos
 *    hay, la aplicación no sabe qué montar.
 * 3. `/api/admin/auth/me` — un 401 aquí es el caso corriente, no un error.
 */
export function App() {
  const [boot, setBoot] = useState<Boot>({ phase: 'loading' });

  const start = useCallback(async () => {
    setBoot({ phase: 'loading' });

    try {
      // Paso 1. Mientras la instalación esté pendiente, esta ruta responde 200;
      // en cuanto se completa, deja de existir y devuelve 404.
      const status = await http.get<{ setupRequired: boolean }>('/setup/status', {
        allowUnauthorized: true,
      });

      if (status.setupRequired) {
        setBoot({ phase: 'setup' });
        return;
      }
    } catch (error) {
      // 404 significa «ya está instalado», que es lo normal. Cualquier otra cosa
      // se deja para el paso 2, que es el que decide si se puede continuar.
      if (!isApiError(error, 'NotFound')) {
        setBoot({
          phase: 'failed',
          detail: isApiError(error) ? error.displayMessage : String(error),
        });
        return;
      }
    }

    try {
      // Paso 2 y 3, más la configuración pública que necesita el armazón.
      const capabilities = await fetchCapabilities();
      const crmActive = capabilities.modules.some(
        (module) => module.code === 'crm',
      );

      const [user, customer, settings] = await Promise.all([
        fetchSession(),
        crmActive ? fetchCustomerSession() : Promise.resolve(null),
        loadSettings(),
      ]);

      setBoot({
        phase: 'ready',
        capabilities,
        user,
        customer,
        settings,
      });
    } catch (error) {
      setBoot({
        phase: 'failed',
        detail: isApiError(error) ? error.displayMessage : String(error),
      });
    }
  }, []);

  useEffect(() => {
    void start();
  }, [start]);

  if (boot.phase === 'loading') {
    // Un estado sobrio, nunca una pantalla en blanco.
    return (
      <div className="pf-boot">
        <Spinner size="lg" label="Cargando" />
      </div>
    );
  }

  if (boot.phase === 'failed') {
    return <PlatformErrorPage detail={boot.detail} onRetry={() => void start()} />;
  }

  if (boot.phase === 'setup') {
    return (
      <BrowserRouter>
        <SetupPage />
      </BrowserRouter>
    );
  }

  return (
    <BrowserRouter>
      <CapabilitiesProvider initial={boot.capabilities}>
        <SessionProvider initialUser={boot.user}>
          <CustomerSessionProvider initialCustomer={boot.customer}>
            <PublicSettingsHost initial={boot.settings}>
            {/* Montado UNA vez, en la raíz. El estado de reconexión vive fuera
                de React, así que sigue ahí aunque el usuario navegue. */}
            {/* Enlace de salto: el primero del recorrido, invisible hasta
                que recibe el foco. Ahora que hay `<main>` en las dos mitades
                —panel y tienda— hay adónde saltar, y quien navega con teclado
                no tiene que recorrer el menú entero en cada pantalla. */}
            <a href="#contenido" className="pf-skip">
              Saltar al contenido
            </a>

            <ReconnectingOverlay />
            <RouteFocus />
            <Wiring />
            <AppRoutes />
            </PublicSettingsHost>
          </CustomerSessionProvider>
        </SessionProvider>
      </CapabilitiesProvider>
    </BrowserRouter>
  );
}

async function loadSettings(): Promise<PublicSettings> {
  try {
    return await fetchPublicSettings();
  } catch {
    // El panel funciona sin ella: solo se queda sin el nombre del negocio en la
    // barra superior. No es motivo para no arrancar.
    return {};
  }
}

function PublicSettingsHost({
  initial,
  children,
}: {
  initial: PublicSettings;
  children: React.ReactNode;
}) {
  const value = useMemo<PublicSettingsValue>(
    () => ({ all: initial, get: (key) => initial[key] ?? null }),
    [initial],
  );

  return <PublicSettingsContext value={value}>{children}</PublicSettingsContext>;
}

/**
 * Conecta el cliente HTTP y el almacén de conexión con lo que solo React sabe
 * hacer: navegar y refrescar los proveedores.
 *
 * No pinta nada. Existe porque el cliente HTTP no debe importar React ni saber
 * de rutas, y el almacén de conexión tampoco.
 */
function Wiring() {
  const navigate = useNavigate();

  useEffect(() => {
    http.onUnauthorized(() => {
      // La sesión murió. Al login, sin reintentos: reintentar con una sesión
      // muerta solo produce un bucle.
      navigate('/login', { replace: true });
    });
  }, [navigate]);

  useEffect(() => {
    // Cuando el servidor vuelva, se recarga la página entera. Es lo más honesto
    // tras un reinicio del host: capacidades, sesión y configuración se releen
    // de una vez y ningún estado viejo sobrevive al cambio que acaba de hacerse.
    connection.onRecovered(() => {
      window.location.reload();
    });
  }, []);

  return null;
}
