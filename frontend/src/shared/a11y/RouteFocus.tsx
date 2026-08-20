import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * Lleva el foco al contenido cuando cambia la ruta.
 *
 * En una aplicación que se pinta en el navegador **la página no se recarga**,
 * así que el foco se queda donde estaba: quien navega con teclado pulsa un
 * enlace, cambia toda la pantalla, y su foco sigue en un menú que ya no
 * corresponde. Con un lector de pantalla es peor — no se anuncia nada.
 *
 * Mover el foco al `<main>` resuelve las dos cosas: el recorrido con Tab
 * empieza donde empieza el contenido, y el lector lee la pantalla nueva.
 *
 * **No actúa en la carga inicial**: al cargar, el foco ya está donde el
 * navegador lo pone, y robarlo sería peor que dejarlo — además de romper el
 * recorrido con Tab desde el principio, que es donde vive el enlace de salto.
 *
 * Se compara **la ruta anterior**, no una bandera de «primera vez». Con una
 * bandera booleana, `StrictMode` invoca el efecto dos veces en desarrollo: la
 * primera la consume y la segunda ya cree que hubo navegación, así que roba
 * el foco nada más cargar. La ruta guardada sobrevive a esa doble invocación
 * porque no cambia entre las dos.
 */
export function RouteFocus() {
  const { pathname } = useLocation();
  const anterior = useRef(pathname);

  useEffect(() => {
    if (anterior.current === pathname) {
      return;
    }

    anterior.current = pathname;

    const main = document.querySelector('main');
    if (!(main instanceof HTMLElement)) {
      return;
    }

    // `tabIndex = -1` lo hace enfocable por código sin meterlo en el
    // recorrido de Tab: nadie va a tabular *hasta* el main, solo se le da
    // el foco al llegar.
    main.setAttribute('tabindex', '-1');
    main.focus({ preventScroll: true });
  }, [pathname]);

  return null;
}
