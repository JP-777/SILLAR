import { useCallback } from 'react';
import { useResource } from '../hooks/useResource';
import { Button, EmptyState, Spinner } from '../ui';
import { galleryService, type GalleryImage } from './gallery';
import './image-picker.css';

interface ImagePickerProps {
  /** Imagen elegida, o `null`. */
  value: string | null;
  /** La URL de la elegida, para poder enseñarla sin volver a buscarla. */
  previewUrl: string | null;
  onChange: (mediaAssetId: string | null, url: string | null) => void;
}

/**
 * Elige una imagen **de la galería de CORE**. No sube archivos: subir es de
 * la galería, y tener dos sitios donde subir lo mismo acaba en dos criterios
 * distintos sobre qué formatos valen.
 *
 * Quitar la imagen **no borra el archivo**: deshace la asociación y nada más.
 *
 * No lleva etiqueta propia: quien lo usa lo envuelve en su `Field`, que es
 * quien sabe si esto es «Logotipo» o «Imagen de la categoría». Fue la única
 * diferencia real entre el de marcas y el de categorías, y no justificaba dos
 * componentes.
 */
export function ImagePicker({ value, previewUrl, onChange }: ImagePickerProps) {
  const load = useCallback(() => galleryService.list(), []);
  const { state } = useResource(load, 'cargar la galería');

  const images = state.status === 'ready' ? state.data.items : [];

  return (
    <div className="ui-picker">
      {value && previewUrl && (
        <div className="ui-picker__current">
          <img src={previewUrl} alt="" className="ui-picker__preview" />
          <Button size="sm" variant="ghost" onClick={() => onChange(null, null)}>
            Quitar imagen
          </Button>
        </div>
      )}

      {state.status === 'loading' && (
        <div className="ui-picker__state">
          <Spinner size="sm" label="Cargando la galería" />
        </div>
      )}

      {state.status === 'error' && <p className="ui-picker__state">{state.failure.message}</p>}

      {state.status === 'ready' && images.length === 0 && (
        <EmptyState
          title="No hay imágenes todavía"
          description="Sube la primera desde Archivos y vuelve aquí para elegirla."
        />
      )}

      {images.length > 0 && (
        <ul className="ui-picker__grid">
          {images.map((image) => (
            <Option
              key={image.mediaAssetId}
              image={image}
              selected={image.mediaAssetId === value}
              onPick={() => onChange(image.mediaAssetId, image.url)}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

function Option({
  image,
  selected,
  onPick,
}: {
  image: GalleryImage;
  selected: boolean;
  onPick: () => void;
}) {
  // El nombre del archivo, nunca el identificador: los `uuid` no se muestran.
  const label = image.originalName ?? 'Imagen sin nombre';

  return (
    <li>
      <button type="button" className="ui-picker__option" aria-pressed={selected} onClick={onPick}>
        <img src={image.url} alt={image.altText ?? ''} loading="lazy" />
        <span className="ui-picker__name">{label}</span>
      </button>
    </li>
  );
}
