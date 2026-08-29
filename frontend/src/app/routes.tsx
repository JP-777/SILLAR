import { Navigate, Route, Routes } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { AdminShell } from '../layout/AdminShell';
import { HomePage } from '../platform/HomePage';
import { LoginPage } from '../platform/LoginPage';
import { PublicSite } from '../platform/PublicSite';
import { RequireAuth } from '../session';
import { catalogPublicRoutes, catalogRoutes } from '../modules/catalog/routes';
import { cmsRoutes } from '../modules/cms/routes';
import { coreRoutes } from '../modules/core/routes';
import { crmAdminRoutes, crmPublicRoutes } from '../modules/crm/routes';

/**
 * Rutas de la aplicación.
 *
 * Cada módulo con interfaz exporta sus rutas y **solo se montan las de los
 * módulos activos**. Un módulo desactivado no deja una ruta muerta que
 * responda con una pantalla rota: la ruta sencillamente no existe, y quien
 * llegue escribiéndola cae en la redirección de abajo.
 *
 * CORE va sin condición porque siempre está activo — es la base sobre la que
 * se enchufa todo lo demás.
 */
export function AppRoutes() {
  const { has } = useCapability();

  return (
    <Routes>
      <Route path="/" element={<PublicSite />} />
      <Route path="/login" element={<LoginPage />} />

      {/* La tienda. Pública y fuera del panel: sin RequireAuth. */}
      {has('catalog') && catalogPublicRoutes}
      {has('crm') && crmPublicRoutes}

      <Route element={<RequireAuth />}>
        <Route path="/admin" element={<AdminShell />}>
          <Route index element={<HomePage />} />
          {coreRoutes}
          {has('catalog') && catalogRoutes}
          {has('cms') && cmsRoutes}
          {has('crm') && crmAdminRoutes}
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
