import { useRef, useState, type DragEvent } from 'react';
import { Button, Field } from '../../../shared/ui';
import { ACCEPTED_TYPES, precheck } from '../services/media';
import '../../../shared/ui/gallery.css';

interface MediaUploaderProps {
  /** Módulos que el producto conoce, para elegir el dueño del archivo. */
  moduleCodes: readonly string[];
  ownerModuleCode: string;
  onOwnerChange: (code: string) => void;
  busy: boolean;
  /** Recibe los archivos ya filtrados por la comprobación de cortesía. */
  onFiles: (files: File[], rejected: string[]) => void;
}

/**
 * Zona de subida: arrastrar y soltar, más un botón.
 *
 * Las dos vías, porque arrastrar no funciona desde el móvil y el botón es lo
 * que espera quien no sabe que se puede arrastrar.
 */
export function MediaUploader({
  moduleCodes,
  ownerModuleCode,
  onOwnerChange,
  busy,
  onFiles,
}: MediaUploaderProps) {
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);

  function accept(list: FileList | null) {
    if (!list || list.length === 0) {
      return;
    }

    const files: File[] = [];
    const rejected: string[] = [];

    for (const file of list) {
      const reason = precheck(file);

      if (reason) {
        rejected.push(reason);
      } else {
        files.push(file);
      }
    }

    onFiles(files, rejected);
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setOver(false);
    accept(event.dataTransfer.files);
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--s4)' }}>
      <Field label="Módulo al que pertenecen" hint="Queda registrado como dueño del archivo.">
        {(props) => (
          <select
            {...props}
            className="ui-input"
            value={ownerModuleCode}
            onChange={(event) => onOwnerChange(event.target.value)}
          >
            {moduleCodes.map((code) => (
              <option key={code} value={code}>
                {code}
              </option>
            ))}
          </select>
        )}
      </Field>

      <div
        className="gal-drop"
        data-over={over}
        onDragOver={(event) => {
          event.preventDefault();
          setOver(true);
        }}
        onDragLeave={() => setOver(false)}
        onDrop={handleDrop}
      >
        <p className="gal-drop__title">Arrastra aquí tus imágenes</p>
        <p className="gal-drop__hint">JPEG, PNG o WebP. Hasta 5 MB cada una.</p>

        <Button variant="secondary" onClick={() => input.current?.click()} loading={busy}>
          Elegir archivos
        </Button>

        <input
          ref={input}
          type="file"
          multiple
          accept={ACCEPTED_TYPES.join(',')}
          className="sr-only"
          onChange={(event) => {
            accept(event.target.files);
            // Se limpia para que volver a elegir el mismo archivo dispare el
            // evento otra vez.
            event.target.value = '';
          }}
        />
      </div>
    </div>
  );
}
