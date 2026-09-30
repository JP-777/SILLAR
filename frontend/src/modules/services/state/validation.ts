export interface ServiceDraft {
  name: string;
  slug: string;
  shortDescription: string;
  description: string;
  price: string;
  imageId: string | null;
  imageAltText: string;
}

export type ServiceDraftErrors = Partial<Record<
  'name' | 'slug' | 'shortDescription' | 'description' | 'price' | 'imageAltText',
  string
>>;

/**
 * Cortesía focal antes de enviar. Refleja ServiceRules, pero no sustituye la
 * validación autoritativa del backend (por ejemplo, existencia del medio).
 */
export function validateServiceDraft(draft: ServiceDraft): ServiceDraftErrors {
  const errors: ServiceDraftErrors = {};

  if (draft.name.trim() === '') {
    errors.name = 'El nombre del servicio es obligatorio.';
  }

  // El backend deriva la dirección desde el nombre cuando el campo queda
  // vacío. Se valida el resultado, no se obliga a escribir dos veces lo mismo.
  if (slugify(draft.slug.trim() || draft.name.trim()) === '') {
    errors.slug = 'Escribe una dirección válida o un nombre del que pueda generarse.';
  }

  if (draft.shortDescription.trim() === '' && draft.description.trim() === '') {
    const message = 'Escribe una descripción breve o una descripción completa.';
    errors.shortDescription = message;
    errors.description = message;
  }

  if (draft.price.trim() !== '') {
    const price = Number(draft.price);
    if (!Number.isFinite(price) || price < 0) {
      errors.price = 'Escribe un importe igual o mayor que cero.';
    }
  }

  if (draft.imageId && draft.imageAltText.trim() === '') {
    errors.imageAltText = 'Describe la imagen para quienes no pueden verla.';
  }

  return errors;
}

function slugify(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}
