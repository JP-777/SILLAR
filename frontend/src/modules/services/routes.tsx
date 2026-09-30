import { useCallback } from 'react';
import { Link, Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { useAporteDePortada } from '../../platform/homeState';
import type { HomeSection } from '../../platform/homeSections';
import { useResource } from '../../shared/hooks/useResource';
import { EmptyState } from '../../shared/ui';
import { RequireRole } from '../../session';
import { ServicePage } from './pages/ServicePage';
import { ServicesAdminPage } from './pages/ServicesAdminPage';
import { ServicesPage } from './pages/ServicesPage';
import { servicesService } from './services/services';

export const servicesNavigation: ModuleNavigation = { moduleCode: 'services', group: 'Servicios', items: [{ to: '/admin/servicios', label: 'Servicios', minimumRole: 'editor' }] };
export const servicesHome: HomeSection = { moduleCode: 'services', Component: ServicesHomeSection };

function ServicesHomeSection() {
  const load = useCallback(() => servicesService.listPublic(), []);
  const { state } = useResource(load, 'comprobar los servicios publicados');
  const hasContent = state.status === 'ready' && state.data.length > 0;
  useAporteDePortada(state.status === 'loading' ? 'cargando' : hasContent ? 'con-contenido' : 'vacio');
  return hasContent ? <EmptyState title="Nuestros servicios" description="Consulta lo que podemos hacer por ti." action={<Link to="/servicios">Ver servicios</Link>} /> : null;
}

export const servicesAdminRoutes = <Route element={<RequireRole minimum="editor" />}><Route path="servicios" element={<ServicesAdminPage />} /></Route>;
export const servicesPublicRoutes = <><Route path="/servicios" element={<ServicesPage />} /><Route path="/servicios/:slug" element={<ServicePage />} /></>;
