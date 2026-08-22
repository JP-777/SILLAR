import { useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { ImagePicker } from '../../../shared/media/ImagePicker';
import { Button, Field, Input } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { featuredProjectsService, type FeaturedProjectAdmin } from '../services/featuredProjects';

interface FeaturedProjectFormProps {
  open: boolean;
  project: FeaturedProjectAdmin | null;
  onClose: () => void;
  onSaved: (project: FeaturedProjectAdmin, message: string) => void;
}

/** Alta y edición de trabajos destacados, sin alterar orden ni estado editorial. */
export function FeaturedProjectForm({
  open,
  project,
  onClose,
  onSaved,
}: FeaturedProjectFormProps) {
  const editing = project !== null;
  const [title, setTitle] = useState(project?.title ?? '');
  const [description, setDescription] = useState(project?.description ?? '');
  const [imageId, setImageId] = useState<string | null>(project?.imageId ?? null);
  const [imageUrl, setImageUrl] = useState<string | null>(project?.imageUrl ?? null);
  const [altText, setAltText] = useState(project?.altText ?? '');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);

    const request = {
      title: optional(title),
      description: optional(description),
      imageId,
      altText: optional(altText),
    };

    try {
      const saved = editing
        ? await featuredProjectsService.update(project.id, request)
        : await featuredProjectsService.create(request);
      onSaved(
        saved,
        editing
          ? `Se guardaron los cambios de «${saved.title}».`
          : `Se creó el trabajo «${saved.title}».`,
      );
    } catch (error) {
      setFailure(describe(error, editing ? 'guardar el trabajo destacado' : 'crear el trabajo destacado'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? `Editar ${project.title}` : 'Nuevo trabajo destacado'}
      description="Un trabajo sin imagen o texto alternativo se conserva como borrador incompleto."
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>Cancelar</Button>
          <Button type="submit" form="formulario-trabajo-cms" loading={busy}>
            {editing ? 'Guardar cambios' : 'Crear trabajo'}
          </Button>
        </>
      }
    >
      <form id="formulario-trabajo-cms" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure} />

        <Field label="Título" required>
          {(props) => <Input {...props} value={title} onChange={(event) => setTitle(event.target.value)} />}
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

        <Field label="Imagen" hint="Elegirla no publica el trabajo por sí solo.">
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

        <Field label="Texto alternativo" hint="Es obligatorio cuando el trabajo tiene imagen.">
          {(props) => <Input {...props} value={altText} onChange={(event) => setAltText(event.target.value)} />}
        </Field>
      </form>
    </Drawer>
  );
}

function optional(value: string): string | null {
  return value === '' ? null : value;
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
