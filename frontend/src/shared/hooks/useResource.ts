import { useCallback, useEffect, useState } from 'react';
import { describe, type Failure } from '../errors/messages';

/**
 * Los cuatro estados de una pantalla que carga datos.
 *
 * Se diseñan los cuatro; ninguno se deja al azar. Es la regla del §3, y el
 * motivo es que el estado que nadie diseña acaba siendo una pantalla en blanco
 * o un mensaje inútil.
 */
export type ResourceState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; failure: Failure }
  | { status: 'forbidden'; failure: Failure };

export interface Resource<T> {
  readonly state: ResourceState<T>;
  /** Vuelve a cargar. Sin estado de carga si ya hay datos, para no parpadear. */
  readonly reload: () => Promise<void>;
  /** Sustituye los datos sin ir al servidor, tras una escritura conocida. */
  readonly replace: (data: T) => void;
}

/**
 * Carga datos del API y expone los cuatro estados.
 *
 * @param load Qué pedir. Debe ser estable: envolverlo en `useCallback`.
 * @param context Qué se está haciendo, para las frases de error.
 */
export function useResource<T>(load: () => Promise<T>, context: string): Resource<T> {
  const [state, setState] = useState<ResourceState<T>>({ status: 'loading' });

  const run = useCallback(
    async (showLoading: boolean) => {
      if (showLoading) {
        setState({ status: 'loading' });
      }

      try {
        setState({ status: 'ready', data: await load() });
      } catch (error) {
        const failure = describe(error, context);

        // Un fallo de red no pinta nada: la reconexión global ya está en
        // pantalla. Dejar el estado como estaba evita que la interfaz se
        // desmonte bajo la superposición y vuelva vacía al recuperarse.
        if (failure.kind === 'silent') {
          return;
        }

        setState({
          status: failure.kind === 'forbidden' ? 'forbidden' : 'error',
          failure,
        });
      }
    },
    [load, context],
  );

  useEffect(() => {
    void run(true);
  }, [run]);

  return {
    state,
    reload: useCallback(() => run(false), [run]),
    replace: useCallback((data: T) => setState({ status: 'ready', data }), []),
  };
}
