import { useState, type FormEvent } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { ImagePicker } from '../../../shared/media/ImagePicker';
import { Button, Field, Input } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { servicesService, type AdminService, type SaveServiceRequest } from '../services/services';

export function ServiceForm({ service, onClose, onSaved }: { service: AdminService | null; onClose: () => void; onSaved: (value: AdminService) => void }) {
  const [name, setName] = useState(service?.name ?? '');
  const [slug, setSlug] = useState(service?.slug ?? '');
  const [shortDescription, setShortDescription] = useState(service?.shortDescription ?? '');
  const [description, setDescription] = useState(service?.description ?? '');
  const [price, setPrice] = useState(service?.price?.toString() ?? '');
  const [saleUnit, setSaleUnit] = useState(service?.saleUnit ?? '');
  const [imageId, setImageId] = useState<string | null>(service?.imageId ?? null);
  const [imageUrl, setImageUrl] = useState<string | null>(service?.imageUrl ?? null);
  const [imageAltText, setImageAltText] = useState(service?.imageAltText ?? '');
  const [failure, setFailure] = useState<Failure | null>(null);
  const [busy, setBusy] = useState(false);
  const fields = failure?.fieldErrors ?? {};

  async function submit(event: FormEvent) {
    event.preventDefault();
    setFailure(null);
    const amount = price.trim() === '' ? null : Number(price);
    if (amount !== null && (!Number.isFinite(amount) || amount < 0)) {
      setFailure({ kind: 'validation', message: 'Revisa el precio.', fieldErrors: { price: 'Escribe un importe igual o mayor que cero.' }, blockedBy: null });
      return;
    }
    const request: SaveServiceRequest = {
      name: name.trim(), slug: slug.trim(), shortDescription: optional(shortDescription),
      description: optional(description), price: amount, saleUnit: optional(saleUnit), imageId,
      imageAltText: imageId ? optional(imageAltText) : null,
    };
    setBusy(true);
    try {
      onSaved(service ? await servicesService.update(service.id, request) : await servicesService.create(request));
    } catch (error) {
      // Conserva todo el estado local: slug duplicado, medio inválido y demás
      // respuestas reales se corrigen sin volver a escribir el formulario.
      setFailure(describe(error, service ? 'guardar el servicio' : 'crear el servicio'));
    } finally { setBusy(false); }
  }

  return (
    <Drawer open title={service ? `Editar ${service.name}` : 'Nuevo servicio'} description="La publicación se gestiona después de guardar." onClose={onClose}
      footer={<><Button variant="secondary" onClick={onClose} disabled={busy}>Cancelar</Button><Button type="submit" form="service-form" loading={busy}>{service ? 'Guardar cambios' : 'Crear servicio'}</Button></>}>
      <form id="service-form" onSubmit={submit} noValidate className="sv-form">
        <FailureAlert failure={failure} />
        <Field label="Nombre" required error={fields.name}>{(props) => <Input {...props} value={name} onChange={(e) => setName(e.target.value)} />}</Field>
        <Field label="Dirección" required hint="Se usa en /servicios/dirección." error={fields.slug}>{(props) => <Input {...props} value={slug} onChange={(e) => setSlug(e.target.value)} />}</Field>
        <Field label="Descripción breve" error={fields.shortDescription}>{(props) => <textarea {...props} className="ui-input sv-textarea" value={shortDescription} onChange={(e) => setShortDescription(e.target.value)} />}</Field>
        <Field label="Descripción" error={fields.description}>{(props) => <textarea {...props} className="ui-input sv-textarea sv-textarea--long" value={description} onChange={(e) => setDescription(e.target.value)} />}</Field>
        <div className="sv-form__row">
          <Field label="Precio" hint="Vacío significa precio a consultar." error={fields.price}>{(props) => <Input {...props} type="number" min="0" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} />}</Field>
          <Field label="Unidad de venta" error={fields.saleUnit}>{(props) => <Input {...props} value={saleUnit} onChange={(e) => setSaleUnit(e.target.value)} />}</Field>
        </div>
        <Field label="Imagen" hint="Solo se eligen medios activos de CORE." error={fields.imageId}>{() => <ImagePicker value={imageId} previewUrl={imageUrl} onChange={(id, url) => { setImageId(id); setImageUrl(url); }} />}</Field>
        {imageId && <Field label="Texto alternativo" required error={fields.imageAltText}>{(props) => <Input {...props} value={imageAltText} onChange={(e) => setImageAltText(e.target.value)} />}</Field>}
      </form>
    </Drawer>
  );
}

function optional(value: string): string | null { return value.trim() === '' ? null : value.trim(); }
