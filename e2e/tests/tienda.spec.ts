import type { APIRequestContext } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * M01 · La tienda pública — criterios de `ENTREGA-04E-TIENDA.md` §4.
 *
 * Los datos se crean **por API**: la pantalla de variantes no existe todavía
 * y no hace falta que exista para probar esto.
 */

/** Prepara el catálogo de esta prueba y devuelve lo que hace falta citar. */
async function sembrar(api: APIRequestContext) {
  const csrf = (await (await api.get('/api/admin/auth/csrf')).json()) as { csrfToken: string };
  const cabeceras = { 'X-CSRF-Token': csrf.csrfToken };

  // **Idempotente a propósito.** Cada prueba de este archivo siembra lo suyo,
  // así que la segunda en correr encuentra lo que dejó la primera. Tratar el
  // 409 como «ya estaba» es más honesto que borrar entre pruebas: el catálogo
  // real tampoco se vacía.
  async function categoria(name: string, slug: string) {
    const r = await api.post('/api/admin/catalog/categories', {
      headers: cabeceras,
      data: { name, slug, parentId: null, description: null, imageId: null, sortOrder: 0 },
    });

    if (r.status() === 409) {
      const todas = (await (await api.get('/api/admin/catalog/categories')).json()) as {
        id: string;
        name: string;
        slug: string;
      }[];
      const encontrada = todas.find((c) => c.slug === slug);
      expect(encontrada, `«${name}» dio 409 y no aparece en el listado`).toBeTruthy();
      return encontrada!;
    }

    expect(r.ok(), `categoría «${name}»: ${r.status()} ${await r.text()}`).toBe(true);
    return (await r.json()) as { id: string; name: string; slug: string };
  }

  async function producto(data: Record<string, unknown>) {
    const r = await api.post('/api/admin/catalog/products', { headers: cabeceras, data });

    if (r.status() === 409) {
      const slug = String(data.slug);
      const pagina = (await (
        await api.get(`/api/admin/catalog/products?q=${encodeURIComponent(String(data.name))}`)
      ).json()) as { items: { id: string; slug: string }[] };
      const encontrado = pagina.items.find((p) => p.slug === slug);
      expect(encontrado, `«${slug}» dio 409 y no aparece en el listado`).toBeTruthy();
      return { ...encontrado!, items: [] as { id: string }[] };
    }

    expect(r.ok(), `producto: ${r.status()} ${await r.text()}`).toBe(true);
    return (await r.json()) as { id: string; slug: string; items: { id: string }[] };
  }

  const deporte = await categoria('Deporte tienda', 'deporte-tienda');
  const juguetes = await categoria('Juguetes tienda', 'juguetes-tienda');

  // El cono: en dos categorías, principal «Deporte».
  const cono = await producto({
    name: 'Cono de entrenamiento',
    slug: 'cono-de-entrenamiento',
    shortDescription: 'Para marcar el campo.',
    description: null,
    primaryCategoryId: deporte.id,
    categoryIds: [deporte.id, juguetes.id],
    brandId: null,
    listPrice: 12,
    saleUnit: null,
    variantLabel: null,
    code: null,
    barcode: null,
  });

  // Búsqueda con tildes y mayúsculas: se busca «lapiz», existe «LÁPIZ».
  await producto({
    name: 'LÁPIZ de carbón',
    slug: 'lapiz-de-carbon',
    shortDescription: null,
    description: null,
    primaryCategoryId: null,
    categoryIds: [],
    brandId: null,
    listPrice: 2,
    saleUnit: null,
    variantLabel: null,
    code: null,
    barcode: null,
  });

  // Los tres estados del precio, y uno sin foto (todos lo están).
  await producto({
    name: 'Bolsa de obsequio',
    slug: 'bolsa-de-obsequio',
    shortDescription: null,
    description: null,
    primaryCategoryId: null,
    categoryIds: [],
    brandId: null,
    listPrice: 0,
    saleUnit: null,
    variantLabel: null,
    code: null,
    barcode: null,
  });

  await producto({
    name: 'Torta personalizada',
    slug: 'torta-personalizada',
    shortDescription: null,
    description: null,
    primaryCategoryId: null,
    categoryIds: [],
    brandId: null,
    listPrice: null,
    saleUnit: null,
    variantLabel: null,
    code: null,
    barcode: null,
  });

  return { cabeceras, deporte, juguetes, cono };
}

test('El caso del cono: en las dos categorías, y la miga usa solo la principal', async ({ page }) => {
  const record = themeRecorder(page, 'tienda');
  await loginAsE2eAdmin(page);
  const { deporte, juguetes } = await sembrar(page.request);

  // Aparece en el listado de las dos.
  await page.goto(`/catalogo/${deporte.slug}`);
  // `.ti-card__name`, no el texto suelto: el nombre aparece dos veces por
  // diseño —en la tarjeta y dentro del relleno sin foto—, y eso es correcto.
  await expect(page.locator('.ti-card__name', { hasText: 'Cono de entrenamiento' })).toBeVisible();

  await page.goto(`/catalogo/${juguetes.slug}`);
  // `.ti-card__name`, no el texto suelto: el nombre aparece dos veces por
  // diseño —en la tarjeta y dentro del relleno sin foto—, y eso es correcto.
  await expect(page.locator('.ti-card__name', { hasText: 'Cono de entrenamiento' })).toBeVisible();

  await record('tienda-categoria-con-el-cono');

  // Y al entrar desde Juguetes, la miga dice **Deporte**: la principal.
  await page.locator('.ti-card__name', { hasText: 'Cono de entrenamiento' }).click();
  await expect(page).toHaveURL(/\/producto\/cono-de-entrenamiento$/);

  const miga = page.getByRole('navigation', { name: 'Dónde estás' });
  await expect(miga).toContainText('Deporte tienda');
  await expect(miga).not.toContainText('Juguetes tienda');

  await record('tienda-ficha-del-cono');
});

test('Buscar «lapiz» devuelve «LÁPIZ», y el término se queda en el campo', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await sembrar(page.request);

  await page.goto('/catalogo');

  const buscador = page.getByLabel('Buscar productos');
  await buscador.fill('lapiz');

  // La búsqueda pública ignora mayúsculas y tildes (`core.es_search`).
  await expect(page.locator('.ti-card__name', { hasText: 'LÁPIZ de carbón' })).toBeVisible();

  // Y lo escrito se queda: corregir una letra es más rápido que reescribir.
  await expect(buscador).toHaveValue('lapiz');
});

test('Cero y vacío se distinguen sin leer la letra pequeña', async ({ page }) => {
  const record = themeRecorder(page, 'tienda');
  await loginAsE2eAdmin(page);
  await sembrar(page.request);

  // Se busca cada uno en vez de fiarse de que esté en la primera página: el
  // catálogo lo llenan también las otras specs, y una prueba que depende de
  // cuántos productos haya se rompe el día que alguien añada uno.
  async function tarjeta(termino: string, nombre: string) {
    await page.goto(`/catalogo?q=${encodeURIComponent(termino)}`);
    const encontrada = page.locator('.ti-card').filter({ hasText: nombre });
    await expect(encontrada).toBeVisible();
    return encontrada;
  }

  // Cero se muestra **como precio**: la palabra, no un 0 suelto ni un hueco.
  await expect(await tarjeta('obsequio', 'Bolsa de obsequio')).toContainText('Gratis');

  // Y vacío dice explícitamente que no es gratis, que es la confusión a evitar.
  const consultar = await tarjeta('Torta', 'Torta personalizada');
  await expect(consultar).toContainText('A consultar');
  await expect(consultar).toContainText('No es gratis');

  // Un número normal **no lleva nota**: si todo la llevara, ninguna se leería.
  const normal = await tarjeta('Cono de entrenamiento', 'Cono de entrenamiento');
  await expect(normal.locator('.ti-price__note')).toHaveCount(0);

  await record('tienda-los-tres-estados-del-precio');
});

test('Los productos sin foto llenan su cuadrado y la rejilla no parece rota', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await sembrar(page.request);

  await page.goto('/catalogo');
  // Esperar a que la rejilla esté antes de contar: contar sin esperar es
  // medir el hueco, no la pantalla.
  await expect(page.locator('.ti-card').first()).toBeVisible();

  // Ninguno tiene foto en esta siembra, así que todos deben traer el relleno.
  const huecos = page.locator('.ti-nophoto');
  expect(await huecos.count()).toBeGreaterThan(0);

  // Y ocupa lo mismo que ocuparía una foto: misma altura que su ancho.
  const caja = await huecos.first().boundingBox();
  expect(caja).not.toBeNull();
  expect(
    Math.abs((caja?.width ?? 0) - (caja?.height ?? 0)),
    'el cuadrado sin foto no es cuadrado, así que la rejilla se descuadra',
  ).toBeLessThanOrEqual(2);

  // Lleva el nombre dentro: es lo que lo hace parecer variedad y no vacío.
  await expect(huecos.first()).not.toBeEmpty();
});

test('Un producto despublicado responde 404, no 403', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const { cabeceras } = await sembrar(page.request);

  // Se despublica por API y se pide su ficha pública.
  //
  // **Se busca por el slug exacto, no por que el nombre contenga «Torta»**:
  // `presentaciones.spec.ts` crea «Torta hereda consultar», y con la suite
  // entera esta prueba despublicaba la que no era y luego preguntaba por una
  // que seguía publicada. El slug es la identidad; el nombre, una coincidencia.
  const lista = await (await page.request.get('/api/admin/catalog/products?q=Torta')).json();
  const torta = (lista as { items: { id: string; name: string; slug: string }[] }).items.find(
    (p) => p.slug === 'torta-personalizada',
  );

  expect(torta, 'el sembrado no dejó «torta-personalizada» en el listado').toBeTruthy();

  const guardado = await page.request.put(`/api/admin/catalog/products/${torta!.id}`, {
    headers: cabeceras,
    data: {
      name: 'Torta personalizada',
      slug: 'torta-personalizada',
      shortDescription: null,
      description: null,
      brandId: null,
      listPrice: null,
      saleUnit: null,
      variantLabel: null,
      isPublic: false,
      isActive: true,
    },
  });
  expect(guardado.ok()).toBe(true);

  // 404 y no 403: contestar «existe pero no puedes» sería contar que existe.
  const publico = await page.request.get('/api/catalog/products/torta-personalizada');
  expect(publico.status(), 'un producto despublicado no puede responder 403').toBe(404);

  // Y la pantalla lo cuenta como «no encontrado», sin jerga.
  await duringExpectedOutage(page, async () => {
    await page.goto('/producto/torta-personalizada');
    await expect(page.getByText('No encontramos ese producto')).toBeVisible();
  });

  await expect(page.locator('body')).not.toContainText('404');
});

test('Con la categoría principal desactivada, la miga cae a otra activa', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const { cabeceras, deporte, juguetes } = await sembrar(page.request);

  // Se da de baja «Deporte», que es la principal del cono.
  const baja = await page.request.delete(`/api/admin/catalog/categories/${deporte.id}`, {
    headers: cabeceras,
  });
  expect(baja.ok()).toBe(true);

  await page.goto('/producto/cono-de-entrenamiento');

  const miga = page.getByRole('navigation', { name: 'Dónde estás' });

  // Cae a la otra categoría activa del producto, sin promover nada en la base
  // (`ChooseTarget`). Y **nunca un enlace a algo invisible**: la desactivada
  // no aparece.
  await expect(miga).toContainText(juguetes.name);
  await expect(miga).not.toContainText(deporte.name);

  // Se deja como se encontró: si «Deporte» queda de baja, desaparece del
  // árbol público y la prueba siguiente no encuentra su filtro. Una prueba
  // que cambia el entorno para las demás es una prueba que las rompe.
  const revivir = await page.request.put(`/api/admin/catalog/categories/${deporte.id}`, {
    headers: cabeceras,
    data: {
      name: deporte.name,
      slug: deporte.slug,
      parentId: null,
      description: null,
      imageId: null,
      sortOrder: 0,
      isActive: true,
    },
  });
  expect(revivir.ok(), `no se pudo reactivar «${deporte.name}»`).toBe(true);
});

test('Cambiar de filtro vuelve a la página 1, y la paginación se usa a 390 px', async ({ page }) => {
  test.setTimeout(120_000);

  await loginAsE2eAdmin(page);
  const { cabeceras, deporte } = await sembrar(page.request);

  // Trece productos más para pasar de la página de doce.
  for (let i = 1; i <= 13; i += 1) {
    const r = await page.request.post('/api/admin/catalog/products', {
      headers: cabeceras,
      data: {
        name: `Relleno tienda ${i}`,
        slug: `relleno-tienda-${i}`,
        shortDescription: null,
        description: null,
        primaryCategoryId: null,
        categoryIds: [],
        brandId: null,
        listPrice: i,
        saleUnit: null,
        variantLabel: null,
        code: null,
        barcode: null,
      },
    });
    expect(r.ok()).toBe(true);
  }

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/catalogo');

  const siguiente = page.getByRole('button', { name: 'Siguiente' });
  await expect(siguiente).toBeVisible();

  // Usable con el pulgar: es la única forma de ver el resto del catálogo.
  const caja = await siguiente.boundingBox();
  expect(caja?.height ?? 0, 'la paginación no se puede pulsar con el dedo').toBeGreaterThanOrEqual(40);

  await siguiente.click();
  await expect(page).toHaveURL(/pagina=2/);

  // **Cambiar de filtro vuelve a la página 1.** Sin esto, filtrar desde la
  // página 2 deja mirando un hueco que sí tiene resultados.
  await page.getByRole('button', { name: deporte.name }).click();
  await expect(page).not.toHaveURL(/pagina=2/);
  // `.ti-card__name`, no el texto suelto: el nombre aparece dos veces por
  // diseño —en la tarjeta y dentro del relleno sin foto—, y eso es correcto.
  await expect(page.locator('.ti-card__name', { hasText: 'Cono de entrenamiento' })).toBeVisible();
});

test('Con M01 desactivado, las rutas públicas desaparecen y el inicio no deja hueco', async ({
  page,
}) => {
  test.setTimeout(180_000);

  await loginAsE2eAdmin(page);

  await page.goto('/');
  await expect(page.getByRole('link', { name: 'Ver el catálogo' })).toBeVisible();

  await page.goto('/admin/modulos');
  await duringExpectedOutage(page, async () => {
    await page.locator('#modulo-catalog').getByRole('switch').click();
    await page.getByRole('alertdialog').getByRole('button', { name: /^Desactivar/ }).click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });

  // Las tres rutas dejan de existir: quien las escriba cae en la redirección.
  for (const ruta of ['/catalogo', '/catalogo/deporte-tienda', '/producto/cono-de-entrenamiento']) {
    await page.goto(ruta);
    await expect(page, `«${ruta}» sigue viva con M01 desactivado`).not.toHaveURL(
      new RegExp(`${ruta.replace(/\//g, '\\/')}$`),
    );
  }

  // Y el inicio **no renderiza la sección en absoluto** — ni vacía, ni con un
  // aviso de que falta algo. Un hueco que explica su ausencia sigue siendo un
  // hueco.
  //
  // **Primero se espera a que la portada haya renderizado algo.** Las dos
  // aserciones de abajo esperan ausencia, y una ausencia se cumple sola en una
  // página que todavía no ha pintado: pasaban vacuamente si el `goto` volvía
  // antes que React. Se vio porque al romper el filtro a propósito la prueba
  // seguía verde con el archivo entero y roja en solitario — la diferencia era
  // cuánto tardaba la página, no lo que enseñaba.
  await page.goto('/');
  await expect(page.getByText('Todavía no hay contenido publicado.')).toBeVisible();

  await expect(page.getByRole('link', { name: 'Ver el catálogo' })).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText(/cat[áa]logo/i);

  // Se deja como se encontró.
  await page.goto('/admin/modulos');
  await duringExpectedOutage(page, async () => {
    await page.locator('#modulo-catalog').getByRole('switch').click();
    await page.getByRole('alertdialog').getByRole('button', { name: /^Activar/ }).click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });

  await expect(page.locator('#modulo-catalog')).toContainText('Activo');
});
