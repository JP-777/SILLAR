import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import type { Failure } from '../errors/messages';
import { Alert, Button, EmptyState, Spinner } from './index';
import { useFocusTrap } from './useFocusTrap';
import './patterns.css';

/**
 * Patrones que fija la entrega 4a §3.
 *
 * Todo lo de 4b los reutiliza tal cual. Si salen mal aquí, salen mal
 * multiplicados por tres: por eso viven en `shared` desde el primer uso y no
 * dentro de la pantalla que los estrenó.
 */

/* --- Panel lateral ------------------------------------------------------ */

interface DrawerProps {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  footer?: ReactNode;
  children: ReactNode;
}

/**
 * Panel lateral para formularios cortos.
 *
 * Página completa solo si el formulario no cabe aquí. El panel conserva la
 * tabla de detrás a la vista, que es lo que hace que editar cinco usuarios
 * seguidos no sea una peregrinación.
 */
export function Drawer({ open, title, description, onClose, footer, children }: DrawerProps) {
  const panel = useRef<HTMLDivElement>(null);

  useFocusTrap(panel, open, onClose);

  if (!open) {
    return null;
  }

  return createPortal(
    <>
      <div className="ui-drawer__backdrop" onClick={onClose} aria-hidden="true" />

      <div
        className="ui-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="drawer-titulo"
        ref={panel}
        tabIndex={-1}
      >
        {/* `div` y no `header`: un `<header>` que no cuelga de article,
            aside, main, nav o section se mapea a landmark `banner`, y el
            armazón ya tiene el suyo en la barra superior — dos banners en la
            misma página es un fallo de accesibilidad (lo cazó axe-core al
            abrir este panel). El `<h2>` de dentro sigue etiquetando el
            diálogo por `aria-labelledby`, así que no se pierde nada. */}
        <div className="ui-drawer__header">
          <div>
            <h2 className="ui-drawer__title" id="drawer-titulo">
              {title}
            </h2>
            {description && <p className="ui-drawer__description">{description}</p>}
          </div>

          <button
            type="button"
            className="ui-drawer__close"
            onClick={onClose}
            aria-label="Cerrar"
          >
            ×
          </button>
        </div>

        <div className="ui-drawer__body">{children}</div>

        {footer && <footer className="ui-drawer__footer">{footer}</footer>}
      </div>
    </>,
    document.body,
  );
}

/* --- Confirmación destructiva ------------------------------------------- */

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  /**
   * La consecuencia real, enunciada. «Se cerrarán sus sesiones abiertas», «el
   * sistema se reiniciará unos segundos». No «¿estás seguro?».
   */
  children: ReactNode;
  /**
   * Texto del botón que confirma. **Nombra la acción**: «Activar Catálogo»,
   * «Desactivar usuario».
   *
   * No tiene valor por defecto a propósito: así no se puede acabar con un
   * «Aceptar» por descuido, que es exactamente lo que la entrega prohíbe.
   */
  confirmLabel: string;
  danger?: boolean;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel,
  danger = false,
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const panel = useRef<HTMLDivElement>(null);

  useFocusTrap(panel, open, onCancel);

  if (!open) {
    return null;
  }

  return createPortal(
    <div className="ui-dialog__backdrop">
      <div
        className="ui-dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="dialogo-titulo"
        ref={panel}
        tabIndex={-1}
      >
        <h2 className="ui-dialog__title" id="dialogo-titulo">
          {title}
        </h2>

        <div className="ui-dialog__body">{children}</div>

        <div className="ui-dialog__footer">
          <Button variant="secondary" onClick={onCancel} disabled={busy}>
            Cancelar
          </Button>
          <Button variant={danger ? 'danger' : 'primary'} onClick={onConfirm} loading={busy}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>,
    document.body,
  );
}

/* --- Avisos de escritura ------------------------------------------------ */

export interface Toast {
  readonly id: number;
  readonly message: string;
  readonly tone: 'success' | 'danger';
}

/**
 * Avisos breves y no intrusivos.
 *
 * Sin diálogos que haya que cerrar para seguir trabajando: quien acaba de
 * guardar algo quiere seguir, no confirmar que sabe que guardó.
 */
export function useToasts() {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const next = useRef(0);

  const show = useCallback((message: string, tone: Toast['tone'] = 'success') => {
    const id = next.current++;
    setToasts((current) => [...current, { id, message, tone }]);
    setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), 4000);
  }, []);

  return { toasts, show };
}

export function Toasts({ toasts }: { toasts: readonly Toast[] }) {
  if (toasts.length === 0) {
    return null;
  }

  return createPortal(
    <div className="ui-toasts" role="status" aria-live="polite">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className={`ui-toast${toast.tone === 'danger' ? ' ui-toast--danger' : ''}`}
        >
          {toast.message}
        </div>
      ))}
    </div>,
    document.body,
  );
}

/* --- Tabla -------------------------------------------------------------- */

export interface Column<T> {
  readonly key: string;
  readonly header: string;
  readonly align?: 'left' | 'right';
  readonly render: (row: T) => ReactNode;
}

interface TableProps<T> {
  columns: readonly Column<T>[];
  rows: readonly T[];
  rowKey: (row: T) => string | number;
  /** Atenúa la fila. Para lo dado de baja, que se muestra pero no se oculta. */
  dimmed?: (row: T) => boolean;
  loading?: boolean;
  /** Qué enseñar cuando no hay nada. Con acción, no solo un texto. */
  empty?: ReactNode;
  pagination?: PaginationProps;
}

export function Table<T>({
  columns,
  rows,
  rowKey,
  dimmed,
  loading = false,
  empty,
  pagination,
}: TableProps<T>) {
  return (
    <div className="ui-table-wrap">
      {/* Región de estado PERSISTENTE: existe desde el primer render, también
          en reposo, y **fuera** del nodo que lleva `aria-busy`.

          Las dos cosas son el patrón, no formalismo. Si el `role="status"`
          llegara al DOM a la vez que el mensaje, el anuncio no está
          garantizado (técnica de fallo F103 de WCAG). Y dentro del nodo
          ocupado, el propio `aria-busy` puede posponer justo el anuncio que
          se quiere dar. */}
      <div className="sr-only" role="status" aria-live="polite" aria-atomic="true">
        {loading ? 'Cargando datos.' : ''}
      </div>

      <div className="ui-table-scroll" aria-busy={loading}>
        <table className="ui-table">
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={column.align === 'right' ? 'ui-table__actions' : undefined}
                >
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>

          <tbody>
            {loading && (
              <tr>
                <td colSpan={columns.length} className="ui-table__state">
                  <div style={{ display: 'flex', justifyContent: 'center' }}>
                    {/* Sin región propia: la de esta tabla vive arriba, fuera
                        del nodo con aria-busy. Ver el comentario de allí. */}
                    <Spinner label="Cargando" announce={false} />
                  </div>
                </td>
              </tr>
            )}

            {!loading && rows.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="ui-table__state">
                  {empty ?? <EmptyState title="No hay nada que mostrar" />}
                </td>
              </tr>
            )}

            {!loading &&
              rows.map((row) => (
                <tr key={rowKey(row)} data-dimmed={dimmed?.(row) ?? false}>
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={column.align === 'right' ? 'ui-table__actions' : undefined}
                    >
                      {column.render(row)}
                    </td>
                  ))}
                </tr>
              ))}
          </tbody>
        </table>
      </div>

      {pagination && <Pagination {...pagination} />}
    </div>
  );
}

/* --- Paginación --------------------------------------------------------- */

export interface PaginationProps {
  page: number;
  totalPages: number;
  totalItems: number;
  onChange: (page: number) => void;
  /** Por defecto `md`. Ver el porqué en el comentario del componente. */
  size?: 'sm' | 'md' | 'lg';
}

/**
 * Paginación.
 *
 * El tamaño se elige desde fuera y **por defecto es `md`**, no `sm`. Lo fijaba
 * por dentro, y con eso la navegación entera del catálogo quedaba en botones
 * de 26,8 px — en móvil, imposibles de acertar.
 *
 * La regla que lo explica y que conviene tener escrita: **`sm` vale para un
 * control repetido en fila densa con ratón, y solo cuando existe otra manera
 * de hacer lo mismo.** «Anterior» y «Siguiente» no cumplen ni una cosa ni la
 * otra: no hay segunda vía para llegar a la página 2.
 */
export function Pagination({ page, totalPages, totalItems, onChange, size = 'md' }: PaginationProps) {
  if (totalItems === 0) {
    return null;
  }

  return (
    <div className="ui-pagination">
      <span>
        {totalItems} {totalItems === 1 ? 'resultado' : 'resultados'}
        {totalPages > 1 && ` · página ${page} de ${totalPages}`}
      </span>

      {totalPages > 1 && (
        <div className="ui-pagination__buttons">
          <Button size={size} variant="secondary" disabled={page <= 1} onClick={() => onChange(page - 1)}>
            Anterior
          </Button>
          <Button
            size={size}
            variant="secondary"
            disabled={page >= totalPages}
            onClick={() => onChange(page + 1)}
          >
            Siguiente
          </Button>
        </div>
      )}
    </div>
  );
}

/* --- Fallo en línea ------------------------------------------------------ */

/** Muestra un fallo junto a la operación que lo produjo. */
export function FailureAlert({ failure }: { failure: Failure | null }) {
  if (!failure || failure.kind === 'silent' || !failure.message) {
    return null;
  }

  return <Alert tone="danger">{failure.message}</Alert>;
}

/* --- Cierre con Escape en cualquier capa -------------------------------- */

/** Escucha Escape mientras la condición se cumpla. */
export function useEscape(active: boolean, onEscape: () => void): void {
  useEffect(() => {
    if (!active) {
      return;
    }

    function handle(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onEscape();
      }
    }

    document.addEventListener('keydown', handle);
    return () => document.removeEventListener('keydown', handle);
  }, [active, onEscape]);
}
