import { useEffect, useRef, useState } from 'react';

/**
 * Deja pasar una bandera solo si se mantiene encendida un rato, y una vez
 * encendida la sostiene un mínimo.
 *
 * Existe para el indicador de carga, y resuelve **dos** parpadeos distintos:
 *
 * 1. **Aparecer para nada.** Una respuesta de 200 ms con spinner se percibe
 *    más lenta que la misma sin nada: el indicador llama la atención sobre la
 *    espera y mueve el contenido dos veces. Por eso no se enseña nada por
 *    debajo de `afterMs`, que por defecto es **un segundo** (ENTREGA-04A §4).
 * 2. **Aparecer y desaparecer de golpe.** Una respuesta que llega a los
 *    1010 ms enseñaría el indicador 10 ms — un destello, que molesta más que
 *    los dos casos anteriores. Por eso, una vez encendido, se sostiene
 *    `atLeastMs` aunque la carga ya haya terminado.
 *
 * El segundo faltaba en la primera versión de este hook, y se notó al
 * revisarla: sin él, el umbral solo movía el problema a la ventana siguiente.
 *
 * @param active Si la operación está en curso.
 * @param afterMs Cuánto hay que esperar antes de enseñar nada.
 * @param atLeastMs Cuánto se sostiene, como mínimo, una vez enseñado.
 */
export function useDelayedFlag(active: boolean, afterMs = 1000, atLeastMs = 400): boolean {
  const [visible, setVisible] = useState(false);
  const shownAt = useRef<number | null>(null);

  useEffect(() => {
    if (active) {
      const timer = setTimeout(() => {
        shownAt.current = Date.now();
        setVisible(true);
      }, afterMs);

      return () => clearTimeout(timer);
    }

    // Se apagó. Si nunca llegó a verse, no hay nada que sostener.
    if (shownAt.current === null) {
      setVisible(false);
      return;
    }

    const pending = atLeastMs - (Date.now() - shownAt.current);

    if (pending <= 0) {
      shownAt.current = null;
      setVisible(false);
      return;
    }

    const timer = setTimeout(() => {
      shownAt.current = null;
      setVisible(false);
    }, pending);

    return () => clearTimeout(timer);
  }, [active, afterMs, atLeastMs]);

  return visible;
}
