import { useCallback, useEffect, useState } from 'react';
import { isApiError } from '../../../shared/http/errors';

export class ProviderUnavailableError extends Error {
  constructor() {
    super('Provider unavailable');
    this.name = 'ProviderUnavailableError';
  }
}

export type CustomerResourceState<T> =
  | { status: 'loading'; key: string }
  | { status: 'ready'; key: string; data: T }
  | { status: 'not-found'; key: string }
  | { status: 'unavailable'; key: string }
  | { status: 'unauthorized'; key: string }
  | { status: 'forbidden'; key: string }
  | { status: 'error'; key: string };

/**
 * Carga datos privados ligados a la identidad actual.
 *
 * El módulo no mantiene caché global: el guard de sesión desmonta Portal al
 * salir A y el montaje de B nace vacío. Además, cada desmontaje aborta la
 * petición pendiente e ignora cualquier resultado tardío.
 */
export function useCustomerResource<T>(
  resourceKey: string,
  load: (signal: AbortSignal) => Promise<T>,
): { state: CustomerResourceState<T>; reload: () => void } {
  const key = resourceKey;
  const [attempt, setAttempt] = useState(0);
  const [stored, setStored] = useState<CustomerResourceState<T>>({
    status: 'loading',
    key,
  });

  useEffect(() => {
    const controller = new AbortController();
    let current = true;

    setStored({ status: 'loading', key });

    void load(controller.signal)
      .then((data) => {
        if (current) {
          setStored({ status: 'ready', key, data });
        }
      })
      .catch((caught: unknown) => {
        if (!current || controller.signal.aborted) {
          return;
        }

        if (caught instanceof ProviderUnavailableError) {
          setStored({ status: 'unavailable', key });
        } else if (isApiError(caught, 'NotFound')) {
          setStored({ status: 'not-found', key });
        } else if (isApiError(caught, 'Unauthorized')) {
          setStored({ status: 'unauthorized', key });
        } else if (isApiError(caught, 'Forbidden')) {
          setStored({ status: 'forbidden', key });
        } else {
          setStored({ status: 'error', key });
        }
      });

    return () => {
      current = false;
      controller.abort();
    };
  }, [attempt, key, load]);

  const reload = useCallback(() => setAttempt((value) => value + 1), []);

  return {
    state: stored.key === key ? stored : { status: 'loading', key },
    reload,
  };
}
