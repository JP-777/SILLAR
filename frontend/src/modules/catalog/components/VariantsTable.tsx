import { useRef, useState } from 'react';
import { Button, Input } from '../../../shared/ui';
import { formatPrice, priceKind } from '../services/publicCatalog';
import { priceFromInput, priceToInput } from '../services/products';
import './catalog.css';

/** Una fila de la tabla, mientras se edita. Sin `id` hasta que se guarda. */
export interface VariantDraft {
  /** Identificador si ya existe en la base. `null` mientras es nueva. */
  id: string | null;
  variantValue: string;
  code: string;
  barcode: string;
  /** Cadena vacía significa **heredar**, no cero. */
  priceOverride: string;
}

interface VariantsTableProps {
  rows: VariantDraft[];
  onChange: (rows: VariantDraft[]) => void;
  /** Precio del producto, que es de lo que heredan las filas sin precio propio. */
  listPrice: number | null;
  /** Cómo se llama lo que varía. Titula el bloque; nunca la palabra «variante». */
  label: string;
  onLabelChange: (label: string) => void;
  /** Volver a una sola presentación. */
  onCollapse: () => void;
  /**
   * Qué fila recibe el cursor al montar la tabla.
   *
   * Es el tercer paso del momento: el cursor en la segunda presentación es lo
   * que convierte el aviso en instrucción. Sin esto el aviso dice qué pasó
   * pero no dónde seguir.
   */
  initialFocusRow?: number;
}

/**
 * La tabla de presentaciones.
 *
 * **Compuesta a mano, y es el caso uno.** `Table` es de solo lectura: no tiene
 * noción de fila con campos, ni de columna cuyo control cambia según el
 * valor, ni de fila en conflicto. Se extrae un `Table` editable cuando
 * aparezca el segundo caso real — construirlo ahora sería hacerlo sobre una
 * predicción, y sale caro.
 *
 * **En móvil deja de ser tabla y pasa a una tarjeta por presentación.** Se
 * pierde comparar de un vistazo; no se pierde ningún campo ni ninguna acción.
 * Lo hace el CSS con `data-label`, así que el marcado es uno solo.
 */
export function VariantsTable({
  rows,
  onChange,
  listPrice,
  label,
  onLabelChange,
  onCollapse,
  initialFocusRow,
}: VariantsTableProps) {
  // La fila que hay que enfocar: al montar, la que diga quien abre la tabla;
  // después, la que se acabe de añadir. Se usa una sola vez cada vez.
  const porEnfocar = useRef<number | null>(initialFocusRow ?? null);

  function actualizar(indice: number, cambio: Partial<VariantDraft>) {
    onChange(rows.map((fila, i) => (i === indice ? { ...fila, ...cambio } : fila)));
  }

  function añadir() {
    porEnfocar.current = rows.length;
    onChange([...rows, { id: null, variantValue: '', code: '', barcode: '', priceOverride: '' }]);
  }

  // La última fila decide el texto del botón de volver: no es lo mismo
  // deshacer algo vacío que destruir lo que alguien escribió.
  const última = rows[rows.length - 1];
  const últimaTieneAlgo =
    última !== undefined &&
    (última.variantValue.trim() !== '' ||
      última.code.trim() !== '' ||
      última.barcode.trim() !== '' ||
      última.priceOverride.trim() !== '');

  return (
    <section className="cat-variants" aria-label="Presentaciones del producto">
      <div className="cat-variants__head">
        {/* La etiqueta nombra al campo, no a la sección: con `aria-labelledby`
            apuntando aquí, la sección tomaba el mismo nombre accesible y
            «Qué cambia entre ellas» pasaba a casar con dos elementos. */}
        <label className="ui-field__label" htmlFor="etiqueta-variantes">
          Qué cambia entre ellas
        </label>
        <Input
          id="etiqueta-variantes"
          value={label}
          onChange={(event) => onLabelChange(event.target.value)}
          placeholder="Color, Tamaño, Presentación"
          maxLength={60}
        />
      </div>

      <div className="cat-variants__grid" role="table" aria-label="Presentaciones">
        <div className="cat-variants__row cat-variants__row--head" role="row">
          {/* En móvil la cabecera se queda en el ordinal: el eje ya titula el
              campo de dentro, y repetirlo sería decirlo dos veces. */}
          <span role="columnheader">#</span>
          <span role="columnheader">{label.trim() === '' ? 'Valor' : label}</span>
          <span role="columnheader">Código</span>
          <span role="columnheader">Código de barras</span>
          <span role="columnheader">Precio</span>
          <span role="columnheader">
            <span className="sr-only">Acciones</span>
          </span>
        </div>

        {rows.map((fila, indice) => (
          <div className="cat-variants__row" role="row" key={fila.id ?? `nueva-${indice}`}>
            <span className="cat-variants__ordinal" role="cell">
              {indice + 1}
            </span>

            <span role="cell" data-label={label.trim() === '' ? 'Valor' : label}>
              <Input
                // El nombre accesible sin etiqueta visible: `Input` reparte
                // las props que no conoce sobre el <input>, así que
                // `aria-label` llega. Comprobado, no supuesto.
                aria-label={`${label.trim() === '' ? 'Valor' : label} de la presentación ${indice + 1}`}
                value={fila.variantValue}
                onChange={(event) => actualizar(indice, { variantValue: event.target.value })}
                autoFocus={porEnfocar.current === indice}
                maxLength={60}
              />
            </span>

            <span role="cell" data-label="Código">
              <Input
                aria-label={`Código de la presentación ${indice + 1}`}
                value={fila.code}
                onChange={(event) => actualizar(indice, { code: event.target.value })}
                maxLength={60}
              />
            </span>

            <span role="cell" data-label="Código de barras">
              <Input
                aria-label={`Código de barras de la presentación ${indice + 1}`}
                value={fila.barcode}
                onChange={(event) => actualizar(indice, { barcode: event.target.value })}
                maxLength={60}
              />
            </span>

            <span role="cell" data-label="Precio">
              <PriceCell
                value={fila.priceOverride}
                listPrice={listPrice}
                onChange={(valor) => actualizar(indice, { priceOverride: valor })}
                ordinal={indice + 1}
              />
            </span>

            <span role="cell">
              {rows.length > 1 && (
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => onChange(rows.filter((_, i) => i !== indice))}
                >
                  Quitar
                </Button>
              )}
            </span>
          </div>
        ))}
      </div>

      {/* Dos casillas de código vacías seguidas parecen un olvido aunque no lo
          sean. Se dice que así está bien — sin marca, sin asterisco, sin
          tratarlo como conflicto, porque no lo es. */}
      {rows.filter((fila) => fila.code.trim() === '').length > 1 && (
        <p className="cat-variants__ok">
          Varias presentaciones sin código: está bien. El código solo hace falta para teclearlas
          en caja.
        </p>
      )}

      <div className="cat-variants__actions">
        <Button size="sm" variant="secondary" onClick={añadir}>
          Añadir presentación
        </Button>

        {/* El mismo botón, dos textos: no es lo mismo deshacer algo vacío que
            destruir lo que alguien acaba de escribir. */}
        <Button size="sm" variant="ghost" onClick={onCollapse}>
          {últimaTieneAlgo ? 'Quitar la última presentación' : 'Volver a una sola presentación'}
        </Button>
      </div>
    </section>
  );
}

/**
 * La celda de precio, que **nunca queda en blanco**.
 *
 * Sin precio propio dice **de qué hereda y con qué valor**: heredar un número
 * y heredar un «a consultar» se ven igual si la celda está vacía, y no son lo
 * mismo para quien vende. Y es pulsable, para pasar a precio propio con el
 * heredado ya cargado.
 */
function PriceCell({
  value,
  listPrice,
  onChange,
  ordinal,
}: {
  value: string;
  listPrice: number | null;
  onChange: (valor: string) => void;
  ordinal: number;
}) {
  const [editando, setEditando] = useState(value.trim() !== '');

  if (editando) {
    return (
      <span className="cat-variants__price">
        <Input
          aria-label={`Precio de la presentación ${ordinal}`}
          type="number"
          inputMode="decimal"
          min={0}
          step="0.01"
          value={value}
          autoFocus
          onChange={(event) => onChange(event.target.value)}
        />
        <Button
          size="sm"
          variant="ghost"
          onClick={() => {
            onChange('');
            setEditando(false);
          }}
        >
          Heredar
        </Button>
      </span>
    );
  }

  const kind = priceKind(listPrice);
  const texto =
    kind === 'consultar'
      ? 'Hereda: a consultar'
      : kind === 'gratis'
        ? 'Hereda: gratis'
        : `Hereda ${formatPrice(listPrice!)}`;

  return (
    <button
      type="button"
      className="cat-variants__inherit"
      aria-label={`Precio de la presentación ${ordinal}: ${texto}. Pulsa para poner uno propio`}
      onClick={() => {
        // Se carga el heredado, no un campo vacío: quien quiere un precio
        // propio casi siempre quiere uno parecido al que había.
        onChange(listPrice === null ? '' : priceToInput(listPrice));
        setEditando(true);
      }}
    >
      {texto}
    </button>
  );
}

/** Convierte las filas del formulario en lo que espera el API. */
export function draftToPayload(fila: VariantDraft) {
  return {
    variantValue: fila.variantValue.trim() === '' ? null : fila.variantValue.trim(),
    code: fila.code.trim() === '' ? null : fila.code.trim(),
    barcode: fila.barcode.trim() === '' ? null : fila.barcode.trim(),
    priceOverride: priceFromInput(fila.priceOverride),
  };
}
