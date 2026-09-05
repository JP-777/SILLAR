import { crearRegistroDeSuperficie } from './surfaceRegistry';

/**
 * **Quién ha puesto algo en el pie, y quién todavía no lo sabe.**
 *
 * Misma maquinaria y misma regla que la portada, sobre otra superficie y con
 * su propio registro. La independencia es lo que hace que las dos respuestas
 * sean honestas: un enlace social publicado no puede hacer que la portada deje
 * de decir «sitio en construcción», y una portada llena no puede hacer
 * aparecer un pie que no tiene nada dentro.
 *
 * El pie **no se pinta mientras se carga**, y no se pinta vacío: hasta que
 * alguien declare que aportó contenido, no hay `<footer>` en el documento.
 */
const pie = crearRegistroDeSuperficie();

export const AportesDeFooter = pie.Proveedor;
export const useAporteDeFooter = pie.useAporte;
export const useFooterState = pie.useEstado;
