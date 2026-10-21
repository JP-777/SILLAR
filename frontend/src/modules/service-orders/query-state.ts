/** El render usa únicamente respuestas de la consulta actualmente solicitada. */
export interface KeyedQueryState<T, F> {
  readonly key: string;
  readonly status: 'loading' | 'ready' | 'forbidden' | 'error' | 'missing';
  readonly data?: T;
  readonly failure?: F;
}

/**
 * React ejecuta useEffect después de pintar. Al cambiar query/recarga,
 * nunca se deben renderizar datos (ni 403) correspondientes a la ruta vieja.
 */
export function activeQuery<T, F>(
  state: KeyedQueryState<T, F>,
  requestedKey: string,
): KeyedQueryState<T, F> {
  return state.key === requestedKey
    ? state
    : { key: requestedKey, status: 'loading' };
}

export function makeQueryKey(search: string, revision: number): string {
  return `${search}\u0000${revision}`;
}
