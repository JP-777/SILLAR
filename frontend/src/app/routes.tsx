import { Navigate, Route, Routes } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { AdminShell } from '../layout/AdminShell';
import { HomePage } from '../platform/HomePage';
import { LoginPage } from '../platform/LoginPage';
import { PublicLayout } from '../platform/PublicLayout';
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
 *
 * **El sitio público va dentro de `PublicLayout`**, que es quien pone el pie:
 * un pie que estuviera solo en la portada desaparecería al abrir una ficha de
 * producto. Las pantallas de acceso quedan fuera — son chrome de plataforma,
 * no el sitio público.
 */
export function AppRoutes() {
  const { has } = useCapability();

  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route path="/" element={<PublicSite />} />

        {/* La tienda. Pública y fuera del panel: sin RequireAuth. */}
        {has('catalog') && catalogPublicRoutes}
      </Route>

      <Route path="/login" element={<LoginPage />} />
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
