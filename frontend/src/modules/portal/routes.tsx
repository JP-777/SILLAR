import { Link, Route } from 'react-router-dom';
import { useAporteDePortada } from '../../platform/homeState';
import type { HomeSection } from '../../platform/homeSections';
import { EmptyState } from '../../shared/ui';
import { PortalOrderDetailPage } from './pages/PortalOrderDetailPage';
import { PortalOrdersPage } from './pages/PortalOrdersPage';
import { PortalOverviewPage } from './pages/PortalOverviewPage';
import { PortalWorkDetailPage } from './pages/PortalWorkDetailPage';
import { PortalWorkPage } from './pages/PortalWorkPage';
import './portal.css';

export const portalHome: HomeSection = {
  moduleCode: 'portal',
  Component: PortalHomeSection,
};

function PortalHomeSection() {
  useAporteDePortada('con-contenido');

  return (
    <EmptyState
      title="Mi portal"
      description="Consulta tus pedidos y el avance de tus trabajos con tu cuenta."
      action={
        <Link to="/mi-portal">Ir a Mi portal</Link>
      }
    />
  );
}

export const portalPublicRoutes = (
  <>
    <Route path="/mi-portal" element={<PortalOverviewPage />} />
    <Route path="/mi-portal/pedidos" element={<PortalOrdersPage />} />
    <Route path="/mi-portal/pedidos/:orderCode" element={<PortalOrderDetailPage />} />
    <Route path="/mi-portal/trabajos" element={<PortalWorkPage />} />
    <Route path="/mi-portal/trabajos/:visibleCode" element={<PortalWorkDetailPage />} />
  </>
);
