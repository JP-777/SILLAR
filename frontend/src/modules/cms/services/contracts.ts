/**
 * Estado calculado por CMS para una ventana de publicación.
 *
 * El frontend lo consume tal cual: no vuelve a comparar startsAt ni endsAt.
 */
export type PublicationState = 'inactive' | 'scheduled' | 'current' | 'expired';

/** Sustituye el orden completo de una entidad, sin actualizaciones parciales. */
export interface ReorderCmsRequest {
  /** Todos los identificadores existentes, exactamente una vez y en su orden final. */
  orderedIds: readonly number[];
}
