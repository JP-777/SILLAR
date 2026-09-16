import { useEffect, useEffectEvent, type RefObject } from 'react';

const FOCUSABLE = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(', ');

/**
 * `:focus-visible` no basta aquí: el navegador juzga el foco inicial de un
 * diálogo por la modalidad de la interacción que lo *abrió* (un clic), no
 * por que el elemento enfocado sea nuevo y nadie lo haya tocado todavía.
 * Comprobado con un clic real, no con `page.click()` de Playwright: al
 * abrir con ratón, `:focus-visible` da `false` en el primer control.
 * `PROTOCOLO-DISENO.md` §4.6 exige el anillo pase lo que pase, así que se
 * fuerza con esta clase y se suelta en cuanto el foco se mueve por
 * cualquier motivo — desde ahí manda `:focus-visible` otra vez.
 */
export const FORCE_FOCUS_RING_CLASS = 'ui-force-focus-ring';

/**
 * Atrapa el foco dentro de un elemento mientras está abierto.
 *
 * Sin esto, tabular desde un diálogo lleva al contenido de detrás, que está
 * tapado y no se puede usar: quien navega con teclado se pierde en una página
 * que visualmente no existe.
 *
 * Al cerrar devuelve el foco a donde estaba, que es lo que hace que abrir y
 * cerrar un panel no obligue a recorrer la página otra vez.
 *
 * **El efecto se arma una vez por apertura, y no con cada pintado de quien
 * lo usa.** Antes dependía también de `onEscape`, y las 24 llamadas del panel
 * lo pasan como flecha en línea: una identidad nueva en cada pintado. Así que
 * cualquier pintado de la página con el diálogo abierto desarmaba el trap
 * —devolviendo el foco a la página de detrás— y lo volvía a armar, mandando el
 * foco al primer control. Y los pintados llegan solos: `useToasts` quita cada
 * aviso a los 4 s con un `setState` en la página. Medido con los componentes
 * reales: tras guardar una marca y abrir «Nueva marca», al caducar el aviso el
 * foco saltaba a «Cerrar» y el siguiente espacio cerraba el panel y tiraba lo
 * escrito; en una confirmación, el Enter que iba a «Dar de baja» cancelaba.
 *
 * Por eso Escape se lee con `useEffectEvent`: la función que se ejecuta es la
 * de ese momento, pero cambiarla no rearma nada.
 */
export function useFocusTrap(
  container: RefObject<HTMLElement | null>,
  open: boolean,
  onEscape: () => void,
): void {
  const escape = useEffectEvent(onEscape);

  useEffect(() => {
    if (!open) {
      return;
    }

    const previous = document.activeElement as HTMLElement | null;
    const element = container.current;

    // Al primer control, o al contenedor si no hay ninguno.
    const first = element?.querySelector<HTMLElement>(FOCUSABLE);
    const target = first ?? element;
    target?.focus();

    target?.classList.add(FORCE_FOCUS_RING_CLASS);
    function releaseForcedRing() {
      target?.classList.remove(FORCE_FOCUS_RING_CLASS);
    }
    target?.addEventListener('blur', releaseForcedRing, { once: true });

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.stopPropagation();
        escape();
        return;
      }

      if (event.key !== 'Tab' || !element) {
        return;
      }

      const focusable = [...element.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
        (candidate) => candidate.offsetParent !== null,
      );

      if (focusable.length === 0) {
        return;
      }

      const start = focusable[0];
      const end = focusable[focusable.length - 1];

      if (event.shiftKey && document.activeElement === start) {
        event.preventDefault();
        end.focus();
      } else if (!event.shiftKey && document.activeElement === end) {
        event.preventDefault();
        start.focus();
      }
    }

    document.addEventListener('keydown', handleKeyDown, true);

    return () => {
      document.removeEventListener('keydown', handleKeyDown, true);
      target?.removeEventListener('blur', releaseForcedRing);
      releaseForcedRing();
      previous?.focus?.();
    };
  }, [container, open]);
}
