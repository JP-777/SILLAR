import { useCallback, useLayoutEffect, useState } from 'react';

/** Los dos temas que define `shared/styles/tokens.css`. */
export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'sillar:theme';

/**
 * Elección explícita de la persona, si la hay. `null` es "ninguna todavía":
 * no es lo mismo que "claro". Sin esta distinción, el hook tendría que fijar
 * un valor concreto desde el primer render, y ese valor —puesto como
 * atributo— ganaría siempre al medio `prefers-color-scheme` de `tokens.css`
 * aunque nadie hubiera elegido nada. Es justo el fallo que encontró el
 * equipo de diseño: el sistema en oscuro se quedaba en claro por defecto
 * porque este hook nunca dejaba el atributo sin poner.
 */
function storedChoice(): Theme | null {
  const value = window.localStorage.getItem(STORAGE_KEY);
  return value === 'light' || value === 'dark' ? value : null;
}

function systemPrefersDark(): boolean {
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

/**
 * Aplica la elección al documento entero, no a un componente: `tokens.css`
 * define `[data-theme]` sobre el atributo, y las variables de rol cascadean
 * desde ahí. Sin elección, se quita el atributo del todo — es lo que deja
 * que el medio `prefers-color-scheme` decida, en vez de forzar un lado.
 */
function apply(choice: Theme | null) {
  if (choice) {
    document.documentElement.setAttribute('data-theme', choice);
  } else {
    document.documentElement.removeAttribute('data-theme');
  }
}

/**
 * Tema claro/oscuro de todo el panel.
 *
 * El tema oscuro estaba definido en `tokens.css` desde el sistema de diseño,
 * pero nada en la aplicación lo alcanzaba nunca: no había ni un lector de
 * `prefers-color-scheme` ni un interruptor. Este hook es ese interruptor, y
 * respeta al sistema mientras nadie haya elegido nada a mano.
 */
export function useTheme() {
  const [choice, setChoice] = useState<Theme | null>(storedChoice);

  // Layout, no efecto normal: aplica antes de que el navegador pinte, para no
  // enseñar un parpadeo cuando la elección guardada difiere del sistema.
  useLayoutEffect(() => {
    apply(choice);

    // Preferencia de interfaz, no sesión ni token CSRF: la regla de "solo en
    // memoria" no la alcanza. Sin elección, no se guarda nada: la próxima
    // carga debe volver a preguntarle al sistema, no repetir un valor viejo.
    if (choice) {
      window.localStorage.setItem(STORAGE_KEY, choice);
    } else {
      window.localStorage.removeItem(STORAGE_KEY);
    }
  }, [choice]);

  const toggle = useCallback(() => {
    setChoice((current) => {
      const currentlyDark = current === 'dark' || (current === null && systemPrefersDark());
      return currentlyDark ? 'light' : 'dark';
    });
  }, []);

  const theme: Theme = choice ?? (systemPrefersDark() ? 'dark' : 'light');

  return { theme, toggle };
}
