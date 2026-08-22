import { useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { ImagePicker } from '../../../shared/media/ImagePicker';
import { Button, Field, Input } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { promotionsService, type PromotionAdmin } from '../services/promotions';
import { CmsPublicationFields } from './CmsPublicationFields';

interface PromotionFormProps {
  open: boolean;
  promotion: PromotionAdmin | null;
  onClose: () => void;
  onSaved: (promotion: PromotionAdmin, message: string) => void;
}

/** Alta y edición de promociones, sin alterar orden ni estado editorial. */
export function PromotionForm({ open, promotion, onClose, onSaved }: PromotionFormProps) {
  const editing = promotion !== null;
  const [title, setTitle] = useState(promotion?.title ?? '');
  const [subtitle, setSubtitle] = useState(promotion?.subtitle ?? '');
  const [description, setDescription] = useState(promotion?.description ?? '');
  const [badgeText, setBadgeText] = useState(promotion?.badgeText ?? '');
  const [imageId, setImageId] = useState<string | null>(promotion?.imageId ?? null);
  const [imageUrl, setImageUrl] = useState<string | null>(promotion?.imageUrl ?? null);
  const [altText, setAltText] = useState(promotion?.altText ?? '');
  const [linkUrl, setLinkUrl] = useState(promotion?.linkUrl ?? '');
  const [linkLabel, setLinkLabel] = useState(promotion?.linkLabel ?? '');
  const [startsAt, setStartsAt] = useState<string | null>(promotion?.startsAt ?? null);
  const [endsAt, setEndsAt] = useState<string | null>(promotion?.endsAt ?? null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);

    const request = {
      title: optional(title),
      subtitle: optional(subtitle),
      description: optional(description),
      badgeText: optional(badgeText),
      imageId,
      altText: optional(altText),
      linkUrl: optional(linkUrl),
      linkLabel: optional(linkLabel),
      startsAt,
      endsAt,
    };

    try {
      const saved = editing
        ? await promotionsService.update(promotion.id, request)
        : await promotionsService.create(request);
      const name = saved.title ?? `promoción #${saved.id}`;
      onSaved(saved, editing ? `Se guardaron los cambios de «${name}».` : `Se creó «${name}».`);
    } catch (error) {
      setFailure(describe(error, editing ? 'guardar la promoción' : 'crear la promoción'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? `Editar ${promotion.title ?? `promoción #${promotion.id}`}` : 'Nueva promoción'}
      description="La vigencia la calcula CMS; esta pantalla solo envía sus límites."
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>Cancelar</Button>
          <Button type="submit" form="formulario-promocion-cms" loading={busy}>
            {editing ? 'Guardar cambios' : 'Crear promoción'}
          </Button>
        </>
      }
    >
      <form id="formulario-promocion-cms" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure} />

        <Field label="Título">
          {(props) => <Input {...props} value={title} onChange={(event) => setTitle(event.target.value)} />}
        </Field>

        <Field label="Subtítulo">
          {(props) => <Input {...props} value={subtitle} onChange={(event) => setSubtitle(event.target.value)} />}
        </Field>

        <Field label="Descripción">
          {(props) => (
            <textarea
              {...props}
              className="ui-input"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              rows={4}
            />
          )}
        </Field>

        <Field label="Etiqueta" hint="Texto breve, de hasta 20 caracteres.">
          {(props) => (
            <Input
              {...props}
              value={badgeText}
              onChange={(event) => setBadgeText(event.target.value)}
              maxLength={20}
            />
          )}
        </Field>

        <Field label="Imagen" hint="La promoción puede publicarse también sin imagen.">
          {() => (
            <ImagePicker
              value={imageId}
              previewUrl={imageUrl}
              onChange={(id, url) => {
                setImageId(id);
                setImageUrl(url);
              }}
            />
          )}
        </Field>

        <Field label="Texto alternativo" hint="Es obligatorio cuando la promoción tiene imagen.">
          {(props) => <Input {...props} value={altText} onChange={(event) => setAltText(event.target.value)} />}
        </Field>

        <Field label="Enlace" hint="Ruta interna /… o URL completa HTTP(S).">
          {(props) => <Input {...props} value={linkUrl} onChange={(event) => setLinkUrl(event.target.value)} />}
        </Field>

        <Field label="Texto del enlace" hint="Es obligatorio cuando se indica un enlace.">
          {(props) => <Input {...props} value={linkLabel} onChange={(event) => setLinkLabel(event.target.value)} />}
        </Field>

        <CmsPublicationFields
          startsAt={startsAt}
          endsAt={endsAt}
          onStartsAtChange={setStartsAt}
          onEndsAtChange={setEndsAt}
        />
      </form>
    </Drawer>
  );
}

function optional(value: string): string | null {
  return value === '' ? null : value;
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
