import { useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { ImagePicker } from '../../../shared/media/ImagePicker';
import { Button, Field, Input, Switch } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { categoriesService, possibleParents, type Category } from '../services/categories';

interface CategoryFormProps {
  open: boolean;
  /** Categoría a editar, o `null` para crear una nueva. */
  category: Category | null;
  /** Todas, para poder elegir el padre sin ofrecer un ciclo. */
  all: readonly Category[];
  onClose: () => void;
  onSaved: (category: Category, message: string) => void;
}

/** Alta y edición de una categoría, en panel lateral. */
export function CategoryForm({ open, category, all, onClose, onSaved }: CategoryFormProps) {
  const editing = category !== null;

  const [name, setName] = useState(category?.name ?? '');
  const [slug, setSlug] = useState(category?.slug ?? '');
  const [parentId, setParentId] = useState<string | null>(category?.parentId ?? null);
  const [description, setDescription] = useState(category?.description ?? '');
  const [imageId, setImageId] = useState<string | null>(category?.imageId ?? null);
  const [imageUrl, setImageUrl] = useState<string | null>(category?.imageUrl ?? null);
  const [isActive, setIsActive] = useState(category?.isActive ?? true);

  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const parents = possibleParents(all, category?.id ?? null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);
    setFieldErrors({});

    try {
      const trimmedSlug = slug.trim();
      const trimmedDescription = description.trim() === '' ? null : description.trim();

      const saved = editing
        ? await categoriesService.update(category.id, {
            name: name.trim(),
            slug: trimmedSlug,
            parentId,
            description: trimmedDescription,
            imageId,
            sortOrder: category.sortOrder,
            isActive,
          })
        : await categoriesService.create({
            name: name.trim(),
            slug: trimmedSlug === '' ? null : trimmedSlug,
            parentId,
            description: trimmedDescription,
            imageId,
            sortOrder: null,
          });

      onSaved(
        saved,
        editing
          ? `Se guardaron los cambios de «${saved.name}».`
          : `Se creó la categoría «${saved.name}».`,
      );
    } catch (error) {
      // El ciclo y el slug repetido llegan con la frase ya redactada por el
      // servidor. No se reescribe aquí.
      const described = describe(error, editing ? 'guardar la categoría' : 'crear la categoría');
      setFailure(described);

      if (described.fieldErrors) {
        setFieldErrors(described.fieldErrors);
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? `Editar ${category.name}` : 'Nueva categoría'}
      description={
        editing
          ? 'Cambiar el nombre no cambia la dirección web: se edita aparte, a propósito.'
          : undefined
      }
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Cancelar
          </Button>
          <Button type="submit" form="formulario-categoria" loading={busy}>
            {editing ? 'Guardar cambios' : 'Crear categoría'}
          </Button>
        </>
      }
    >
      <form id="formulario-categoria" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure?.kind === 'inline' ? failure : null} />

        <Field label="Nombre" required error={fieldErrors.categoria ?? null}>
          {(props) => (
            <Input
              {...props}
              value={name}
              onChange={(event) => setName(event.target.value)}
              maxLength={120}
            />
          )}
        </Field>

        <Field
          label="Dirección web"
          hint={
            editing
              ? 'Cambiarla rompe los enlaces que ya circulen a esta categoría.'
              : 'Si la dejas vacía se genera del nombre.'
          }
          required={editing}
        >
          {(props) => (
            <Input
              {...props}
              value={slug}
              onChange={(event) => setSlug(event.target.value)}
              maxLength={120}
              placeholder="cuadernos"
            />
          )}
        </Field>

        <Field
          label="Cuelga de"
          hint={
            editing
              ? 'No aparecen ni esta categoría ni las que dependen de ella: sería un ciclo.'
              : 'Déjalo en «Ninguna» para que sea una categoría principal.'
          }
        >
          {(props) => (
            <select
              {...props}
              className="ui-input"
              value={parentId ?? ''}
              onChange={(event) => setParentId(event.target.value || null)}
            >
              <option value="">Ninguna — es una categoría principal</option>
              {parents.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          )}
        </Field>

        <Field label="Descripción" hint="Se muestra en la web, encima de sus productos.">
          {(props) => (
            <textarea
              {...props}
              className="ui-input"
              rows={3}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              maxLength={500}
            />
          )}
        </Field>

        <Field label="Imagen" hint="Se elige de Archivos. Quitarla aquí no borra el archivo.">
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

        {editing && (
          <Switch
            checked={isActive}
            onChange={setIsActive}
            label={isActive ? 'Visible en la web' : 'Oculta en la web'}
          />
        )}
      </form>
    </Drawer>
  );
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
