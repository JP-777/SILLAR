import { Route } from 'react-router-dom';
import { RequireRole } from '../../session';
import { CotizacionPage } from './pages/CotizacionPage';
import { CotizacionesPage } from './pages/CotizacionesPage';
import { SolicitudesPage } from './pages/SolicitudesPage';

/**
 * M07 en el panel: rutas de administración bajo `/admin/solicitudes`. La
 * aplicación las monta **solo si la capacidad `b2b` está activa**; desactivado
 * M07, no queda ni entrada de menú ni ruta.
 */
export { b2bNavigation } from './navegacion';

export const b2bRoutes = (
  <Route element={<RequireRole minimum="editor" />}>
    <Route path="solicitudes/personalizadas" element={<SolicitudesPage tipo="personalizacion" />} />
    <Route path="solicitudes/institucionales" element={<SolicitudesPage tipo="volumen" />} />
    <Route path="solicitudes/cotizaciones" element={<CotizacionesPage />} />
    <Route path="solicitudes/cotizaciones/:id" element={<CotizacionPage />} />
  </Route>
);
