import { Badge, Field, Input, type BadgeTone } from '../../../shared/ui';
import type { PublicationState } from '../services/contracts';

const PUBLICATION_LABELS: Record<PublicationState, { label: string; tone: BadgeTone }> = {
  inactive: { label: 'Inactivo', tone: 'neutral' },
  scheduled: { label: 'Programado', tone: 'warning' },
  current: { label: 'Vigente', tone: 'success' },
  expired: { label: 'Caducado', tone: 'danger' },
};

/** Presenta el estado calculado por CMS sin volver a interpretar sus fechas. */
export function CmsPublicationStateBadge({ state }: { state: PublicationState }) {
  const presentation = PUBLICATION_LABELS[state];
  return <Badge tone={presentation.tone}>{presentation.label}</Badge>;
}

interface CmsPublicationFieldsProps {
  startsAt: string | null;
  endsAt: string | null;
  onStartsAtChange: (value: string | null) => void;
  onEndsAtChange: (value: string | null) => void;
}

/** Campos de entrada de la ventana; no calculan si el contenido está vigente. */
export function CmsPublicationFields({
  startsAt,
  endsAt,
  onStartsAtChange,
  onEndsAtChange,
}: CmsPublicationFieldsProps) {
  return (
    <>
      <Field label="Inicio de publicación" hint="Vacío significa que no hay fecha mínima.">
        {(props) => (
          <Input
            {...props}
            type="datetime-local"
            value={toLocalDateTime(startsAt)}
            onChange={(event) => onStartsAtChange(toIsoDateTime(event.target.value))}
          />
        )}
      </Field>

      <Field label="Fin de publicación" hint="Vacío significa que no caduca por fecha.">
        {(props) => (
          <Input
            {...props}
            type="datetime-local"
            value={toLocalDateTime(endsAt)}
            onChange={(event) => onEndsAtChange(toIsoDateTime(event.target.value))}
          />
        )}
      </Field>
    </>
  );
}

/** Formato de lectura únicamente; la clasificación llega ya resuelta por backend. */
export function formatCmsDateTime(value: string | null, emptyLabel: string): string {
  if (!value) {
    return emptyLabel;
  }

  return new Date(value).toLocaleString('es-PE', {
    dateStyle: 'short',
    timeStyle: 'short',
  });
}

function toLocalDateTime(value: string | null): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

function toIsoDateTime(value: string): string | null {
  return value === '' ? null : new Date(value).toISOString();
}
