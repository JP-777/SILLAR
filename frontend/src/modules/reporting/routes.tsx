import { Route } from 'react-router-dom';
import { RequireRole } from '../../session';
import { ReportsPage } from './pages/ReportsPage';

/** M10 se monta únicamente con la capacidad reporting activa. */
export const reportingRoutes = (
  <Route element={<RequireRole minimum="admin" />}>
    <Route path="reportes" element={<ReportsPage />} />
  </Route>
);

export { reportingNavigation } from './navegacion';
