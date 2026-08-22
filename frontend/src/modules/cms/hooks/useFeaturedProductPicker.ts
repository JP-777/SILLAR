import { useCallback, useEffect, useRef, useState } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import {
  featuredProductsCatalogService,
  type FeaturedProductPickerItem,
} from '../services/featuredProducts';

export type FeaturedProductPickerState =
  | { status: 'unavailable' }
  | { status: 'idle' }
  | { status: 'searching'; query: string }
  | { status: 'results'; query: string; items: readonly FeaturedProductPickerItem[] }
  | { status: 'empty'; query: string }
  | { status: 'error'; query: string; failure: Failure };

export interface FeaturedProductPickerOptions {
  /** Resultado de `has('catalog')`; guía la interfaz, no sustituye al backend. */
  readonly catalogAvailable: boolean;
  /** Límite que se transmite al endpoint. `undefined` conserva el valor del servidor. */
  readonly limit?: number;
}

export interface FeaturedProductPicker {
  readonly state: FeaturedProductPickerState;
  /** La selección cambia solo mediante `select`; buscar nunca la borra. */
  readonly selected: FeaturedProductPickerItem | null;
  readonly select: (item: FeaturedProductPickerItem | null) => void;
  readonly search: (term: string) => Promise<void>;
}

/**
 * Estado funcional del selector de productos destacados.
 *
 * No añade debounce, búsqueda por prefijo ni filtrado local. Envía el término
 * completo al contrato de Catálogo y descarta respuestas de búsquedas anteriores
 * que lleguen tarde.
 */
export function useFeaturedProductPicker({
  catalogAvailable,
  limit,
}: FeaturedProductPickerOptions): FeaturedProductPicker {
  const firstState: FeaturedProductPickerState = catalogAvailable
    ? { status: 'idle' }
    : { status: 'unavailable' };

  const [state, setState] = useState<FeaturedProductPickerState>(firstState);
  const [selected, setSelected] = useState<FeaturedProductPickerItem | null>(null);
  const latestSearch = useRef(0);
  const lastSettledState = useRef<FeaturedProductPickerState>(firstState);

  const settle = useCallback((next: FeaturedProductPickerState) => {
    lastSettledState.current = next;
    setState(next);
  }, []);

  useEffect(() => {
    // Invalida cualquier respuesta en vuelo cuando cambia la capacidad.
    latestSearch.current += 1;
    settle(catalogAvailable ? { status: 'idle' } : { status: 'unavailable' });
  }, [catalogAvailable, settle]);

  const search = useCallback(
    async (term: string) => {
      const query = term.trim();
      const searchId = ++latestSearch.current;

      if (!catalogAvailable) {
        settle({ status: 'unavailable' });
        return;
      }

      if (query === '') {
        settle({ status: 'idle' });
        return;
      }

      const previous = lastSettledState.current;
      setState({ status: 'searching', query });

      try {
        const items = await featuredProductsCatalogService.search({ q: query, limit });

        if (searchId !== latestSearch.current) {
          return;
        }

        settle(
          items.length === 0
            ? { status: 'empty', query }
            : { status: 'results', query, items },
        );
      } catch (error) {
        if (searchId !== latestSearch.current) {
          return;
        }

        const failure = describe(error, 'buscar productos para destacar');

        // La reconexión global ya explica un fallo silencioso. Se conserva el
        // último estado estable, igual que hace useResource al recargar.
        if (failure.kind === 'silent') {
          setState(previous);
          return;
        }

        settle({ status: 'error', query, failure });
      }
    },
    [catalogAvailable, limit, settle],
  );

  const select = useCallback((item: FeaturedProductPickerItem | null) => {
    setSelected(item);
  }, []);

  return { state, selected, select, search };
}
