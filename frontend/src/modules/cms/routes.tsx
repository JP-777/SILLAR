import { Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { RequireRole } from '../../session';
import { BannersPage } from './pages/BannersPage';
import { FeaturedProductsPage } from './pages/FeaturedProductsPage';
import { FeaturedProjectsPage } from './pages/FeaturedProjectsPage';
import { PromotionsPage } from './pages/PromotionsPage';
import { SocialLinksPage } from './pages/SocialLinksPage';

export const cmsNavigation: ModuleNavigation = {
  moduleCode: 'cms',
  group: 'Contenido',
  items: [
    { to: '/admin/contenido/banners', label: 'Banners', minimumRole: 'editor' },
    { to: '/admin/contenido/promociones', label: 'Promociones', minimumRole: 'editor' },
    { to: '/admin/contenido/productos-destacados', label: 'Productos destacados', minimumRole: 'editor' },
    { to: '/admin/contenido/trabajos-destacados', label: 'Trabajos destacados', minimumRole: 'editor' },
    { to: '/admin/contenido/redes-sociales', label: 'Redes sociales', minimumRole: 'editor' },
  ],
};

export const cmsRoutes = (
  <Route element={<RequireRole minimum="editor" />}>
    <Route path="contenido/banners" element={<BannersPage />} />
    <Route path="contenido/promociones" element={<PromotionsPage />} />
    <Route path="contenido/productos-destacados" element={<FeaturedProductsPage />} />
    <Route path="contenido/trabajos-destacados" element={<FeaturedProjectsPage />} />
    <Route path="contenido/redes-sociales" element={<SocialLinksPage />} />
  </Route>
);
