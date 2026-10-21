import { Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { RequireRole } from '../../session';
import { ServiceOrdersListPage } from './pages/ServiceOrdersListPage';
import { ServiceOrderCreatePage } from './pages/ServiceOrderCreatePage';
import { ServiceOrderDetailPage } from './pages/ServiceOrderDetailPage';

/** M05b A1-A6: diseño ratificado expresamente por JP en conversación el 21-10-2026.
 * El montaje sigue sujeto al typecheck, QA y las autorizaciones de promoción.
 * La procedencia de Claude Design exigida por PROTOCOLO-DISENO.md no está acreditada.
 */
export const serviceOrdersNavigation: ModuleNavigation = {
  moduleCode: 'service_orders', group: 'Servicios',
  items: [{ to: '/admin/ordenes-servicio', label: 'Órdenes', minimumRole: 'editor' }],
};

export const serviceOrdersAdminRoutes = <Route element={<RequireRole minimum="editor" />}>
  <Route path="ordenes-servicio" element={<ServiceOrdersListPage />} />
  <Route path="ordenes-servicio/nueva" element={<ServiceOrderCreatePage />} />
  <Route path="ordenes-servicio/:id" element={<ServiceOrderDetailPage />} />
</Route>;
