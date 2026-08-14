import { Navigate, Route, Routes } from 'react-router-dom';
import { AdminShell } from '../layout/AdminShell';
import { HomePage } from '../platform/HomePage';
import { LoginPage } from '../platform/LoginPage';
import { PublicSite } from '../platform/PublicSite';
import { RequireAuth } from '../session';

/**
 * Rutas de la aplicación.
 *
 * Cuando existan módulos con interfaz, cada uno exportará su `routes.ts` y se
 * montarán aquí solo los activos. Hoy no hay ninguno: `src/modules/` está vacío
 * a propósito, para comunicar dónde va lo que viene.
 */
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<PublicSite />} />
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route path="/admin" element={<AdminShell />}>
          <Route index element={<HomePage />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
