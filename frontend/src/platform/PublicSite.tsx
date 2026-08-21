import { Link } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { usePublicSettings } from './usePublicSettings';
import { visibleHomeSections } from './homeSections';
import { EmptyState } from '../shared/ui';
import './platform.css';

/**
 * La portada de la web pública.
 *
 * **El armazón no conoce a ningún módulo.** Recorre `HOME_SECTIONS`, se queda
 * con las de los módulos activos y las pinta en el orden del array. Antes
 * preguntaba `has('catalog')` con un `if` escrito a mano: correcto mientras
 * hubo un solo módulo publicable, y un `if` por módulo en cuanto hubiera dos.
 *
 * Lo que sí es del armazón es **la identidad del sitio**. El nombre del
 * negocio encabeza la página, y ya lo hacía —las dos ramas del `if` anterior
 * lo ponían de título—, así que nunca fue de ninguna sección: es de la web.
 * Cada módulo pone lo suyo debajo.
 *
 * **Sin ninguna sección no se inventa nada**: se dice que el sitio está en
 * construcción, que es la verdad. Y un módulo inactivo no deja hueco, ni
 * aviso, ni sección vacía — sencillamente no está en la lista.
 */
export function PublicSite() {
  const businessName = usePublicSettings().get('business_name');
  const configured = businessName && businessName !== 'PENDIENTE_DEFINIR';
  const secciones = visibleHomeSections(useCapability().has);

  if (secciones.length === 0) {
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

  return (
    <main className="pf-centered" id="contenido" tabIndex={-1}>
      {/* **Sin nombre configurado no hay encabezado.** El encabezado *es* el
          nombre del negocio; poner uno de reserva lo duplicaba con el título
          de la primera sección, que en la portada del catálogo es «Nuestra
          tienda». Un rótulo inventado no informa de nada. */}
      {configured && <h1 className="pf-centered__brand">{businessName}</h1>}

      {secciones.map(({ moduleCode, Component }) => (
        <Component key={moduleCode} />
      ))}
    </main>
  );
}
