import { useCallback, useEffect, useState } from 'react';
import { describe } from '../../shared/errors/messages';

type State<T> = { key: string; status: 'loading' } | { key: string; status: 'ready'; data: T }
  | { key: string; status: 'error'; message: string };
export function useSalesResource<T>(key: string, load: (signal: AbortSignal) => Promise<T>) {
  const [revision, setRevision] = useState(0);
  const [stored, setStored] = useState<State<T>>({ key, status: 'loading' });
  useEffect(() => {
    const controller = new AbortController();
    setStored({ key, status: 'loading' });
    void load(controller.signal).then(data => {
      if (!controller.signal.aborted) setStored({ key, status: 'ready', data });
    }).catch(error => {
      if (!controller.signal.aborted) setStored({ key, status: 'error',
        message: describe(error, 'cargar los pedidos').message || 'No se pudo completar la consulta. Reintenta cuando vuelva la conexión.' });
    });
    return () => controller.abort();
  }, [key, load, revision]);
  return { state: stored.key === key ? stored : { key, status: 'loading' } as State<T>,
    reload: useCallback(() => setRevision(n => n + 1), []) };
}
