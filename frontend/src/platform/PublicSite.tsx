import { Link } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { usePublicSettings } from './usePublicSettings';
import { AportesDePortada, useHomeState } from './homeState';
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
 *
 * ---
 *
 * **Y desde el 25 de agosto de 2026, tampoco lo deja un módulo activo que
 * todavía no tiene nada publicado**, que es el caso que faltaba y el que dejó
 * una portada muda.
 *
 * Contar secciones de módulos activos es contar **quién podría aportar**, no
 * quién aportó. Con M02 activo y sin contenido, la cuenta daba uno: el aviso
 * no salía, y los cuatro bloques de M02 devolvían `null`. La portada se
 * quedaba con el nombre del negocio y nada debajo. `catalogHome` tapaba el
 * agujero sin querer, porque pinta siempre — el día que hubo un segundo módulo
 * publicable, dejó de taparlo.
 *
 * Ahora lo dicen las propias secciones (`homeState.tsx`), y el armazón
 * sigue sin saber cuántos bloques trae ninguna.
 */
export function PublicSite() {
  // El proveedor y quien lo lee no pueden ser el mismo componente: un
  // componente no ve el contexto que él mismo monta. De ahí la partición.
  return (
    <AportesDePortada>
      <Portada />
    </AportesDePortada>
  );
}

function Portada() {
  const businessName = usePublicSettings().get('business_name');
  const configured = businessName && businessName !== 'PENDIENTE_DEFINIR';
  const secciones = visibleHomeSections(useCapability().has);
  const aportado = useHomeState();

  // Sin ninguna sección montada nadie va a declarar nunca, así que esperar a
  // que hablen sería esperar para siempre.
  const vacia = secciones.length === 0 || aportado === 'vacio';

  return (
    <main className="pf-centered" id="contenido" tabIndex={-1}>
      {/* **Sin nombre configurado no hay encabezado.** El encabezado *es* el
          nombre del negocio; poner uno de reserva lo duplicaba con el título
          de la primera sección, que en la portada del catálogo es «Nuestra
          tienda». Un rótulo inventado no informa de nada.

          Y va **siempre** que esté configurado, también mientras se espera y
          también en la portada vacía: es la identidad del sitio, no una
          consecuencia de tener contenido. Antes aparecía y desaparecía según
          la rama, y al asentarse el estado la página daba un salto. */}
      {configured && <h1 className="pf-centered__brand">{businessName}</h1>}

      {secciones.map(({ moduleCode, Component }) => (
        <Component key={moduleCode} />
      ))}

      {vacia && (
        <EmptyState
          title="Sitio en construcción"
          // **Antes decía «Aparecerá cuando se active el módulo de contenido
          // web», y eso solo era cierto sin ningún módulo publicable activo.**
          // En el caso que este arreglo destapó, el módulo está activo: el
          // aviso le estaba diciendo a alguien que active lo que ya tiene
          // activo. Esto es verdad en los dos casos y en ninguno manda a
          // hacer algo que ya está hecho.
          description="Todavía no hay contenido publicado. Lo que se publique desde el panel aparecerá aquí."
          action={<Link to="/admin">Ir al panel de administración</Link>}
        />
      )}
    </main>
  );
}
