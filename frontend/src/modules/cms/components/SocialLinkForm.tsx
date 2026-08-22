import { useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { Button, Field, Input } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { socialLinksService, type SocialLinkAdmin } from '../services/socialLinks';

interface SocialLinkFormProps {
  open: boolean;
  link: SocialLinkAdmin | null;
  onClose: () => void;
  onSaved: (link: SocialLinkAdmin, message: string) => void;
}

const PLATFORMS = [
  { value: 'facebook', label: 'Facebook' },
  { value: 'instagram', label: 'Instagram' },
  { value: 'tiktok', label: 'TikTok' },
  { value: 'whatsapp', label: 'WhatsApp' },
  { value: 'youtube', label: 'YouTube' },
] as const;

/** Alta y edición de una red social; el ciclo de vida usa operaciones separadas. */
export function SocialLinkForm({ open, link, onClose, onSaved }: SocialLinkFormProps) {
  const editing = link !== null;
  const [platform, setPlatform] = useState(link?.platform ?? '');
  const [url, setUrl] = useState(link?.url ?? '');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);

    const request = {
      platform: optional(platform),
      url: optional(url),
    };

    try {
      const saved = editing
        ? await socialLinksService.update(link.id, request)
        : await socialLinksService.create(request);
      onSaved(
        saved,
        editing
          ? `Se guardaron los cambios de ${saved.platform}.`
          : `Se añadió ${saved.platform} a las redes sociales.`,
      );
    } catch (error) {
      setFailure(describe(error, editing ? 'guardar la red social' : 'crear la red social'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? `Editar ${link.platform}` : 'Nueva red social'}
      description="Editar no activa ni desactiva la red; esas acciones tienen permisos separados."
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>Cancelar</Button>
          <Button type="submit" form="formulario-red-social-cms" loading={busy}>
            {editing ? 'Guardar cambios' : 'Añadir red social'}
          </Button>
        </>
      }
    >
      <form id="formulario-red-social-cms" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure} />

        <Field label="Red social" required>
          {(props) => (
            <select
              {...props}
              className="ui-input"
              value={platform}
              onChange={(event) => setPlatform(event.target.value)}
            >
              <option value="">Elige una red</option>
              {PLATFORMS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          )}
        </Field>

        <Field label="Dirección" hint="URL completa HTTP o HTTPS." required>
          {(props) => (
            <Input
              {...props}
              type="url"
              value={url}
              onChange={(event) => setUrl(event.target.value)}
              placeholder="https://…"
            />
          )}
        </Field>
      </form>
    </Drawer>
  );
}

function optional(value: string): string | null {
  return value === '' ? null : value;
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
