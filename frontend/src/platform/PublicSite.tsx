import { Link } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { usePublicSettings } from './usePublicSettings';
import { EmptyState } from '../shared/ui';
import './platform.css';

/**
 * Web pública.
 *
 * Un contenedor vacío a propósito: no hay ningún módulo que la construya
 * todavía. Existirá de verdad con M02 Contenido Web, que traerá los banners y
 * las secciones.
 *
 * A diferencia del panel, aquí sí mandará el tema del cliente cuando llegue.
 */
export function PublicSite() {
  const businessName = usePublicSettings().get('business_name');
  const configured = businessName && businessName !== 'PENDIENTE_DEFINIR';
  const conCatalogo = useCapability().has('catalog');

  // Con M01 activo, la portada lleva al catálogo. **Sin M01 no se renderiza
  // esta sección en absoluto** — ni vacía, ni deshabilitada, ni con un aviso
  // de que falta algo: un hueco que explica su ausencia sigue siendo un hueco.
  if (conCatalogo) {
    return (
      <main className="pf-centered" id="contenido" tabIndex={-1}>
        <EmptyState
          title={configured ? businessName : 'Nuestra tienda'}
          description="Mira todo lo que tenemos publicado."
          action={<Link to="/catalogo">Ver el catálogo</Link>}
        />
      </main>
    );
  }

  return (
    <main className="pf-centered" id="contenido" tabIndex={-1}>
      <EmptyState
        title={configured ? businessName : 'Sitio en construcción'}
        description="Todavía no hay contenido publicado. Aparecerá cuando se active el módulo de contenido web."
        action={<Link to="/admin">Ir al panel de administración</Link>}
      />
    </main>
  );
}
