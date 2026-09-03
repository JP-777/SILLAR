import { Link, Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { useHomeContribution } from '../../platform/homeContributions';
import type { HomeSection } from '../../platform/homeSections';
import { EmptyState } from '../../shared/ui';
import { RequireRole } from '../../session';
import { BrandsPage } from './pages/BrandsPage';
import { CategoriesPage } from './pages/CategoriesPage';
import { ProductsPage } from './pages/ProductsPage';
import { CatalogPage } from './pages/CatalogPage';
import { CategoryPage } from './pages/CategoryPage';
import { ProductPage } from './pages/ProductPage';

/**
 * Lo que el módulo M01 aporta al panel.
 *
 * Mismo patrón que CORE: el módulo exporta su navegación y sus rutas, y la
 * aplicación monta **solo** lo de los módulos activos. Ninguna entrada de
 * menú está escrita a mano en el armazón: si M01 se desactiva, ni la entrada
 * ni la ruta existen.
 */
export const catalogNavigation: ModuleNavigation = {
  moduleCode: 'catalog',
  group: 'Catálogo',
  items: [
    // Editor basta: administrar el catálogo es trabajo de contenido, no de
    // configuración del negocio. Es el mismo rol que exige la API
    // (`BrandEndpoints.cs:24`).
    { to: '/admin/catalogo/productos', label: 'Productos', minimumRole: 'editor' },
    { to: '/admin/catalogo/categorias', label: 'Categorías', minimumRole: 'editor' },
    { to: '/admin/catalogo/marcas', label: 'Marcas', minimumRole: 'editor' },
  ],
};

/**
 * Lo que M01 aporta a la portada pública.
 *
 * Estaba escrito a mano en el armazón, con un `if` sobre el código del módulo
 * (`PublicSite.tsx`, antes de esta extracción). Vive aquí por lo mismo que la
 * navegación: **el armazón no conoce a ningún módulo**, y si M01 se desactiva
 * esta sección no se renderiza — ni vacía, ni con un aviso de que falta.
 *
 * El orden en la portada no se decide aquí: lo da la posición en
 * `HOME_SECTIONS`, porque es decisión de producto y no de este módulo.
 */
export const catalogHome: HomeSection = {
  moduleCode: 'catalog',
  Component: CatalogHomeSection,
};

/** La invitación a ver el catálogo. */
function CatalogHomeSection() {
  // **Siempre pinta, así que siempre aporta contenido.** Esta sección no
  // consulta nada: es una invitación fija, no un listado. Se declara igual
  // porque el armazón no distingue secciones «que siempre pintan» de las que
  // dependen de datos — y el día que ésta consulte productos, aquí es donde
  // cambia, sin tocar la portada.
  useHomeContribution('con-contenido');

  return (
    <EmptyState
      title="Nuestra tienda"
      description="Mira todo lo que tenemos publicado."
      action={<Link to="/catalogo">Ver el catálogo</Link>}
    />
  );
}

/**
 * Rutas del módulo.
 *
 * Las guardas de rol evitan enseñar lo que no se puede usar; la autorización
 * que manda sigue siendo la del backend.
 */
export const catalogRoutes = (
  <Route element={<RequireRole minimum="editor" />}>
    <Route path="catalogo/productos" element={<ProductsPage />} />
    <Route path="catalogo/categorias" element={<CategoriesPage />} />
    <Route path="catalogo/marcas" element={<BrandsPage />} />
  </Route>
);

/**
 * Las tres rutas de la tienda, **públicas**: sin sesión y fuera del armazón
 * del panel.
 *
 * Se montan solo con M01 activo, igual que las de administración. Con el
 * módulo desactivado no existen, y quien escriba una cae en la redirección
 * general — no en una pantalla rota.
 */
export const catalogPublicRoutes = (
  <>
    <Route path="/catalogo" element={<CatalogPage />} />
    <Route path="/catalogo/:categoria" element={<CategoryPage />} />
    <Route path="/producto/:slug" element={<ProductPage />} />
  </>
);
