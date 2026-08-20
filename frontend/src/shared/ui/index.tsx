import { useId, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode } from 'react';
import './ui.css';

/**
 * Componentes base.
 *
 * Solo los que las pantallas de F-08 usan. `Table` y compañía llegarán con la
 * pantalla que los necesite: construir hoy lo que nadie usa es adivinar cómo
 * será esa pantalla.
 */

/* --- Button ------------------------------------------------------------- */

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  block?: boolean;
  /** Muestra un indicador y deshabilita, para que no se envíe dos veces. */
  loading?: boolean;
}

export function Button({
  variant = 'primary',
  size = 'md',
  block = false,
  loading = false,
  disabled,
  children,
  className,
  ...rest
}: ButtonProps) {
  const classes = [
    'ui-button',
    `ui-button--${variant}`,
    `ui-button--${size}`,
    block ? 'ui-button--block' : '',
    className ?? '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <button
      // **`button` por defecto, no `submit`.** El HTML por defecto es
      // `submit`, así que cualquier botón dentro de un `<form>` lo enviaba
      // sin decirlo: «Añadir presentación» guardaba el producto entero, y el
      // guardado de verdad chocaba después con lo que ese envío ya había
      // creado. Quien quiera enviar lo pide con `type="submit"`, que es lo
      // que ya hacen los formularios.
      type="button"
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading}
      {...rest}
    >
      {loading && <Spinner size="sm" />}
      {children}
    </button>
  );
}

/* --- Input -------------------------------------------------------------- */

// Se excluye el `size` nativo del `<input>`, que es un número de caracteres y
// no tiene nada que ver con el tamaño del control. Dejar los dos convertiría
// `size={2}` en algo que compila y no hace lo que parece.
interface InputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'size'> {
  invalid?: boolean;
  /** Igual que en `Button`, para que los dos se puedan alinear en una fila. */
  size?: 'sm' | 'md' | 'lg';
}

export function Input({ invalid = false, size = 'md', className, ...rest }: InputProps) {
  const classes = [
    'ui-input',
    size === 'md' ? '' : `ui-input--${size}`,
    invalid ? 'ui-input--invalid' : '',
    className ?? '',
  ]
    .filter(Boolean)
    .join(' ');

  return <input className={classes} aria-invalid={invalid || undefined} {...rest} />;
}

/* --- Field -------------------------------------------------------------- */

interface FieldProps {
  label: string;
  /** Ayuda que se muestra ANTES de escribir, no después de fallar. */
  hint?: ReactNode;
  error?: string | null;
  required?: boolean;
  /** Recibe los atributos que hay que poner en el control. */
  children: (props: {
    id: string;
    'aria-describedby': string | undefined;
    'aria-invalid': boolean | undefined;
    required: boolean;
  }) => ReactNode;
}

/**
 * Etiqueta, ayuda y error, con las relaciones de accesibilidad ya montadas.
 *
 * El error se anuncia a los lectores de pantalla: quien no ve el color rojo
 * tiene que enterarse igual.
 */
export function Field({ label, hint, error, required = false, children }: FieldProps) {
  const id = useId();
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;

  return (
    <div className="ui-field">
      <label className="ui-field__label" htmlFor={id}>
        {label}
        {required && (
          <span className="ui-field__required" aria-hidden="true">
            *
          </span>
        )}
      </label>

      {hint && (
        <span className="ui-field__hint" id={hintId}>
          {hint}
        </span>
      )}

      {children({
        id,
        'aria-describedby': describedBy,
        'aria-invalid': error ? true : undefined,
        required,
      })}

      {error && (
        <span className="ui-field__error" id={errorId} role="alert">
          {error}
        </span>
      )}
    </div>
  );
}

/* --- Alert -------------------------------------------------------------- */

export type AlertTone = 'info' | 'success' | 'warning' | 'danger';

interface AlertProps {
  tone?: AlertTone;
  title?: string;
  children?: ReactNode;
}

export function Alert({ tone = 'info', title, children }: AlertProps) {
  return (
    <div
      className={`ui-alert ui-alert--${tone}`}
      role={tone === 'danger' ? 'alert' : 'status'}
    >
      {title && <span className="ui-alert__title">{title}</span>}
      {children && <span>{children}</span>}
    </div>
  );
}

/* --- Card --------------------------------------------------------------- */

interface CardProps {
  title?: string;
  subtitle?: string;
  children: ReactNode;
}

export function Card({ title, subtitle, children }: CardProps) {
  return (
    <section className="ui-card">
      {title && (
        <header className="ui-card__header">
          <h2 className="ui-card__title">{title}</h2>
          {subtitle && <p className="ui-card__subtitle">{subtitle}</p>}
        </header>
      )}
      <div className="ui-card__body">{children}</div>
    </section>
  );
}

/* --- Badge -------------------------------------------------------------- */

export type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger';

export function Badge({ tone = 'neutral', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={`ui-badge ui-badge--${tone}`}>{children}</span>;
}

/* --- FilterChip --------------------------------------------------------- */

/**
 * Un filtro que se enciende y se apaga.
 *
 * **No es lo mismo que `Tag`, y la prueba está en qué contesta cada uno al
 * pulsarlo:** este contesta «¿estoy encendido?» y **se queda**; `Tag`
 * contesta «¿sigo existiendo?» y **desaparece**. Un componente único
 * necesitaría una prop que dijera cuál de los dos es, y esa prop sería la
 * confesión de que son dos.
 *
 * Lo encendido usa `--selected`, no `--success`: designado no es «va bien».
 */
export function FilterChip({
  selected,
  onToggle,
  children,
}: {
  selected: boolean;
  onToggle: (selected: boolean) => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      className={`ui-chip${selected ? ' ui-chip--selected' : ''}`}
      aria-pressed={selected}
      onClick={() => onToggle(!selected)}
    >
      {children}
    </button>
  );
}

/* --- Tag ---------------------------------------------------------------- */

/**
 * Algo elegido, con la posibilidad de quitarlo.
 *
 * Sin `onRemove` es solo una etiqueta y no lleva botón: un aspa que no hace
 * nada es peor que ninguna.
 */
export function Tag({
  onRemove,
  removeLabel,
  children,
}: {
  onRemove?: () => void;
  /** Qué se quita, para el lector de pantalla: «Quitar Deporte». */
  removeLabel?: string;
  children: ReactNode;
}) {
  return (
    <span className="ui-tag">
      <span>{children}</span>

      {onRemove && (
        <button
          type="button"
          className="ui-tag__remove"
          onClick={onRemove}
          aria-label={removeLabel ?? 'Quitar'}
        >
          ×
        </button>
      )}
    </span>
  );
}

/* --- NoResults ---------------------------------------------------------- */

/**
 * Buscar y no encontrar. **No es el estado vacío**, y por eso es otro
 * componente.
 *
 * Se distinguen por dónde vive el arreglo: en el vacío hay que crear algo, y
 * por eso `EmptyState` lleva acción; aquí **el arreglo ya está en pantalla**
 * —el campo de búsqueda, los filtros— y una acción principal le competiría.
 */
export function NoResults({
  title,
  description,
  onClear,
}: {
  title: string;
  description?: string;
  /** Quitar los filtros. Secundaria a propósito: la principal es el buscador. */
  onClear?: () => void;
}) {
  return (
    <div className="ui-empty">
      <p className="ui-empty__title">{title}</p>
      {description && <p className="ui-empty__description">{description}</p>}
      {onClear && (
        <Button variant="ghost" size="sm" onClick={onClear}>
          Quitar los filtros
        </Button>
      )}
    </div>
  );
}

/* --- Switch ------------------------------------------------------------- */

interface SwitchProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  disabled?: boolean;
}

export function Switch({ checked, onChange, label, disabled = false }: SwitchProps) {
  return (
    <label className="ui-switch">
      <input
        type="checkbox"
        className="ui-switch__input"
        role="switch"
        checked={checked}
        disabled={disabled}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span className="ui-switch__track" aria-hidden="true">
        <span className="ui-switch__thumb" />
      </span>
      <span className="ui-switch__label">{label}</span>
    </label>
  );
}

/* --- EmptyState --------------------------------------------------------- */

interface EmptyStateProps {
  title: string;
  description?: string;
  /** **Una sola.** Ver el comentario del componente. */
  action?: ReactNode;
  /** Dato secundario, cierto y no accionable. Va después de la acción. */
  note?: ReactNode;
}

/**
 * El interior de un contenedor que existe y todavía no tiene nada.
 *
 * **Una sola acción, nunca dos.** Si el contenedor tuviera dos cosas que
 * hacer, no sabría qué es. Buscar y no encontrar es otra cosa y tiene su
 * propio componente: ver `NoResults`.
 *
 * @param note Dato secundario, cierto y **no accionable**, después de la
 *   acción — «Se importan desde un archivo más adelante». No es una segunda
 *   acción disfrazada: si invita a pulsar algo, va en `action`.
 */
export function EmptyState({ title, description, action, note }: EmptyStateProps) {
  return (
    <div className="ui-empty">
      <p className="ui-empty__title">{title}</p>
      {description && <p className="ui-empty__description">{description}</p>}
      {action}
      {note && <p className="ui-empty__note">{note}</p>}
    </div>
  );
}

/* --- Spinner ------------------------------------------------------------ */

/**
 * Indicador de espera indeterminada.
 *
 * **El anillo es apoyo visual; lo que informa es el texto.** Va dentro de una
 * región `role="status"` (que implica `aria-live="polite"` y `aria-atomic`),
 * porque un `sr-only` suelto se puede leer pero **no se anuncia**: quien usa
 * un lector de pantalla no se entera de que algo empezó a cargar. Era la
 * técnica de fallo F103 de WCAG, y el defecto existía girase o no el anillo.
 *
 * El giro no necesita excepción bajo `prefers-reduced-motion`: la regla
 * global de `base.css` ya lo detiene, y con el anillo quieto el estado sigue
 * comunicándose por el texto. WCAG 2.2 §2.2.2 admite que la animación de
 * precarga sea esencial, pero solo cuando no hay otro canal — aquí lo hay.
 *
 * @param label Qué se está esperando, en español y nombrando la tarea:
 *   «Cargando módulos», no «Cargando». Si varias zonas cargan a la vez, una
 *   sucesión de «Cargando…» sin objeto no dice nada.
 * @param visibleLabel Enseña el texto, no solo lo anuncia. **Por defecto sí**:
 *   con movimiento reducido el anillo se queda quieto, y si el texto fuera
 *   invisible quien ve se quedaría mirando un círculo parado sin más — el
 *   defecto original, entero. Solo se pone en `false` donde otra cosa ya diga
 *   visiblemente qué se espera.
 * @param announce Si este indicador es además la región viva. **Se pone en
 *   `false` cuando quien lo usa ya tiene la suya**, como la tabla: dos
 *   regiones `status` para la misma espera se anuncian dos veces, y la de
 *   dentro cae encima del `aria-busy`, que puede posponer justo ese anuncio.
 */
export function Spinner({
  size = 'md',
  label,
  visibleLabel = true,
  announce = true,
}: {
  size?: 'sm' | 'md' | 'lg';
  label?: string;
  visibleLabel?: boolean;
  announce?: boolean;
}) {
  return (
    <span className="ui-spinner-wrap" role={announce ? 'status' : undefined}>
      <span className={`ui-spinner ui-spinner--${size}`} aria-hidden="true" />
      {label && <span className={visibleLabel ? undefined : 'sr-only'}>{label}</span>}
    </span>
  );
}
