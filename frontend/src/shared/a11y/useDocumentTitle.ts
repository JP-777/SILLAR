import { useEffect } from 'react';

/** Lo que va detrás del título de cada pantalla. */
const SUFIJO = 'SILLAR';

/**
 * Pone el título del documento de esta pantalla.
 *
 * **No es decoración.** El título es lo que aparece en el historial, lo que
 * anuncia un lector de pantalla al cambiar de página, y **lo que se ve al
 * compartir un enlace**. Con un único título estático, trece pantallas se
 * llaman igual y el historial deja de servir para volver.
 *
 * Va primero lo específico: quien tiene ocho pestañas abiertas lee los
 * primeros caracteres, no los últimos.
 */
export function useDocumentTitle(title: string | null | undefined) {
  useEffect(() => {
    if (!title) {
      return;
    }

    const anterior = document.title;
    document.title = `${title} · ${SUFIJO}`;

    // Se restaura al desmontar para que una pantalla no le deje su título a
    // la siguiente si esta todavía no ha puesto el suyo.
    return () => {
      document.title = anterior;
    };
  }, [title]);
}
