import { Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { RequireModuleOperation, RequireRole } from '../../session';
import { OrdersPage } from './pages/OrdersPage';
import { OrderDetailPage } from './pages/OrderDetailPage';
import './sales.css';
export const salesNavigation: ModuleNavigation = {
  moduleCode: 'sales', group: 'Ventas', items: [{ to: '/admin/pedidos', label: 'Pedidos en línea', minimumRole: 'editor', operation: 'read' }],
};
export const salesAdminRoutes = <Route element={<RequireRole minimum="editor" />}>
  <Route element={<RequireModuleOperation module="sales" operation="read" />}>
    <Route path="pedidos" element={<OrdersPage />} /><Route path="pedidos/:code" element={<OrderDetailPage />} />
  </Route>
</Route>;
