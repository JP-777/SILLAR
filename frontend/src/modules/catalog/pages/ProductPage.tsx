import { useCallback, useId, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { EmptyState, Spinner } from '../../../shared/ui';
import { NoPhoto, Price } from '../components/ProductCard';
import { formatPrice, publicCatalog, variantPriceNote } from '../services/publicCatalog';
import '../components/tienda.css';

/** De dónde venía quien llegó aquí, si venía de algún sitio de este sitio. */
interface Origen {
  slug: string;
  name: string;
}

/**
 * `/producto/:slug` — la ficha.
 *
 * Un producto despublicado o dado de baja responde **404** desde el servidor,
 * y aquí se ve como «no encontrado». No 403: contestar «existe pero no
 * puedes» sería contar que existe.
 */
export function ProductPage() {
  const { slug = '' } = useParams();
  const location = useLocation();

  // El origen viaja en el estado de navegación, así que **se pierde al
  // recargar** — y con él la miga larga. Es correcto: quien llega desde un
  // enlace compartido no viene de ninguna categoría, y así es como se
  // comparte un producto. Por eso la versión corta es la de por defecto.
  const origen = (location.state as { origen?: Origen } | null)?.origen ?? null;

  /**
   * Qué presentación se está mirando.
   *
   * **El precio grande la sigue**, que es lo que la lista de solo lectura no
   * hacía: tres opciones con importes distintos y un número arriba que no se
   * movía obligaba a comparar de memoria cuál de ellos era el que se cobra.
   *
   * Se guarda el índice y no el valor porque una presentación puede no tener
   * nombre: con una sola, `variantValue` es nulo a propósito.
   */
  const [elegida, setElegida] = useState(0);
  const grupo = useId();

  const load = useCallback(() => publicCatalog.product(slug), [slug]);
  const { state } = useResource(load, 'cargar el producto');
  const showLoading = useDelayedFlag(state.status === 'loading');

  // El nombre del producto es lo que se ve al compartir el enlace.
  useDocumentTitle(state.status === 'ready' ? state.data.name : 'Producto');

  if (showLoading) {
    return (
      <main className="ti-page" id="contenido" tabIndex={-1}>
        <Spinner label="Cargando el producto" />
      </main>
    );
  }

  if (state.status === 'error') {
    return (
      <main className="ti-page" id="contenido" tabIndex={-1}>
        <EmptyState
          title="No encontramos ese producto"
          description="Puede que ya no esté a la venta o que haya cambiado de dirección."
          action={<Link to="/catalogo">Ver todo el catálogo</Link>}
        />
      </main>
    );
  }

  if (state.status !== 'ready') {
    return <main className="ti-page" id="contenido" tabIndex={-1} />;
  }

  const product = state.data;
  const principal = product.images.find((image) => image.isPrimary) ?? product.images[0] ?? null;

  // La miga sale de la categoría **principal**, así que puede decir algo
  // distinto de por dónde se vino. Solo hay discrepancia si el origen no está
  // en la ruta que se muestra.
  const enLaMiga = origen && product.breadcrumb.some((item) => item.slug === origen.slug);
  const discrepa = origen !== null && !enLaMiga;

  const variantes = product.variants;
  const varias = variantes.length > 1;
  const notaPrecio = variantPriceNote(variantes);
  // Si la elegida ya no existe —se cambió de producto sin desmontar la
  // página—, manda la primera.
  const actual = variantes[elegida] ?? variantes[0] ?? null;
  const precioBase = actual?.price ?? null;

  return (
    <main className="ti-page" id="contenido" tabIndex={-1}>
      <nav className="ti-crumbs" aria-label="Dónde estás">
        <Link to="/catalogo">Catálogo</Link>
        {product.breadcrumb.map((item) => (
          <span key={item.slug}>
            <span className="ti-crumbs__sep"> / </span>
            <Link to={`/catalogo/${item.slug}`}>{item.name}</Link>
          </span>
        ))}
      </nav>

      {/* La versión larga, **solo cuando hay origen y no coincide**. Explicar
          la discrepancia sin que la haya sería sembrar una duda que nadie
          tenía. */}
      {discrepa && (
        <div className="ti-origin">
          <Link to={`/catalogo/${origen.slug}`}>‹ Volver a {origen.name}</Link>
          <span className="ti-origin__note">
            Este producto está en varias categorías. Arriba se muestra la principal.
          </span>
        </div>
      )}

      <div className="ti-detail">
        <div className="ti-detail__gallery">
          {principal ? (
            <img src={principal.url} alt={principal.altText ?? ''} className="ti-detail__image" />
          ) : (
            // 16:9 y no cuadrado: sin nada que encuadrar, un cuadrado grande
            // solo estira la página y aleja el precio de lo que se lee.
            <NoPhoto
              name={product.name}
              context={product.breadcrumb.at(-1)?.name ?? null}
              ratio="wide"
            />
          )}

          {product.images.length > 1 && (
            <ul className="ti-detail__thumbs">
              {product.images.map((image) => (
                <li key={image.url}>
                  <img src={image.url} alt={image.altText ?? ''} loading="lazy" />
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="ti-detail__body">
          <h1 className="ti-detail__name">{product.name}</h1>

          {product.brandName && (
            <p className="ti-detail__brand">
              {product.brandSlug ? (
                <Link to={`/catalogo?marca=${product.brandSlug}`}>{product.brandName}</Link>
              ) : (
                product.brandName
              )}
            </p>
          )}

          <Price value={precioBase} size="detail" />

          {product.saleUnit && <p className="ti-detail__brand">{product.saleUnit}</p>}

          {product.shortDescription && (
            <p className="ti-detail__desc">{product.shortDescription}</p>
          )}

          {product.description && <p className="ti-detail__desc">{product.description}</p>}

          {/* Con una sola variante, **la palabra no aparece**: sus datos ya
              están arriba, como datos del producto. */}
          {varias && (
            <section className="ti-options">
              {/* Se titula con lo que varía —«Color de la tinta»— y nunca con
                  la palabra «variante», que no significa nada para quien
                  compra. */}
              <h2 className="ti-options__title">{product.variantLabel ?? 'Opciones'}</h2>

              {/* La frase sale del dato: escrita a mano mentiría el día que
                  alguien le ponga a una opción un precio distinto. */}
              {notaPrecio && <p className="ti-options__note">{notaPrecio}</p>}

              {/* **Se elige, no se lee.** Botones de radio de verdad y no
                  `div`s con `onClick`: dan la navegación por flechas, el
                  estado leído en voz alta y el foco visible sin escribir
                  ninguna de las tres cosas. */}
              <ul className="ti-options__list">
                {variantes.map((variante, indice) => (
                  <li key={variante.variantValue ?? variante.code ?? indice}>
                    <label
                      className="ti-options__item"
                      data-elegida={indice === elegida ? 'si' : undefined}
                    >
                      <input
                        type="radio"
                        name={`presentacion-${grupo}`}
                        className="ti-options__radio"
                        checked={indice === elegida}
                        onChange={() => setElegida(indice)}
                      />
                      <span className="ti-options__value">{variante.variantValue}</span>
                      {variante.price !== null && (
                        <span className="ti-options__price">{formatPrice(variante.price)}</span>
                      )}
                    </label>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      </div>
    </main>
  );
}
