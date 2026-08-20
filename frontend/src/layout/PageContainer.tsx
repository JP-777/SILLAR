import type { ReactNode } from 'react';
import { useDocumentTitle } from '../shared/a11y/useDocumentTitle';
import './layout.css';

/** Contenedor con el ancho y el ritmo vertical de una pantalla del panel. */
export function PageContainer({
  title,
  description,
  actions,
  children,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
}) {
  // El título de la pantalla es también el del documento: ya está aquí, y
  // así ninguna pantalla del panel tiene que acordarse de ponerlo.
  useDocumentTitle(title);

  return (
    <div className="ly-page">
      <div className="ly-page__header">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 'var(--s4)' }}>
          <h1>{title}</h1>
          {actions}
        </div>
        {description && <p className="ly-page__description">{description}</p>}
      </div>

      {children}
    </div>
  );
}
