import type { ReorderCmsRequest } from '../services/contracts';

/**
 * Mueve una fila y construye el payload completo que exige CMS.
 *
 * `items` debe ser el listado administrativo entero, incluidas las filas
 * inactivas: filtrar antes de llamar rompería el contrato de orden completo.
 */
export function buildCmsReorderRequest<T extends { readonly id: number }>(
  items: readonly T[],
  fromIndex: number,
  toIndex: number,
): ReorderCmsRequest {
  if (
    !Number.isInteger(fromIndex)
    || !Number.isInteger(toIndex)
    || fromIndex < 0
    || fromIndex >= items.length
    || toIndex < 0
    || toIndex >= items.length
  ) {
    throw new RangeError('Las posiciones de reordenamiento deben pertenecer al listado completo.');
  }

  const reordered = [...items];
  const [moved] = reordered.splice(fromIndex, 1);
  reordered.splice(toIndex, 0, moved);

  return { orderedIds: reordered.map((item) => item.id) };
}
