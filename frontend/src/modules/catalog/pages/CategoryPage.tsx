import { useCallback, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, EmptyState, Spinner } from '../../../shared/ui';
import { Pagination } from '../../../shared/ui/patterns';
import { ProductCard } from '../components/ProductCard';
import { publicCatalog } from '../services/publicCatalog';
import '../components/tienda.css';

/**
 * `/catalogo/:categoria` — una categoría con sus productos.
 *
 * Una categoría inexistente **o desactivada** responde 404 desde el servidor,
 * y aquí se ve como «no encontrada»: las dos son lo mismo para quien navega,
 * y distinguirlas contaría lo que hay dado de baja.
 */
export function CategoryPage() {
  const { categoria = '' } = useParams();
  const [page, setPage] = useState(1);

  const load = useCallback(() => publicCatalog.category(categoria, page), [categoria, page]);
  const { state } = useResource(load, 'cargar la categoría');
  const showLoading = useDelayedFlag(state.status === 'loading');

  // El nombre solo se sabe al llegar los datos; antes, la ruta.
  useDocumentTitle(state.status === 'ready' ? state.data.name : 'Categoría');

  if (showLoading) {
    return (
      <main className="ti-page" id="contenido" tabIndex={-1}>
        <Spinner label="Cargando la categoría" />
      </main>
    );
  }

  if (state.status === 'error') {
    return (
      <main className="ti-page" id="contenido" tabIndex={-1}>
        <EmptyState
          title="No encontramos esa categoría"
          description="Puede que ya no exista o que haya cambiado de dirección."
          action={<Link to="/catalogo">Ver todo el catálogo</Link>}
        />
      </main>
    );
  }

  if (state.status !== 'ready') {
    return <main className="ti-page" id="contenido" tabIndex={-1} />;
  }

  const { name, breadcrumb, products } = state.data;

  return (
    <main className="ti-page" id="contenido" tabIndex={-1}>
      <nav className="ti-crumbs" aria-label="Dónde estás">
        <Link to="/catalogo">Catálogo</Link>
        {breadcrumb.map((item) => (
          <span key={item.slug} className="ti-crumbs__item">
            <span className="ti-crumbs__sep"> / </span>
            <Link to={`/catalogo/${item.slug}`}>{item.name}</Link>
          </span>
        ))}
      </nav>

      <div>
        <h1 className="ti-title">{name}</h1>
      </div>

      {products.items.length > 0 ? (
        <ul className="ti-grid">
          {products.items.map((product) => (
            <ProductCard key={product.slug} product={product} context={name} />
          ))}
        </ul>
      ) : (
        // Una categoría sin productos **todavía**. No es error de nadie, y no
        // promete fecha: prometerla es lo que envejece mal.
        <EmptyState
          title={`Todavía no hay nada en ${name}`}
          description="Esta categoría está creada pero aún no tiene productos publicados."
          action={<Link to="/catalogo">Ver todo el catálogo</Link>}
          note="Vuelve a mirar más adelante o busca por nombre."
        />
      )}

      {products.totalPages > 1 && (
        <Pagination
          page={products.page}
          totalPages={products.totalPages}
          totalItems={products.totalItems}
          size="lg"
          onChange={setPage}
        />
      )}

      {state.status === 'ready' && products.items.length === 0 && (
        <Alert tone="info">Puedes explorar el resto del catálogo mientras tanto.</Alert>
      )}
    </main>
  );
}
