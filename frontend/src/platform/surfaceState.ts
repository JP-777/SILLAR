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
 */
export type EstadoAporte = 'cargando' | 'vacio' | 'con-contenido';

export function reducirAportes(aportes: readonly EstadoAporte[]): EstadoAporte {
  if (aportes.includes('con-contenido')) {
    return 'con-contenido';
  }

  return aportes.length === 0 || aportes.includes('cargando') ? 'cargando' : 'vacio';
}
