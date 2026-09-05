import type { CSSProperties } from 'react';
import { useAporteDeFooter } from '../../platform/footerState';
import type { PublicFooterContribution } from '../../platform/footerContributions';
import type { EstadoAporte } from '../../platform/surfaceState';
import { useResource, type ResourceState } from '../../shared/hooks/useResource';
import { publicSocialLinksService, type PublicSocialLink } from './services/socialLinks';

/** La única contribución de M02 al pie; el registro central decide su posición. */
export const cmsFooter: PublicFooterContribution = {
  moduleCode: 'cms',
  Component: CmsFooterBlock,
};

/**
 * Cómo se llama cada red por su nombre.
 *
 * **Fallback honesto**: una plataforma que no esté en el mapa se enseña con el
 * código que venga, no como «Otra red». Es la misma regla que la auditoría —
 * antes que inventar una etiqueta, enseñar la verdad.
 */
const NOMBRES: Readonly<Record<string, string>> = {
  instagram: 'Instagram',
  facebook: 'Facebook',
  tiktok: 'TikTok',
  whatsapp: 'WhatsApp',
  youtube: 'YouTube',
};

/**
 * En qué queda el bloque, para que el pie sepa si hay algo que envolver.
 *
 * **Un fallo cuenta como vacío**, al revés que en la portada, y la diferencia
 * es deliberada: allí un bloque fallido pinta su título y su aviso, así que la
 * página no está vacía. Aquí no hay nada que pintar — el pie no es sitio para
 * un mensaje de error, y un `<footer>` con una disculpa dentro es peor que no
 * tener pie.
 */
function aporteDe(state: ResourceState<readonly PublicSocialLink[]>): EstadoAporte {
  if (state.status === 'loading') {
    return 'cargando';
  }

  if (state.status === 'ready') {
    return state.data.length === 0 ? 'vacio' : 'con-contenido';
  }

  return 'vacio';
}

/**
 * Las redes publicadas, como texto.
 *
 * **Texto y no iconos**, por dos razones que no son estéticas: no añadir una
 * dependencia de iconos por cinco enlaces, y no dibujar a mano logotipos que
 * son marcas de otros. El enlace dice a dónde lleva, que es lo que un enlace
 * externo debe decir.
 *
 * El tratamiento visual final queda fuera de esta corrección.
 */
function CmsFooterBlock() {
  const { state } = useResource(publicSocialLinksService.list, 'cargar las redes publicadas');

  // Se declara **antes de cualquier salida temprana**: un hook no puede quedar
  // detrás de un `return`, y además el estado que hay que declarar es
  // justamente el que provoca esas salidas.
  useAporteDeFooter(aporteDe(state));

  if (state.status !== 'ready' || state.data.length === 0) {
    return null;
  }

  return (
    <nav aria-label="Redes sociales">
      <ul style={listStyle}>
        {state.data.map((enlace) => (
          <li key={enlace.id}>
            <a href={enlace.url} rel="noopener" target="_blank">
              {NOMBRES[enlace.platform] ?? enlace.platform}
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}

const listStyle: CSSProperties = {
  display: 'flex',
  flexWrap: 'wrap',
  gap: 'var(--s4)',
  justifyContent: 'center',
  listStyle: 'none',
  margin: 0,
  padding: 0,
};
