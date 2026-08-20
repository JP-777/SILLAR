import { useId } from 'react';
import { asTree, type Category } from '../services/categories';
import './catalog.css';

interface CategoryAssignerProps {
  /** Todas las categorías, activas o no. El control decide cuáles enseña. */
  categories: readonly Category[];
  /** Las elegidas ahora mismo. */
  selected: readonly string[];
  /** Cuál de las elegidas es la principal. `null` si no hay ninguna elegida. */
  primary: string | null;
  onChange: (selected: string[], primary: string | null) => void;
  /**
   * Qué decir cuando el control cambia la principal por su cuenta.
   *
   * No lo dice el propio control: el aviso vive en el formulario, en la
   * misma región `role="status"` que usa el momento de las presentaciones,
   * porque dos regiones vivas compitiendo se pisan.
   */
  onAnnounce: (mensaje: string) => void;
}

/**
 * Asignación de categorías a un producto: el control N:M y su principal.
 *
 * **Un producto está en varias categorías y solo una da la miga de pan**
 * (regla 6). Son dos preguntas distintas y por eso son dos controles: casillas
 * para el conjunto, y **la principal se elige solo entre las ya marcadas** —
 * ofrecer como principal una categoría que el producto no tiene es enseñar una
 * puerta pintada, y el servidor la rechazaría.
 *
 * **Con una sola elegida no hay nada que elegir**, así que en vez de un botón
 * de radio solitario se dice con una frase cuál es y por qué.
 *
 * Enseña las activas **más las que el producto ya tenga aunque estén dadas de
 * baja**: desactivar una categoría no desactiva sus productos (regla 9), así
 * que esconderla aquí haría que guardar el formulario la quitara sin decirlo.
 */
export function CategoryAssigner({
  categories,
  selected,
  primary,
  onChange,
  onAnnounce,
}: CategoryAssignerProps) {
  const grupo = useId();
  const elegidas = new Set(selected);

  // Las de baja solo si el producto ya las tiene: ver el comentario de arriba.
  const visibles = categories.filter((c) => c.isActive || elegidas.has(c.id));
  const filas = asTree(visibles);

  const nombre = (id: string) => categories.find((c) => c.id === id)?.name ?? '';

  // Las elegidas **en el orden del árbol**, no en el que se pulsaron: la
  // lista de principales debajo tiene que leerse igual que las casillas de
  // arriba, o hay que buscar dos veces la misma categoría.
  const elegidasEnOrden = filas.map((f) => f.category.id).filter((id) => elegidas.has(id));

  function alternar(id: string, marcar: boolean) {
    if (marcar) {
      const nuevas = [...selected, id];
      // La primera que se marca es la principal: no tener ninguna con
      // categorías puestas deja al producto sin miga de pan.
      onChange(nuevas, primary ?? id);
      return;
    }

    const nuevas = selected.filter((otra) => otra !== id);

    if (primary !== id) {
      onChange(nuevas, primary);
      return;
    }

    // Se quitó la principal. **Se promueve otra y se dice**: dejarlo en
    // silencio cambia la dirección de la miga de pan sin que nadie lo sepa.
    const sucesora = elegidasEnOrden.find((otra) => nuevas.includes(otra)) ?? null;

    onChange(nuevas, sucesora);

    if (sucesora !== null) {
      onAnnounce(`Quitaste la categoría principal. Ahora la principal es «${nombre(sucesora)}».`);
    }
  }

  return (
    <fieldset className="cat-assign">
      <legend className="ui-field__label">Categorías</legend>
      <p className="cat-assign__hint">
        Un producto puede estar en varias. La principal es la que da su dirección en la web y la
        ruta que se lee encima del nombre.
      </p>

      {filas.length === 0 ? (
        <p className="cat-assign__empty">
          Todavía no hay categorías. Se crean en Catálogo → Categorías, y luego se eligen aquí.
        </p>
      ) : (
        <ul className="cat-assign__list">
          {filas.map(({ category, depth }) => (
            <li key={category.id} style={{ paddingLeft: `calc(${depth} * var(--s5))` }}>
              <label className="cat-assign__item">
                <input
                  type="checkbox"
                  checked={elegidas.has(category.id)}
                  onChange={(event) => alternar(category.id, event.target.checked)}
                />
                <span>{category.name}</span>
                {/* El estado con texto, no con color: una categoría de baja
                    sigue asignada y hay que poder verlo. */}
                {!category.isActive && <span className="cat-assign__off">dada de baja</span>}
              </label>
            </li>
          ))}
        </ul>
      )}

      {selected.length === 1 && (
        <p className="cat-assign__primary-note">
          «{nombre(elegidasEnOrden[0] ?? selected[0])}» es la principal, porque es la única.
        </p>
      )}

      {selected.length > 1 && (
        <fieldset className="cat-assign__primary">
          <legend className="ui-field__label">Categoría principal</legend>
          {elegidasEnOrden.map((id) => (
            <label key={id} className="cat-assign__item">
              <input
                type="radio"
                name={`principal-${grupo}`}
                checked={primary === id}
                onChange={() => onChange([...selected], id)}
              />
              <span>{nombre(id)}</span>
            </label>
          ))}
        </fieldset>
      )}
    </fieldset>
  );
}
