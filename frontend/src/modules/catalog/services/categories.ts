import { http } from '../../../shared/http/client';

/** Una categoría, tal como la devuelve `GET /api/admin/catalog/categories`. */
export interface Category {
  id: string;
  /** Padre en el árbol, o `null` si cuelga de la raíz. */
  parentId: string | null;
  name: string;
  /** Para la URL pública. **No se recalcula al cambiar el nombre** (regla 3). */
  slug: string;
  description: string | null;
  imageId: string | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
  /**
   * Cuántos productos activos la tienen.
   *
   * Viaja en el listado porque la regla 9 pide avisar **antes** de desactivar,
   * y el recuento que devuelve la baja llega cuando ya se decidió.
   */
  productCount: number;
}

export interface CreateCategory {
  name: string;
  slug: string | null;
  parentId: string | null;
  description: string | null;
  imageId: string | null;
  sortOrder: number | null;
}

export interface UpdateCategory {
  name: string;
  slug: string;
  parentId: string | null;
  description: string | null;
  imageId: string | null;
  sortOrder: number;
  isActive: boolean;
}

/** Lo que devuelve la baja: la categoría y a cuántos productos deja sin ella. */
export interface CategoryDeactivated {
  category: Category;
  productsLosingThisCategory: number;
}

const BASE = '/admin/catalog/categories';

export const categoriesService = {
  list: () => http.get<Category[]>(BASE),

  create: (category: CreateCategory) => http.post<Category>(BASE, category),

  update: (id: string, category: UpdateCategory) =>
    http.put<Category>(`${BASE}/${encodeURIComponent(id)}`, category),

  /** Baja lógica, sin cascada: sus productos siguen activos y pierden la categoría. */
  deactivate: (id: string) =>
    http.delete<CategoryDeactivated>(`${BASE}/${encodeURIComponent(id)}`),
};

/**
 * Ordena el listado plano como un árbol y calcula la profundidad de cada
 * categoría, para poder sangrarlas sin montar un componente de árbol entero.
 *
 * El SPEC no obliga a resolver la navegación completa del árbol; sí a **no
 * mentir sobre él**. Una lista plana que no enseña de quién cuelga cada
 * categoría estaría mintiendo.
 *
 * Una categoría cuyo padre no está en la lista se trata como raíz: es mejor
 * enseñarla suelta que perderla.
 */
export function asTree(categories: readonly Category[]): { category: Category; depth: number }[] {
  const byParent = new Map<string | null, Category[]>();
  const ids = new Set(categories.map((category) => category.id));

  for (const category of categories) {
    const parent = category.parentId && ids.has(category.parentId) ? category.parentId : null;
    const siblings = byParent.get(parent) ?? [];
    siblings.push(category);
    byParent.set(parent, siblings);
  }

  const ordered: { category: Category; depth: number }[] = [];

  function walk(parentId: string | null, depth: number) {
    for (const category of byParent.get(parentId) ?? []) {
      ordered.push({ category, depth });
      walk(category.id, depth + 1);
    }
  }

  walk(null, 0);
  return ordered;
}

/**
 * Las categorías que pueden ser padre de `categoryId`.
 *
 * Excluye la propia y toda su descendencia: elegirlas formaría un ciclo. El
 * servidor lo rechaza igualmente —es él quien manda—, pero ofrecer en un
 * desplegable una opción que siempre falla es enseñar una puerta pintada.
 */
export function possibleParents(
  categories: readonly Category[],
  categoryId: string | null,
): Category[] {
  if (categoryId === null) {
    return [...categories];
  }

  const forbidden = new Set<string>([categoryId]);
  let grew = true;

  while (grew) {
    grew = false;
    for (const category of categories) {
      if (category.parentId && forbidden.has(category.parentId) && !forbidden.has(category.id)) {
        forbidden.add(category.id);
        grew = true;
      }
    }
  }

  return categories.filter((category) => !forbidden.has(category.id));
}
