import type { APIRequestContext } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * El precio de la tarjeta pública — `ENTREGA-04D-VARIANTES-CATEGORIAS.md` §4.
 *
 * **La tarjeta no tiene selector de variante, pero enseña un precio.** Hasta
 * 04D ninguna pantalla podía crear presentaciones con precios distintos, así
 * que la tarjeta nunca había podido mentir. Ahora sí, y esto lo impide.
 */

/** Deja un producto público con las presentaciones que se le pidan. */
async function sembrar(
  api: APIRequestContext,
  producto: { name: string; slug: string; listPrice: number | null },
  precios: (number | null)[],
) {
  const csrf = (await (await api.get('/api/admin/auth/csrf')).json()) as { csrfToken: string };
  const cabeceras = { 'X-CSRF-Token': csrf.csrfToken };

  const creado = await api.post('/api/admin/catalog/products', {
    headers: cabeceras,
    data: {
      ...producto,
      shortDescription: null,
      description: null,
      primaryCategoryId: null,
      categoryIds: [],
      brandId: null,
      saleUnit: null,
      variantLabel: 'Color',
      code: null,
      barcode: null,
    },
  });

  expect(creado.ok(), `crear: ${creado.status()} ${await creado.text()}`).toBe(true);
  const ficha = (await creado.json()) as { id: string; items: { id: string }[] };

  // La primera presentación ya existe —la única que crea el alta—, así que se
  // ajusta; las demás se añaden.
  for (const [indice, precio] of precios.entries()) {
    const datos = {
      variantValue: `Tono ${indice + 1}`,
      code: null,
      barcode: null,
      priceOverride: precio,
      imageId: null,
      sortOrder: indice,
      isActive: true,
    };

    const r =
      indice === 0
        ? await api.put(`/api/admin/catalog/items/${ficha.items[0].id}`, {
            headers: cabeceras,
            data: datos,
          })
        : await api.post(`/api/admin/catalog/products/${ficha.id}/items`, {
            headers: cabeceras,
            data: datos,
          });

    expect(r.ok(), `presentación ${indice + 1}: ${r.status()} ${await r.text()}`).toBe(true);
  }

  return ficha;
}

test('Con presentaciones de distinto precio, la tarjeta dice «Desde» y el más barato', async ({
  page,
}) => {
  const record = themeRecorder(page, 'tarjeta-precio');
  await loginAsE2eAdmin(page);

  const sello = Date.now();
  const slug = `plumon-precios-${sello}`;

  // 8,00 · 5,50 · hereda 4,90. El mínimo es el heredado: mirar solo los
  // precios propios daría 5,50, que es más caro de lo que se cobra.
  await sembrar(page.request, { name: `Plumón precios ${sello}`, slug, listPrice: 4.9 }, [
    8,
    5.5,
    null,
  ]);

  const respuesta = await page.request.get(`/api/catalog/products?q=${sello}`);
  expect(respuesta.status(), 'el listado público tras crear presentaciones').toBe(200);

  const listado = (await respuesta.json()) as {
    items: { slug: string; price: number | null; priceVaries: boolean }[];
  };
  const tarjeta = listado.items.find((item) => item.slug === slug);

  expect(tarjeta, 'el producto sembrado no aparece en el listado público').toBeTruthy();
  expect(tarjeta!.price, 'la tarjeta no enseña el mínimo efectivo').toBe(4.9);
  expect(tarjeta!.priceVaries, 'la tarjeta no avisa de que los precios varían').toBe(true);

  // Y la pantalla lo dice con palabras, que es lo que ve quien compra.
  await page.goto(`/catalogo?q=${sello}`);
  const artículo = page.locator('.ti-card').filter({ hasText: `Plumón precios ${sello}` });
  await expect(artículo).toBeVisible();
  await expect(artículo).toContainText(/Desde\s+S\/\s*4[.,]90/);

  await record('tarjeta-precio-desde');
});

test('Si una presentación es «a consultar», toda la tarjeta lo es', async ({ page }) => {
  await loginAsE2eAdmin(page);

  const sello = Date.now();
  const slug = `temperas-consultar-${sello}`;

  // Dos con precio y una sin ninguno: **«desde» promete una cota**, y una
  // presentación sin precio puede costar cualquier cosa.
  await sembrar(page.request, { name: `Témperas consultar ${sello}`, slug, listPrice: null }, [
    8,
    5.5,
    null,
  ]);

  const listado = (await (
    await page.request.get(`/api/catalog/products?q=${sello}`)
  ).json()) as { items: { slug: string; price: number | null; priceVaries: boolean }[] };

  const tarjeta = listado.items.find((item) => item.slug === slug);
  expect(tarjeta, 'el producto sembrado no aparece en el listado público').toBeTruthy();
  expect(tarjeta!.price, 'una presentación sin precio no dejó la tarjeta a consultar').toBeNull();
  expect(tarjeta!.priceVaries, '«a consultar» no admite «desde»: no hay cota').toBe(false);

  await page.goto(`/catalogo?q=${sello}`);
  const artículo = page.locator('.ti-card').filter({ hasText: `Témperas consultar ${sello}` });
  await expect(artículo).toBeVisible();
  await expect(artículo).toContainText('A consultar');
  await expect(artículo).not.toContainText('Desde');
});

test('Con todas al mismo precio, la tarjeta no dice «Desde»', async ({ page }) => {
  await loginAsE2eAdmin(page);

  const sello = Date.now();
  const slug = `sobres-iguales-${sello}`;

  // El caso mayoritario, y el que se rompería si «desde» se pusiera de más:
  // tres presentaciones que heredan el mismo precio del producto.
  await sembrar(page.request, { name: `Sobres iguales ${sello}`, slug, listPrice: 1.2 }, [
    null,
    null,
    null,
  ]);

  const listado = (await (
    await page.request.get(`/api/catalog/products?q=${sello}`)
  ).json()) as { items: { slug: string; price: number | null; priceVaries: boolean }[] };

  const tarjeta = listado.items.find((item) => item.slug === slug);
  expect(tarjeta!.price).toBe(1.2);
  expect(tarjeta!.priceVaries, 'se dijo «desde» sin que nada varíe').toBe(false);

  await page.goto(`/catalogo?q=${sello}`);
  const artículo = page.locator('.ti-card').filter({ hasText: `Sobres iguales ${sello}` });
  await expect(artículo).toContainText(/S\/\s*1[.,]20/);
  await expect(artículo).not.toContainText('Desde');
});

test('El listado de una categoría enseña el mismo precio que el general', async ({ page }) => {
  // **Son dos proyecciones distintas** —`ProductService` y `CategoryService`—
  // y el mismo cambio hubo que hacerlo en las dos. Probar solo una deja a la
  // otra sin nada que la sujete.
  await loginAsE2eAdmin(page);

  const sello = Date.now();
  const slug = `mochila-categoria-${sello}`;
  const api = page.request;
  const csrf = (await (await api.get('/api/admin/auth/csrf')).json()) as { csrfToken: string };
  const cabeceras = { 'X-CSRF-Token': csrf.csrfToken };

  const categoria = await api.post('/api/admin/catalog/categories', {
    headers: cabeceras,
    data: {
      name: `Escolar precios ${sello}`,
      slug: `escolar-precios-${sello}`,
      parentId: null,
      description: null,
      imageId: null,
      sortOrder: 0,
    },
  });
  expect(categoria.ok(), `categoría: ${categoria.status()} ${await categoria.text()}`).toBe(true);
  const categoriaId = ((await categoria.json()) as { id: string }).id;

  const ficha = await sembrar(api, { name: `Mochila categoria ${sello}`, slug, listPrice: 30 }, [
    45,
    null,
  ]);

  const asignada = await api.put(`/api/admin/catalog/products/${ficha.id}/categories`, {
    headers: cabeceras,
    data: { categoryIds: [categoriaId], primaryCategoryId: categoriaId },
  });
  expect(asignada.ok(), `asignar: ${asignada.status()} ${await asignada.text()}`).toBe(true);

  const detalle = await api.get(`/api/catalog/categories/escolar-precios-${sello}`);
  expect(detalle.status(), 'la categoría pública').toBe(200);

  const contenido = (await detalle.json()) as {
    products: { items: { slug: string; price: number | null; priceVaries: boolean }[] };
  };
  const tarjeta = contenido.products.items.find((item) => item.slug === slug);

  expect(tarjeta, 'el producto no aparece en su categoría').toBeTruthy();
  expect(tarjeta!.price, 'la tarjeta de la categoría no enseña el mínimo').toBe(30);
  expect(tarjeta!.priceVaries, 'la tarjeta de la categoría no avisa de que varían').toBe(true);

  await page.goto(`/catalogo/escolar-precios-${sello}`);
  await expect(
    page.locator('.ti-card').filter({ hasText: `Mochila categoria ${sello}` }),
  ).toContainText(/Desde\s+S\/\s*30/);
});
