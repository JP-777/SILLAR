import { Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { RequireRole } from '../../session';
import { TrackingPage } from './pages/TrackingPage';

export const trackingNavigation: ModuleNavigation = {
  moduleCode: 'tracking',
  group: 'Seguimiento',
  items: [
    { to: '/admin/seguimiento', label: 'Tablero', minimumRole: 'editor' },
  ],
};

export const trackingRoutes = (
  <Route element={<RequireRole minimum="editor" />}>
    <Route path="seguimiento" element={<TrackingPage />} />
  </Route>
);
