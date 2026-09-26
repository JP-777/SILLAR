import './no-photo.css';

/**
 * El cuadrado de un producto sin foto.
 *
 * **No se deja hueco.** Un cuadrado vacío entre cuadrados llenos se lee como
 * «catálogo a medio hacer»; uno ocupado por el nombre en grande, con su
 * categoría encima, se lee como variedad. Misma altura que una foto, así que
 * la rejilla no cambia de forma.
 *
 * Vive en `shared/` porque lo pinta más de un módulo: un módulo no importa de
 * otro (ADR-005). Las clases conservan el prefijo `ti-` con el que nacieron en
 * el catálogo porque las pruebas e2e las localizan por él.
 */
export function NoPhoto({
  name,
  context,
  ratio = 'square',
}: {
  name: string;
  /** La categoría, encima del nombre. Da sitio y contexto a la vez. */
  context?: string | null;
  ratio?: 'square' | 'wide';
}) {
  return (
    <div className={`ti-nophoto ti-nophoto--${ratio}`} aria-hidden="true">
      {context && <span className="ti-nophoto__context">{context}</span>}
      <span className="ti-nophoto__name">{name}</span>
    </div>
  );
}
