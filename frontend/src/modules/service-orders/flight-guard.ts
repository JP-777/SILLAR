/**
 * Protección inmediata, por referencia, contra eventos consecutivos antes de
 * que React procese el siguiente render. No sustituye locks ni idempotencia
 * del backend.
 *
 * El scope identifica la orden y revisión del formulario: una respuesta
 * antigua no puede desbloquear una operación iniciada en una nueva ruta.
 */
export interface FlightSlot {
  current: string | null;
}

export function beginFlight(slot: FlightSlot, scope: string): boolean {
  if (slot.current === scope) return false;
  slot.current = scope;
  return true;
}

export function endFlight(slot: FlightSlot, scope: string): void {
  if (slot.current === scope) slot.current = null;
}
