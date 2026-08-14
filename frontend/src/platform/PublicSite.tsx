import { Link } from 'react-router-dom';
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

  return (
    <div className="pf-centered">
      <EmptyState
        title={configured ? businessName : 'Sitio en construcción'}
        description="Todavía no hay contenido publicado. Aparecerá cuando se active el módulo de contenido web."
        action={<Link to="/admin">Ir al panel de administración</Link>}
      />
    </div>
  );
}
