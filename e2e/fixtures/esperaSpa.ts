/**
 * Encadena una única navegación con la comprobación observable de que la SPA
 * terminó de pintar la superficie esperada.
 *
 * Las funciones se reciben separadas para poder acreditar sin navegador que
 * no se recarga durante la espera y que un fallo de visibilidad se propaga.
 */
export async function navegarUnaVezYEsperarVisibilidad(
  navegar: () => Promise<void>,
  esperarVisibilidad: () => Promise<void>,
): Promise<void> {
  await navegar();
  await esperarVisibilidad();
}
