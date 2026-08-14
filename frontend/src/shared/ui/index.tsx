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
    <button className={classes} disabled={disabled || loading} aria-busy={loading} {...rest}>
      {loading && <Spinner size="sm" />}
      {children}
    </button>
  );
}

/* --- Input -------------------------------------------------------------- */

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

export function Input({ invalid = false, className, ...rest }: InputProps) {
  const classes = ['ui-input', invalid ? 'ui-input--invalid' : '', className ?? '']
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
  action?: ReactNode;
}

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="ui-empty">
      <p className="ui-empty__title">{title}</p>
      {description && <p className="ui-empty__description">{description}</p>}
      {action}
    </div>
  );
}

/* --- Spinner ------------------------------------------------------------ */

export function Spinner({ size = 'md', label }: { size?: 'sm' | 'md' | 'lg'; label?: string }) {
  return (
    <>
      <span className={`ui-spinner ui-spinner--${size}`} aria-hidden="true" />
      {label && <span className="sr-only">{label}</span>}
    </>
  );
}
