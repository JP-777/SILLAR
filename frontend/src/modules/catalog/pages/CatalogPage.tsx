import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, EmptyState, FilterChip, Input, NoResults, Spinner } from '../../../shared/ui';
import { Pagination } from '../../../shared/ui/patterns';
import { ProductCard } from '../components/ProductCard';
import { publicCatalog, type PublicBrand, type PublicCategory } from '../services/publicCatalog';
import '../components/tienda.css';

/** Aplana el árbol: los filtros son una lista, no una jerarquía. */
function flatten(nodes: readonly PublicCategory[]): PublicCategory[] {
  return nodes.flatMap((node) => [node, ...flatten(node.children)]);
}

/**
 * `/catalogo` — todo lo publicado, con filtros y búsqueda.
 *
 * **Móvil primero.** Los filtros son `FilterChip` envueltos y no una barra
 * lateral: en una pantalla estrecha una barra lateral o se esconde tras un
 * botón, y entonces nadie filtra, o se come el sitio de los productos.
 */
export function CatalogPage() {
  useDocumentTitle('Catálogo');

  const [params, setParams] = useSearchParams();

  const category = params.get('categoria');
  const brand = params.get('marca');
  const q = params.get('q') ?? '';
  const page = Number(params.get('pagina') ?? '1');

  // El término se queda en el campo: corregir una letra es más rápido que
  // volver a escribirlo entero, y quien busca mal casi siempre está cerca.
  const [term, setTerm] = useState(q);
  useEffect(() => setTerm(q), [q]);

  const [categories, setCategories] = useState<PublicCategory[]>([]);
  const [brands, setBrands] = useState<PublicBrand[]>([]);

  useEffect(() => {
    publicCatalog.categories().then((tree) => setCategories(flatten(tree))).catch(() => setCategories([]));
    publicCatalog.brands().then(setBrands).catch(() => setBrands([]));
  }, []);

  const load = useCallback(
    () =>
      publicCatalog.products({
        category: category ?? undefined,
        brand: brand ?? undefined,
        q: q || undefined,
        page,
        pageSize: 12,
      }),
    [category, brand, q, page],
  );

  const { state } = useResource(load, 'cargar el catálogo');
  const showLoading = useDelayedFlag(state.status === 'loading');
  const result = state.status === 'ready' ? state.data : null;

  /**
   * Cambia un filtro y **vuelve a la página 1**.
   *
   * Sin esto, quien está en la página 3 y filtra por una marca con dos
   * productos se queda mirando una página vacía que sí tiene resultados.
   */
  function setFilter(key: string, value: string | null) {
    const next = new URLSearchParams(params);

    if (value === null) {
      next.delete(key);
    } else {
      next.set(key, value);
    }

    next.delete('pagina');
    setParams(next);
  }

  const filtrando = Boolean(category || brand || q);

  return (
    <main className="ti-page" id="contenido" tabIndex={-1}>
      <div>
        <h1 className="ti-title">Catálogo</h1>
        <p className="ti-lead">Todo lo que tenemos publicado.</p>
      </div>

      <Input
        // `lg` porque en móvil es el control principal de la pantalla.
        size="lg"
        type="search"
        value={term}
        aria-label="Buscar productos"
        placeholder="Buscar por nombre"
        onChange={(event) => {
          setTerm(event.target.value);
          setFilter('q', event.target.value.trim() === '' ? null : event.target.value);
        }}
      />

      <div className="ti-filters">
        {categories.length > 0 && (
          <div className="ti-filters__group">
            <span className="ti-filters__label">Categorías</span>
            {categories.map((option) => (
              <FilterChip
                key={option.slug}
                selected={category === option.slug}
                onToggle={(on) => setFilter('categoria', on ? option.slug : null)}
              >
                {option.name}
              </FilterChip>
            ))}
          </div>
        )}

        {brands.length > 0 && (
          <div className="ti-filters__group">
            <span className="ti-filters__label">Marcas</span>
            {brands.map((option) => (
              <FilterChip
                key={option.slug}
                selected={brand === option.slug}
                onToggle={(on) => setFilter('marca', on ? option.slug : null)}
              >
                {option.name}
              </FilterChip>
            ))}
          </div>
        )}
      </div>

      {state.status === 'error' && <Alert tone="danger">{state.failure.message}</Alert>}

      {showLoading && <Spinner label="Cargando el catálogo" />}

      {result && result.items.length > 0 && (
        <ul className="ti-grid">
          {result.items.map((product) => (
            <ProductCard key={product.slug} product={product} />
          ))}
        </ul>
      )}

      {result && result.items.length === 0 && filtrando && (
        // Buscar y no encontrar. **Sin acción principal**: el arreglo ya está
        // en pantalla —el campo y los filtros— y una acción competiría con él.
        <NoResults
          title={q ? `No hay resultados para «${q}»` : 'No hay resultados con esos filtros'}
          description="Prueba con menos palabras, o quita algún filtro."
          onClear={() => setParams(new URLSearchParams())}
        />
      )}

      {result && result.items.length === 0 && !filtrando && (
        <EmptyState
          title="Todavía no hay productos publicados"
          description="Cuando se publique el primero aparecerá aquí."
          // Nota: cierta, secundaria y **no accionable**. No promete fecha.
          note="El catálogo se llena desde el panel de administración."
        />
      )}

      {result && result.totalPages > 1 && (
        <Pagination
          page={result.page}
          totalPages={result.totalPages}
          totalItems={result.totalItems}
          // `lg` en la tienda: llegar a la página 2 con el pulgar es la única
          // forma de ver el resto del catálogo.
          size="lg"
          onChange={(next) => {
            const params2 = new URLSearchParams(params);
            params2.set('pagina', String(next));
            setParams(params2);
          }}
        />
      )}
    </main>
  );
}
