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
  await page.goto('/admin');
  await expect(page.locator('main')).toBeVisible();
  await expect(page.getByRole('navigation')).not.toContainText('Productos');

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
  await duringExpectedOutage(page, async () => {
    await page.goto('/');
    await expect(page.locator('main')).toBeVisible();
  });

  await expect(page.getByRole('link', { name: 'Ver el catálogo' })).toHaveCount(0);
  await expect(page.locator('main')).not.toContainText('catálogo');

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
