import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useSession, type Role } from './index';
import { ForbiddenPage } from '../platform/ForbiddenPage';

/**
 * Protege el panel. Sin sesión, al login.
 *
 * Recuerda a dónde iba para poder devolver ahí después de entrar: quien pulsa un
 * enlace y acaba en el login espera volver a lo que pulsó.
 */
export function RequireAuth() {
  const { isAuthenticated } = useSession();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  return <Outlet />;
}

/**
 * Exige un rol mínimo, con la jerarquía del backend.
 *
 * Sin el rol, una pantalla que lo explica. No un menú roto ni una página en
 * blanco: quien llega aquí normalmente ha seguido un enlace legítimo y merece
 * saber por qué no puede pasar.
 */
export function RequireRole({ minimum }: { minimum: Role }) {
  const { hasRole } = useSession();

  return hasRole(minimum) ? <Outlet /> : <ForbiddenPage minimum={minimum} />;
}
