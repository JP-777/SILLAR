import { useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { Button, Field, Input, Switch } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { ImagePicker } from '../../../shared/media/ImagePicker';
import { brandsService, type Brand } from '../services/brands';

interface BrandFormProps {
  open: boolean;
  /** Marca a editar, o `null` para crear una nueva. */
  brand: Brand | null;
  onClose: () => void;
  onSaved: (brand: Brand, message: string) => void;
}

/**
 * Alta y edición de una marca, en panel lateral.
 *
 * La validación es **al enviar**, no en cada tecla: corregir a alguien
 * mientras escribe es molestarle antes de que haya terminado. Mismo criterio
 * que el formulario de usuarios de CORE.
 */
export function BrandForm({ open, brand, onClose, onSaved }: BrandFormProps) {
  const editing = brand !== null;

  const [name, setName] = useState(brand?.name ?? '');
  const [slug, setSlug] = useState(brand?.slug ?? '');
  const [logoId, setLogoId] = useState<string | null>(brand?.logoId ?? null);
  const [logoUrl, setLogoUrl] = useState<string | null>(brand?.logoUrl ?? null);
  const [isActive, setIsActive] = useState(brand?.isActive ?? true);

  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);
    setFieldErrors({});

    try {
      const trimmedSlug = slug.trim();

      const saved = editing
        ? await brandsService.update(brand.id, {
            name: name.trim(),
            slug: trimmedSlug,
            logoId,
            isActive,
          })
        : await brandsService.create({
            name: name.trim(),
            // Vacío significa «genéralo tú del nombre», que es lo que hace el
            // servidor. Enviar "" sería pedir un slug vacío.
            slug: trimmedSlug === '' ? null : trimmedSlug,
            logoId,
          });

      onSaved(
        saved,
        editing ? `Se guardaron los cambios de «${saved.name}».` : `Se creó la marca «${saved.name}».`,
      );
    } catch (error) {
      // El 409 de nombre o slug repetido llega con la frase ya redactada por
      // el servidor, que explica lo de las mayúsculas cuando toca. No se
      // reescribe aquí: sería tener la misma frase en dos sitios.
      const described = describe(error, editing ? 'guardar la marca' : 'crear la marca');
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
      title={editing ? `Editar ${brand.name}` : 'Nueva marca'}
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
          <Button type="submit" form="formulario-marca" loading={busy}>
            {editing ? 'Guardar cambios' : 'Crear marca'}
          </Button>
        </>
      }
    >
      <form id="formulario-marca" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure?.kind === 'inline' ? failure : null} />

        <Field label="Nombre" required error={fieldErrors.marca ?? null}>
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
              ? 'Cambiarla rompe los enlaces que ya circulen a esta marca.'
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
              placeholder="artesco"
            />
          )}
        </Field>

        <Field label="Logotipo" hint="Se elige de Archivos. Quitarlo aquí no borra el archivo.">
          {() => (
            <ImagePicker
              value={logoId}
              previewUrl={logoUrl}
              onChange={(id, url) => {
                setLogoId(id);
                setLogoUrl(url);
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
