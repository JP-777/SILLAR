import { useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { ImagePicker } from '../../../shared/media/ImagePicker';
import { Button, Field, Input } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { bannersService, type BannerAdmin } from '../services/banners';
import { CmsPublicationFields } from './CmsPublicationFields';

interface BannerFormProps {
  open: boolean;
  banner: BannerAdmin | null;
  onClose: () => void;
  onSaved: (banner: BannerAdmin, message: string) => void;
}

/** Alta y edición de banners, sin alterar orden ni estado editorial. */
export function BannerForm({ open, banner, onClose, onSaved }: BannerFormProps) {
  const editing = banner !== null;
  const [title, setTitle] = useState(banner?.title ?? '');
  const [subtitle, setSubtitle] = useState(banner?.subtitle ?? '');
  const [imageDesktopId, setImageDesktopId] = useState<string | null>(banner?.imageDesktopId ?? null);
  const [imageDesktopUrl, setImageDesktopUrl] = useState<string | null>(banner?.imageDesktopUrl ?? null);
  const [imageMobileId, setImageMobileId] = useState<string | null>(banner?.imageMobileId ?? null);
  const [imageMobileUrl, setImageMobileUrl] = useState<string | null>(banner?.imageMobileUrl ?? null);
  const [altText, setAltText] = useState(banner?.altText ?? '');
  const [linkUrl, setLinkUrl] = useState(banner?.linkUrl ?? '');
  const [linkLabel, setLinkLabel] = useState(banner?.linkLabel ?? '');
  const [startsAt, setStartsAt] = useState<string | null>(banner?.startsAt ?? null);
  const [endsAt, setEndsAt] = useState<string | null>(banner?.endsAt ?? null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);

    const request = {
      title: optional(title),
      subtitle: optional(subtitle),
      imageDesktopId,
      imageMobileId,
      altText: optional(altText),
      linkUrl: optional(linkUrl),
      linkLabel: optional(linkLabel),
      startsAt,
      endsAt,
    };

    try {
      const saved = editing
        ? await bannersService.update(banner.id, request)
        : await bannersService.create(request);
      const name = saved.title ?? `banner #${saved.id}`;
      onSaved(saved, editing ? `Se guardaron los cambios de «${name}».` : `Se creó «${name}».`);
    } catch (error) {
      setFailure(describe(error, editing ? 'guardar el banner' : 'crear el banner'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? `Editar ${banner.title ?? `banner #${banner.id}`}` : 'Nuevo banner'}
      description="La vigencia y la completitud las decide CMS al guardar."
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>Cancelar</Button>
          <Button type="submit" form="formulario-banner-cms" loading={busy}>
            {editing ? 'Guardar cambios' : 'Crear banner'}
          </Button>
        </>
      }
    >
      <form id="formulario-banner-cms" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure} />

        <Field label="Título">
          {(props) => <Input {...props} value={title} onChange={(event) => setTitle(event.target.value)} />}
        </Field>

        <Field label="Subtítulo">
          {(props) => <Input {...props} value={subtitle} onChange={(event) => setSubtitle(event.target.value)} />}
        </Field>

        <Field label="Imagen de escritorio" hint="Elegirla no publica el banner por sí solo.">
          {() => (
            <ImagePicker
              value={imageDesktopId}
              previewUrl={imageDesktopUrl}
              onChange={(id, url) => {
                setImageDesktopId(id);
                setImageDesktopUrl(url);
              }}
            />
          )}
        </Field>

        <Field label="Imagen móvil" hint="Es opcional; si falta, la web puede usar la imagen de escritorio.">
          {() => (
            <ImagePicker
              value={imageMobileId}
              previewUrl={imageMobileUrl}
              onChange={(id, url) => {
                setImageMobileId(id);
                setImageMobileUrl(url);
              }}
            />
          )}
        </Field>

        <Field label="Texto alternativo" hint="Es obligatorio cuando el banner tiene alguna imagen.">
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
