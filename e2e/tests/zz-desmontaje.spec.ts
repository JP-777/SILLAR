import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';

/**
 * **El criterio de terminado, con datos dentro.**
 *
 * > Un módulo está terminado cuando se puede instalar y desinstalar sin romper
 * > nada del resto del sistema.
 *
 * Es la promesa que se vende: SILLAR se ofrece como módulos desmontables y
 * licenciables por separado. Si no se sostiene, lo que falla no es una prueba.
 *
 * Lo que ya estaba probado: el schema se crea y se elimina sin llevarse nada
 * de `core` (`zz-instalacion.spec.ts:94`), y desactivar deja el panel en pie
 * sin entrada de menú ni ruta muerta (`catalogo.spec.ts:232`).
 *
 * Lo que faltaba y prueba este archivo: **desactivar no es borrar**. Un
 * cliente que deja de pagar M01 durante un mes y vuelve tiene que encontrar su
 * catálogo donde lo dejó — y esa afirmación solo se puede hacer con datos
 * dentro, que es la condición en la que un cliente lo tendría.
 *
 * Va con prefijo `zz` porque reinicia el proceso dos veces: corre al final,
 * cuando nadie más va a mirar.
 */

/** Lo que hay en el catálogo, para comparar antes y después. */
async function inventario(api: import('@playwright/test').APIRequestContext) {
  const productos = (await (
    await api.get('/api/admin/catalog/products?pageSize=200')
  ).json()) as { totalItems: number; items: { slug: string }[] };

  const marcas = (await (await api.get('/api/admin/catalog/brands')).json()) as unknown[];
  const categorias = (await (await api.get('/api/admin/catalog/categories')).json()) as unknown[];

  return {
    productos: productos.totalItems,
    marcas: marcas.length,
    categorias: categorias.length,
    // Los slugs, ordenados: un recuento igual con contenido distinto sería
    // igual de malo y no se vería.
    slugs: productos.items.map((p) => p.slug).sort(),
  };
}

/** Deja una marca, una categoría y tres productos. Idempotente. */
async function sembrar(api: import('@playwright/test').APIRequestContext) {
  const { csrfToken } = (await (await api.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };
  const cabeceras = { 'X-CSRF-Token': csrfToken };

  await api.post('/api/admin/catalog/brands', {
    headers: cabeceras,
    data: { name: 'Marca desmontaje', slug: 'marca-desmontaje', description: null, logoId: null },
  });

  await api.post('/api/admin/catalog/categories', {
    headers: cabeceras,
    data: {
      name: 'Categoria desmontaje',
      slug: 'categoria-desmontaje',
      parentId: null,
      description: null,
      imageId: null,
      sortOrder: 0,
    },
  });

  for (const n of [1, 2, 3]) {
    await api.post('/api/admin/catalog/products', {
      headers: cabeceras,
      data: {
        name: `Cuaderno desmontaje ${n}`,
        slug: `cuaderno-desmontaje-${n}`,
        shortDescription: null,
        description: null,
        primaryCategoryId: null,
        categoryIds: [],
        brandId: null,
        listPrice: n,
        saleUnit: null,
        variantLabel: null,
        code: null,
        barcode: null,
      },
    });
  }
}

/** Mueve el interruptor del módulo y espera a que el proceso vuelva. */
async function cambiarModulo(page: import('@playwright/test').Page, accion: 'Activar' | 'Desactivar') {
  await page.goto('/admin/modulos');

  await duringExpectedOutage(page, async () => {
    await page.locator('#modulo-catalog').getByRole('switch').click();
    await page.getByRole('alertdialog').getByRole('button', { name: new RegExp(`^${accion}`) }).click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });
}

test('Desactivar M01 no borra nada, y al volver el catálogo está donde lo dejaron', async ({
  page,
}) => {
  test.setTimeout(240_000);
  await loginAsE2eAdmin(page);

  // **Siembra lo suyo.** Corriendo con la suite entera encontraría de sobra,
  // pero una prueba que solo funciona acompañada no dice qué falla cuando
  // falla — y ésta afirma justamente que los datos sobreviven, así que
  // necesita datos que sean suyos y se puedan nombrar.
  await sembrar(page.request);

  const antes = await inventario(page.request);
  expect(antes.productos, 'esta prueba necesita un catálogo con datos').toBeGreaterThan(0);

  // --- Se desactiva -------------------------------------------------------
  await cambiarModulo(page, 'Desactivar');
  await expect(page.locator('#modulo-catalog')).toContainText('Inactivo');

  // 1 · El panel sigue en pie y no queda rastro del módulo en el menú.
  //
  // **Por sus enlaces, no por una palabra.** Esto decía
  // `not.toContainText('Productos')`, y M02 añadió «Productos destacados»:
  // la aserción empezó a fallar con el módulo correctamente desmontado. La
  // trampa es que cambiarla por otra subcadena solo muda la fragilidad al
  // siguiente módulo que use la palabra — y M18 Campaña Escolar la va a usar.
  //
  // Se pregunta por lo que identifica a M01 sin ambigüedad: **sus tres
  // destinos**. Un enlace a `/admin/catalogo/…` solo lo pone M01
  // (`catalog/routes.tsx`), así que la afirmación sigue siendo cierta con
  // cualquier número de módulos que hablen de productos.
  await page.goto('/admin');
  await expect(page.locator('main')).toBeVisible();

  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu, 'el panel se quedó sin menú al desactivar M01').toBeVisible();
  await expect(
    menu.locator('a[href^="/admin/catalogo/"]'),
    'quedó un enlace de M01 en el menú con el módulo desactivado',
  ).toHaveCount(0);

  // 2 · Sus rutas no existen: quien escriba una a mano no encuentra una
  //     pantalla rota.
  await page.goto('/admin/catalogo/productos');
  await expect(page).not.toHaveURL(/\/admin\/catalogo\/productos$/);

  // 3 · Y el API del catálogo tampoco: 404, no 500.
  const publico = await page.request.get('/api/catalog/products');
  expect(publico.status(), 'con M01 inactivo el catálogo público no responde 404').toBe(404);

  // 4 · **La portada no renderiza la sección de productos.** No vacía, no
  //     deshabilitada, no con un aviso de que falta algo: un hueco que
  //     explica su ausencia sigue siendo un hueco.
  // **Se espera al contenido de verdad, no a que `main` exista.** Las dos
  // aserciones de abajo esperan ausencia, y una ausencia se cumple sola en una
  // página a medio pintar: `main` es visible en cuanto aparece el armazón.
  await duringExpectedOutage(page, async () => {
    await page.goto('/');
    // CRM permanece activo: sirve como ancla positiva para demostrar que
    // la portada terminó de renderizar aunque M01 esté apagado.
    await expect(
      page.getByText('Cuenta de cliente', { exact: true }),
    ).toBeVisible();

    // **Y por eso mismo el aviso de portada vacía no sale.** Antes de M04
    // aquí se afirmaba lo contrario, y era correcto: sin nadie que aportara,
    // la portada lo decía. Ahora M04 aporta, así que decir «todavía no hay
    // contenido publicado» debajo de una sección con contenido sería mentira.
    // Las dos mitades se afirman juntas a propósito: sin esta, un fallo del
    // registro de contribuciones dejaría las dos cosas en pantalla y ninguna
    // prueba lo vería.
    await expect(
      page.getByText('Todavía no hay contenido publicado.'),
      'la portada avisa de que está vacía mientras pinta la sección de M04',
    ).toHaveCount(0);
  });

  // Y lo mismo en la tienda, por el mismo motivo: esto era
  // `not.toContainText('catálogo')` sobre todo `main`, que es una palabra que
  // cualquier módulo público puede usar con toda la razón. Lo que identifica a
  // M01 aquí son **sus rutas públicas** —`/catalogo` y `/producto/…`
  // (`catalog/routes.tsx`, `catalogPublicRoutes`)— y el título de su sección.
  await expect(
    page.locator('main a[href^="/catalogo"], main a[href^="/producto/"]'),
    'la portada sigue enlazando a la tienda con M01 desactivado',
  ).toHaveCount(0);
  await expect(
    page.getByRole('link', { name: 'Ver el catálogo', exact: true }),
    'sigue el enlace de la sección de M01',
  ).toHaveCount(0);
  await expect(
    page.getByText('Nuestra tienda', { exact: true }),
    'la sección de M01 sigue pintándose con el módulo desactivado',
  ).toHaveCount(0);

  // --- Se vuelve a activar ------------------------------------------------
  await cambiarModulo(page, 'Activar');
  await expect(page.locator('#modulo-catalog')).toContainText('Activo');

  // 5 · **Todo sigue ahí.** Es la afirmación de esta prueba: desactivar un
  //     módulo apaga sus pantallas y sus rutas, no toca sus datos.
  const despues = await inventario(page.request);

  expect(despues.productos, 'al reactivar M01 faltan productos').toBe(antes.productos);
  expect(despues.marcas, 'al reactivar M01 faltan marcas').toBe(antes.marcas);
  expect(despues.categorias, 'al reactivar M01 faltan categorías').toBe(antes.categorias);
  expect(despues.slugs, 'al reactivar M01 el catálogo tiene otros productos').toEqual(antes.slugs);

  // 6 · Y la tienda vuelve a servir.
  await expect
    .poll(async () => (await page.request.get('/api/catalog/products')).status(), {
      message: 'al reactivar M01 la tienda pública no vuelve',
    })
    .toBe(200);
});
