import type { ReactNode } from 'react';
import { EmptyState, Spinner } from './index';
import './gallery.css';

/**
 * Galería.
 *
 * El único patrón nuevo de la entrega 4b, y está justificado: los medios son
 * visuales y una tabla de nombres de archivo obliga a abrir cada uno para saber
 * cuál es.
 *
 * Comparte forma con `Table` a propósito —mismos estados de carga, vacío y
 * atenuado— para que las dos pantallas se comporten igual.
 */
interface GalleryProps<T> {
  items: readonly T[];
  itemKey: (item: T) => string | number;
  render: (item: T) => ReactNode;
  loading?: boolean;
  empty?: ReactNode;
}

export function Gallery<T>({ items, itemKey, render, loading = false, empty }: GalleryProps<T>) {
  if (loading) {
    return (
      <div className="gal">
        <div className="gal__state" style={{ display: 'flex', justifyContent: 'center', padding: 'var(--s7)' }}>
          <Spinner size="lg" label="Cargando archivos" />
        </div>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="gal">
        <div className="gal__state">{empty ?? <EmptyState title="No hay archivos" />}</div>
      </div>
    );
  }

  return (
    <div className="gal">
      {items.map((item) => (
        <div key={itemKey(item)} style={{ display: 'contents' }}>
          {render(item)}
        </div>
      ))}
    </div>
  );
}
