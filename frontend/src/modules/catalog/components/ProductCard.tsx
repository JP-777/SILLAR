import { Link } from 'react-router-dom';
import { formatPrice, priceKind, type PublicCard } from '../services/publicCatalog';
import './tienda.css';

/**
 * El precio, con sus tres estados.
 *
 * **Solo los dos raros se explican.** Un número normal no lleva nota: si todo
 * la llevara, la nota dejaría de significar algo y nadie leería ninguna.
 *
 * Cero se muestra **como precio** —grande y en negrita, con una afirmación
 * debajo— porque gratis es un precio y esconderlo en letra pequeña lo haría
 * parecer un error. «A consultar» se muestra apagado y **diciendo que no es
 * gratis**, que es exactamente la confusión que hay que evitar.
 */
export function Price({
  value,
  size = 'card',
  from = false,
}: {
  value: number | null;
  size?: 'card' | 'detail';
  /**
   * Si el importe es **la más barata de varias**, no el precio.
   *
   * La tarjeta del listado no tiene selector de variante, así que en cuanto
   * las presentaciones cuestan distinto el número que enseña solo se cobra por
   * una de ellas. «Desde S/ 5,50» es la única cota que se puede prometer.
   *
   * No se aplica a «a consultar»: ahí no hay cota que prometer, y el servidor
   * ya lo resuelve mandando el precio nulo.
   */
  from?: boolean;
}) {
  const kind = priceKind(value);
  const clase = `ti-price ti-price--${size} ti-price--${kind}`;

  if (kind === 'consultar') {
    return (
      <p className={clase}>
        <span className="ti-price__amount">A consultar</span>
        <span className="ti-price__note">No es gratis: escríbenos y te decimos el precio.</span>
      </p>
    );
  }

  if (kind === 'gratis') {
    return (
      <p className={clase}>
        <span className="ti-price__amount">Gratis</span>
        {/* La nota de siempre **afirma que no hay que pagar nada**, y eso es
            falso si la más barata es gratis pero otra cuesta. Cero sigue
            siendo el mínimo y se enseña como precio; lo que cambia es la
            afirmación, que pasa a decir la verdad entera. */}
        <span className="ti-price__note">
          {from
            ? 'Hay una presentación gratis; las demás cuestan. Ábrelo para ver cada una.'
            : 'Sin costo. No hay que pagar nada por él.'}
        </span>
      </p>
    );
  }

  return (
    <p className={clase}>
      {/* «Desde» va **dentro del importe**, no en la nota: es parte de lo que
          cuesta, y una nota se puede no leer. La nota sigue reservada a los
          dos casos raros. */}
      <span className="ti-price__amount">
        {from ? `Desde ${formatPrice(value!)}` : formatPrice(value!)}
      </span>
      {from && (
        <span className="ti-price__note">
          Las presentaciones cuestan distinto. Ábrelo para ver cada una.
        </span>
      )}
    </p>
  );
}

/**
 * El cuadrado de un producto sin foto.
 *
 * **No se deja hueco.** Un cuadrado vacío entre cuadrados llenos se lee como
 * «catálogo a medio hacer»; uno ocupado por el nombre en grande, con su
 * categoría encima, se lee como variedad. Misma altura que una foto, así que
 * la rejilla no cambia de forma.
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

/** Una tarjeta del listado. */
export function ProductCard({ product, context }: { product: PublicCard; context?: string | null }) {
  return (
    <li className="ti-card">
      <Link to={`/producto/${product.slug}`} className="ti-card__link">
        {product.primaryImageUrl ? (
          <img
            src={product.primaryImageUrl}
            alt=""
            className="ti-card__image"
            loading="lazy"
          />
        ) : (
          <NoPhoto name={product.name} context={context} />
        )}

        <div className="ti-card__body">
          <p className="ti-card__name">{product.name}</p>
          {product.shortDescription && (
            <p className="ti-card__desc">{product.shortDescription}</p>
          )}
          <Price value={product.price} from={product.priceVaries} />
        </div>
      </Link>
    </li>
  );
}
