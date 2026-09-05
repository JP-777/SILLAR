import type { CSSProperties } from 'react';
import { Outlet } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { visibleFooterContributions } from './footerContributions';
import { AportesDeFooter, useFooterState } from './footerState';

/**
 * El armazón de la web pública: lo que hay debajo de todas sus páginas.
 *
 * Existe porque **un pie que aparece en la portada y desaparece al abrir una
 * ficha de producto no es alcance reducido, es un defecto visible**. El
 * visitante ve el pie, entra en un producto y el pie se esfuma.
 *
 * `/login` queda fuera a propósito: es chrome de plataforma, no el sitio
 * público, y un formulario de acceso no necesita redes sociales.
 */
export function PublicLayout() {
  return (
    <AportesDeFooter>
      <Outlet />
      <PublicFooter />
    </AportesDeFooter>
  );
}

/**
 * El pie, que **solo existe cuando alguien ha puesto algo dentro**.
 *
 * Las contribuciones se montan siempre, también mientras se carga y también
 * cuando no traen nada: es la única forma de que puedan declarar en qué han
 * quedado. Lo que aparece o no es el `<footer>` que las envuelve. Una
 * contribución sin contenido devuelve `null`, así que sin contenido no queda
 * ni el contenedor ni un hueco en el documento.
 *
 * Nunca un pie vacío. Nunca un aviso de que el pie está vacío: el visitante no
 * tiene nada que hacer con esa información.
 *
 * **La plataforma no pone nada suyo aquí** — ni nombre del negocio, ni
 * copyright, ni aviso legal, ni dirección. El pie es un contenedor de
 * contribuciones.
 */
function PublicFooter() {
  const contribuciones = visibleFooterContributions(useCapability().has);
  const aportado = useFooterState();

  // **El `<footer>` se monta siempre y se oculta**, en vez de aparecer y
  // desaparecer. No es una preferencia: cambiar el padre de una contribución
  // la **remonta**, y remontar la retira del registro y le da una clave nueva
  // con su petición otra vez en vuelo. El resumen vuelve a «cargando», el pie
  // se va, la contribución remonta — y así sin parar, pidiendo las redes en
  // cada vuelta. Con el elemento estable, dentro solo cambia el contenido.
  //
  // Oculto no es un hueco: `hidden` lo saca del árbol de accesibilidad y no
  // ocupa alto. Sin contenido no hay pie que ver ni que leer.
  return (
    <footer
      className="pf-footer"
      hidden={aportado !== 'con-contenido'}
      style={aportado === 'con-contenido' ? footerStyle : undefined}
    >
      {contribuciones.map(({ moduleCode, Component }) => (
        <Component key={moduleCode} />
      ))}
    </footer>
  );
}

const footerStyle: CSSProperties = {
  display: 'grid',
  gap: 'var(--s4)',
  marginBlockStart: 'var(--s8)',
  paddingBlock: 'var(--s6)',
  borderBlockStart: '1px solid var(--border)',
};
