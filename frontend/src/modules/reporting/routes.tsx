import { Route } from 'react-router-dom';
import { RequireRole, RequireModuleOperation } from '../../session';
import { ReportsPage } from './pages/ReportsPage';

/** M10 se monta únicamente con la capacidad reporting activa. */
export const reportingRoutes = (
  <Route element={<RequireRole minimum="admin" />}>
    <Route element={<RequireModuleOperation module="reporting" operation="read" />}><Route path="reportes" element={<ReportsPage />} /></Route>
  </Route>
);

export { reportingNavigation } from './navegacion';

export { reportingAuditEntityVocabulary } from './auditEntityVocabulary';
