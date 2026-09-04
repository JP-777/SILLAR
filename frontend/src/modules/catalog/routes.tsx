import { useCallback } from 'react';
import { Link, Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { useAporteDePortada } from '../../platform/homeState';
import type { EstadoAporte } from '../../platform/surfaceState';
import type { HomeSection } from '../../platform/homeSections';
import { useResource, type ResourceState } from '../../shared/hooks/useResource';
import { EmptyState } from '../../shared/ui';
import { publicCatalog, type PublicCard, type PublicPage } from './services/publicCatalog';
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

/**
 * En qué queda la invitación, según lo que haya publicado de verdad.
 *
 * **Un fallo cuenta como contenido**, igual que en M02 (`cmsHome.tsx:28-32`) y
 * por la misma regla: lo que se declara tiene que coincidir con lo que se
 * pinta. Si la consulta falla no sabemos si hay catálogo — probablemente sí—,
 * y la sección se sigue pintando; decir «todavía no hay contenido publicado»
 * porque no pudimos *preguntar* sería una segunda explicación del mismo hueco,
 * y la falsa debajo.
 */
function aporteDe(state: ResourceState<PublicPage<PublicCard>>): EstadoAporte {
  if (state.status === 'loading') {
    return 'cargando';
  }

  if (state.status === 'ready') {
    return state.data.totalItems === 0 ? 'vacio' : 'con-contenido';
  }

  return 'con-contenido';
}

/**
 * La invitación a ver el catálogo, **solo si hay catálogo que ver**.
 *
 * Antes pintaba siempre y declaraba siempre `'con-contenido'`. Con M01 activo
 * y cero productos públicos, la portada afirmaba «Mira todo lo que tenemos
 * publicado» y enlazaba a una lista vacía — y de paso impedía que la portada
 * llegara nunca a su estado vacío, porque el resumen ya tenía un aporte.
 *
 * **Se pregunta por uno, no por todos**: `pageSize: 1` basta para saber si
 * existe alguno, y `totalItems` lo dice sin traerse el catálogo entero a la
 * portada. Es el mismo endpoint público que usa `/catalogo`
 * (`publicCatalog.products`), no uno nuevo y no el de administración.
 */
function CatalogHomeSection() {
  const load = useCallback(() => publicCatalog.products({ page: 1, pageSize: 1 }), []);
  const { state } = useResource(load, 'mirar si hay catálogo publicado');

  // Antes de cualquier salida temprana: un hook no puede quedar detrás de un
  // `return`, y el estado que hay que declarar es justo el que las provoca.
  useAporteDePortada(aporteDe(state));

  // **Mientras se espera no se pinta nada, y no hace falta más.** El armazón
  // no afirma que la portada esté vacía mientras alguien siga cargando
  // (`homeState.tsx:139-147`), así que el aviso no puede aparecer y
  // desaparecer. Un indicador aquí sería ruido: la sección entera son tres
  // líneas.
  if (state.status !== 'ready') {
    return state.status === 'loading' ? null : <Invitacion />;
  }

  return state.data.totalItems === 0 ? null : <Invitacion />;
}

function Invitacion() {
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
