/**
 * Estado agregado de una superficie compuesta por contribuciones modulares.
 *
 * La regla es deliberadamente común a cualquier superficie:
 *
 * 1. Si alguien aporta contenido, la superficie tiene contenido.
 * 2. Si nadie aporta contenido pero alguien sigue cargando, todavía no se sabe.
 * 3. Si todos terminaron y ninguno aportó, está vacía.
 *
 * Cero aportes significa `cargando`, no `vacio`: en el primer render los
 * efectos que registran las contribuciones todavía no han corrido.
 *
 * Regla de montaje: si los contribuyentes viven dentro de la misma superficie
 * cuyo estado decide si esa superficie aparece, el montaje no puede ser el
 * mecanismo de ocultación. Sería circular: los aportes solo existen después
 * de montar a quienes los declaran. La superficie mantiene a sus
 * contribuyentes montados y oculta únicamente su presentación cuando el
 * resumen todavía no tiene contenido.
 */
export type EstadoAporte = 'cargando' | 'vacio' | 'con-contenido';

export function reducirAportes(aportes: readonly EstadoAporte[]): EstadoAporte {
  if (aportes.includes('con-contenido')) {
    return 'con-contenido';
  }

  return aportes.length === 0 || aportes.includes('cargando') ? 'cargando' : 'vacio';
}
