/**
 * M02 — evidencia suplementaria de los criterios 3, 5, 13, 16–19, 21, 24, 25 y 32.
 * No modifica ningún componente ni ningún contrato del producto.
 * Se ejecuta contra el stack efímero de e2e.  Si una prueba descubre un
 * defecto de producto, se registra y NO se corrige dentro de este frente.
 */
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { APIRequestContext, Locator, Page, Request } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';

const SELLO = Date.now();
const DESTACADOS = '/admin/contenido/productos-destacados';
const PNG_ANCHO = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAIAAADwyuo0AAAAEElEQVR4nGP8z4AATEhsBgAZNQEDGy9LEgAAAABJRU5ErkJggg==', 'base64',
); // PNG real de 4 × 2 px.
const PNG_ALTO = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAIAAAAECAIAAAArjXluAAAAE0lEQVR4nGP8z8DAwMDAxICFAgAbcQEHkfYvJQAAAABJRU5ErkJggg==', 'base64',
); // PNG real de 2 × 4 px.

type Item = {
  id: string; variantValue: string | null; code: string | null;
  barcode: string | null; priceOverride: number | null;
  imageId: string | null; sortOrder: number; isActive: boolean;
};
type Product = { id: string; name: string; slug: string; items: Item[] };
type Featured = {
  id: number; productId: string | null; productName: string;
  productSlug: string | null; productPrice: number | null;
  productPriceVaries: boolean; productCategory: string | null;
  imageUrl: string | null;
};

const createdCms: { collection: string; id: number }[] = [];
const createdProducts: string[] = [];
const createdCategories: string[] = [];

async function csrf(api: APIRequestContext) {
  const r = await api.get('/api/admin/auth/csrf');
  expect(r.ok(), `CSRF HTTP ${r.status()}`).toBe(true);
  return { 'X-CSRF-Token': ((await r.json()) as { csrfToken: string }).csrfToken };
}

async function imagen(api: APIRequestContext, nombre: string, bytes = PNG_ANCHO) {
  const r = await api.post('/api/admin/media', {
    headers: await csrf(api),
    multipart: { ownerModuleCode: 'cms', file: { name: nombre, mimeType: 'image/png', buffer: bytes } },
  });
  expect(r.ok(), `medio ${nombre}: ${r.status()} ${await r.text()}`).toBe(true);
  return ((await r.json()) as { mediaAssetId: string }).mediaAssetId;
}

async function banner(api: APIRequestContext, title: string, desktop: string, mobile: string | null) {
  const r = await api.post('/api/admin/cms/banners', {
    headers: await csrf(api),
    data: {
      title, subtitle: null, imageDesktopId: desktop, imageMobileId: mobile,
      altText: `Imagen de ${title}`, linkUrl: null, linkLabel: null,
      startsAt: null, endsAt: null,
    },
  });
  expect(r.ok(), `banner: ${r.status()} ${await r.text()}`).toBe(true);
  const id = ((await r.json()) as { id: number }).id;
  createdCms.push({ collection: 'banners', id });
  return id;
}

async function red(api: APIRequestContext, platform: string, url: string) {
  const r = await api.post('/api/admin/cms/social-links', {
    headers: await csrf(api), data: { platform, url },
  });
  expect(r.ok(), `red: ${r.status()} ${await r.text()}`).toBe(true);
  createdCms.push({ collection: 'social-links', id: ((await r.json()) as { id: number }).id });
}

async function categoria(api: APIRequestContext, name: string, slug: string) {
  const r = await api.post('/api/admin/catalog/categories', {
    headers: await csrf(api),
    data: { name, slug, parentId: null, description: null, imageId: null, sortOrder: 0 },
  });
  expect(r.ok(), `categoría: ${r.status()} ${await r.text()}`).toBe(true);
  const id = ((await r.json()) as { id: string }).id;
  createdCategories.push(id);
  return id;
}

async function producto(api: APIRequestContext, name: string, slug: string, listPrice: number | null) {
  const r = await api.post('/api/admin/catalog/products', {
    headers: await csrf(api), data: {
      name, slug, shortDescription: null, description: null, primaryCategoryId: null,
      categoryIds: [], brandId: null, listPrice, saleUnit: null, variantLabel: null,
      code: null, barcode: null,
    },
  });
  expect(r.ok(), `producto: ${r.status()} ${await r.text()}`).toBe(true);
  const p = (await r.json()) as Product;
  createdProducts.push(p.id);
  return p;
}

async function editarProducto(api: APIRequestContext, id: string, changes: Record<string, unknown>) {
  const r = await api.get(`/api/admin/catalog/products/${id}`);
  expect(r.ok()).toBe(true);
  const before = (await r.json()) as Record<string, unknown>;
  const edited = await api.put(`/api/admin/catalog/products/${id}`, {
    headers: await csrf(api), data: { ...before, ...changes },
  });
  expect(edited.ok(), `editar producto: ${edited.status()} ${await edited.text()}`).toBe(true);
}

async function editarItem(api: APIRequestContext, item: Item, price: number | null, value: string) {
  const r = await api.put(`/api/admin/catalog/items/${item.id}`, {
    headers: await csrf(api), data: {
      variantValue: value, code: item.code, barcode: item.barcode, priceOverride: price,
      imageId: item.imageId, sortOrder: item.sortOrder, isActive: true,
    },
  });
  expect(r.ok(), `editar presentación: ${r.status()} ${await r.text()}`).toBe(true);
}

async function agregarItem(api: APIRequestContext, productId: string, price: number, value: string) {
  const r = await api.post(`/api/admin/catalog/products/${productId}/items`, {
    headers: await csrf(api), data: {
      variantValue: value, code: null, barcode: null, priceOverride: price, imageId: null,
    },
  });
  expect(r.ok(), `agregar presentación: ${r.status()} ${await r.text()}`).toBe(true);
  return (await r.json()) as Item;
}

async function destacar(api: APIRequestContext, id: string) {
  const r = await api.post('/api/admin/cms/featured-products', {
    headers: await csrf(api), data: { productId: id, startsAt: null, endsAt: null },
  });
  expect(r.ok(), `destacar: ${r.status()} ${await r.text()}`).toBe(true);
  const featured = (await r.json()) as Featured;
  createdCms.push({ collection: 'featured-products', id: featured.id });
  return featured;
}

async function leerDestacados(api: APIRequestContext) {
  const r = await api.get('/api/cms/featured-products');
  expect(r.ok(), `destacados públicos: ${r.status()}`).toBe(true);
  return (await r.json()) as Featured[];
}

async function esperarDestacado(
  api: APIRequestContext, name: string, predicate: (p: Featured) => boolean,
) {
  let found: Featured | undefined;
  await expect.poll(async () => {
    found = (await leerDestacados(api)).find((p) => p.productName === name);
    return found && predicate(found);
  }, { message: `snapshot de ${name} no se actualizó`, timeout: 15_000 }).toBe(true);
  return found!;
}

function tarjeta(page: Page, name: string) {
  return page.locator('section[aria-labelledby="cms-featured-products-title"] article')
    .filter({ hasText: name });
}
function bannerCard(page: Page, name: string) {
  return page.locator('section[aria-labelledby="cms-banners-title"] article')
    .filter({ hasText: name });
}

async function modulo(page: Page, code: string, action: 'Activar' | 'Desactivar') {
  await page.goto('/admin/modulos');
  await duringExpectedOutage(page, async () => {
    await page.locator(`#modulo-${code}`).getByRole('switch').click();
    await page.getByRole('alertdialog')
      .getByRole('button', { name: new RegExp(`^${action}`) }).click();
    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });
}

// Limpieza explícita después de cada prueba: sin esto los datos sembrados
// alterarían las expectativas de `tienda.spec.ts` durante la puerta global.
// No oculta los fallos: únicamente devuelve CMS/Catálogo a su estado inicial.
test.afterEach(async ({ page }) => {
  await loginAsE2eAdmin(page);
  const headers = await csrf(page.request);
  for (const item of createdCms.reverse()) {
    const r = await page.request.delete(`/api/admin/cms/${item.collection}/${item.id}`, { headers });
    expect(r.ok(), `limpieza CMS ${item.collection}/${item.id}: ${r.status()}`).toBe(true);
  }
  createdCms.length = 0;
  for (const id of createdProducts.reverse()) {
    const r = await page.request.delete(`/api/admin/catalog/products/${id}`, { headers });
    expect(r.ok(), `limpieza Catálogo ${id}: ${r.status()}`).toBe(true);
  }
  createdProducts.length = 0;
  for (const id of createdCategories.reverse()) {
    const r = await page.request.delete(`/api/admin/catalog/categories/${id}`, { headers });
    expect(r.ok(), `limpieza categoría ${id}: ${r.status()}`).toBe(true);
  }
  createdCategories.length = 0;
});

test('[M02-C3] módulo inactivo: arranque, menú, portada y pie sin huecos', async ({ page }) => {
  test.setTimeout(240_000);
  await loginAsE2eAdmin(page);
  const name = `Banner desmontable ${SELLO}`;
  await banner(page.request, name, await imagen(page.request, `c3-${SELLO}.png`), null);
  await red(page.request, 'youtube', `https://youtube.com/@m02c3${SELLO}`);
  await page.goto('/');
  await expect(page.getByRole('heading', { name })).toBeVisible();
  await expect(page.getByRole('contentinfo')).toBeVisible();
  await modulo(page, 'cms', 'Desactivar');
  try {
    await page.goto('/admin');
    await expect(page.locator('main')).toBeVisible();
    const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
    await expect(menu).toBeVisible();
    await expect(menu.locator('a[href^="/admin/contenido/"]')).toHaveCount(0);
    await page.goto('/admin/contenido/banners');
    await expect(page).not.toHaveURL(/\/admin\/contenido\/banners$/);
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    for (const id of ['cms-banners', 'cms-promotions', 'cms-featured-products', 'cms-featured-projects']) {
      await expect(page.locator(`section[aria-labelledby="${id}-title"]`)).toHaveCount(0);
    }
    await expect(page.getByRole('contentinfo')).toHaveCount(0);
    const foot = page.locator('footer');
    if (await foot.count()) {
      const height = await foot.evaluate((node) => node.getBoundingClientRect().height);
      expect(height, 'el pie vacío ocupa espacio').toBe(0);
    }
    const caps = (await (await page.request.get('/api/capabilities')).json()) as {
      modules: { code: string }[];
    };
    expect(caps.modules.map((m) => m.code)).not.toContain('cms');
  } finally {
    await modulo(page, 'cms', 'Activar');
  }
  await page.goto('/');
  await expect(page.getByRole('heading', { name })).toBeVisible();
  await expect(page.getByRole('contentinfo')).toBeVisible();
});

test('[M02-C5] banner móvil: fallback real y dos proporciones', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const desktop = await imagen(page.request, `c5-horizontal-${SELLO}.png`, PNG_ANCHO);
  const mobile = await imagen(page.request, `c5-vertical-${SELLO}.png`, PNG_ALTO);
  const fallback = `Fallback ${SELLO}`;
  const directed = `Dirigido ${SELLO}`;
  await banner(page.request, fallback, desktop, null);
  await banner(page.request, directed, desktop, mobile);
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/');
  const large = bannerCard(page, directed).locator('img');
  await expect(large).toBeVisible();
  await expect.poll(() => large.evaluate((el) => {
    const img = el as HTMLImageElement;
    return img.complete ? img.naturalWidth / img.naturalHeight : 0;
  })).toBe(2);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  const small = bannerCard(page, directed).locator('img');
  await expect(small).toBeVisible();
  await expect.poll(() => small.evaluate((el) => {
    const img = el as HTMLImageElement;
    return img.complete ? img.naturalWidth / img.naturalHeight : 0;
  })).toBe(0.5);
  const inherited = bannerCard(page, fallback);
  await expect(inherited.locator('picture source')).toHaveCount(0);
  const original = inherited.locator('img');
  await expect(original).toBeVisible();
  await expect.poll(() => original.evaluate((el) => {
    const img = el as HTMLImageElement;
    return img.complete ? img.naturalWidth / img.naturalHeight : 0;
  })).toBe(2);
});

test('[M02-C13] corregir slug cambia el enlace público de verdad', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const name = `Enlace slug ${SELLO}`;
  const before = `antes-m02-${SELLO}`;
  const after = `despues-m02-${SELLO}`;
  const p = await producto(page.request, name, before, 12);
  await destacar(page.request, p.id);
  await page.goto('/');
  await expect(tarjeta(page, name).getByRole('link', { name }))
    .toHaveAttribute('href', `/producto/${before}`);
  await editarProducto(page.request, p.id, { slug: after });
  await esperarDestacado(page.request, name, (d) => d.productSlug === after);
  await page.reload();
  const link = tarjeta(page, name).getByRole('link', { name });
  await expect(link).toHaveAttribute('href', `/producto/${after}`);
  await expect(page.locator(`a[href="/producto/${before}"]`)).toHaveCount(0);
  await link.click();
  await expect(page).toHaveURL(new RegExp(`/producto/${after}$`));
  await expect(page.getByRole('heading', { name })).toBeVisible();
});

test('[M02-C16] precios variables: Desde; sin precio: A consultar', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const variable = `Variable M02 ${SELLO}`;
  const p = await producto(page.request, variable, `variable-m02-${SELLO}`, null);
  await editarItem(page.request, p.items[0], 5, 'Chico');
  await agregarItem(page.request, p.id, 9, 'Grande');
  await destacar(page.request, p.id);
  const none = `Consultar M02 ${SELLO}`;
  const p2 = await producto(page.request, none, `consultar-m02-${SELLO}`, null);
  await destacar(page.request, p2.id);
  await esperarDestacado(page.request, variable, (d) => d.productPrice === 5 && d.productPriceVaries);
  await esperarDestacado(page.request, none, (d) => d.productPrice === null && !d.productPriceVaries);
  await page.goto('/');
  await expect(tarjeta(page, variable)).toContainText(/Desde\s+S\/\s*5(?:[.,]00)?/);
  await expect(tarjeta(page, none)).toContainText('A consultar');
  await expect(tarjeta(page, none)).not.toContainText('Desde');
});

test('[M02-C17] gratis puro no se confunde con gratis + pago', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const pure = `Gratis puro M02 ${SELLO}`;
  await destacar(page.request, (await producto(page.request, pure, `gratis-puro-m02-${SELLO}`, 0)).id);
  const mixed = `Gratis mixto M02 ${SELLO}`;
  const p = await producto(page.request, mixed, `gratis-mixto-m02-${SELLO}`, null);
  await editarItem(page.request, p.items[0], 0, 'Gratis');
  await agregarItem(page.request, p.id, 7.5, 'De pago');
  await destacar(page.request, p.id);
  await esperarDestacado(page.request, pure, (d) => d.productPrice === 0 && !d.productPriceVaries);
  await esperarDestacado(page.request, mixed, (d) => d.productPrice === 0 && d.productPriceVaries);
  await page.goto('/');
  await expect(tarjeta(page, pure)).toContainText('Gratis');
  await expect(tarjeta(page, pure)).not.toContainText('Desde Gratis');
  await expect(tarjeta(page, mixed)).toContainText('Desde Gratis');
});

test('[M02-C18] retirar presentación cara cambia el estado de precio en portada', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const name = `Retiro cara M02 ${SELLO}`;
  const p = await producto(page.request, name, `retiro-cara-m02-${SELLO}`, null);
  await editarItem(page.request, p.items[0], 5, 'Barata');
  const high = await agregarItem(page.request, p.id, 9, 'Cara');
  await destacar(page.request, p.id);
  await esperarDestacado(page.request, name, (d) => d.productPrice === 5 && d.productPriceVaries);
  await page.goto('/');
  await expect(tarjeta(page, name)).toContainText(/Desde\s+S\/\s*5(?:[.,]00)?/);
  const result = await page.request.delete(`/api/admin/catalog/items/${high.id}`, {
    headers: await csrf(page.request),
  });
  expect(result.ok(), `retirar cara: ${result.status()} ${await result.text()}`).toBe(true);
  await esperarDestacado(page.request, name, (d) => d.productPrice === 5 && !d.productPriceVaries);
  await page.reload();
  await expect(tarjeta(page, name)).toContainText(/S\/\s*5(?:[.,]00)?/);
  await expect(tarjeta(page, name)).not.toContainText('Desde');
});

test('[M02-C19] editar precio de presentación llega al destacado', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const name = `Edición precio M02 ${SELLO}`;
  const p = await producto(page.request, name, `edicion-precio-m02-${SELLO}`, null);
  await editarItem(page.request, p.items[0], 5, 'Uno');
  await agregarItem(page.request, p.id, 9, 'Dos');
  await destacar(page.request, p.id);
  await esperarDestacado(page.request, name, (d) => d.productPrice === 5 && d.productPriceVaries);
  await editarItem(page.request, p.items[0], 7, 'Uno');
  await esperarDestacado(page.request, name, (d) => d.productPrice === 7 && d.productPriceVaries);
  await page.goto('/');
  await expect(tarjeta(page, name)).toContainText(/Desde\s+S\/\s*7(?:[.,]00)?/);
});

test('[M02-C21] destacado sin foto compara nombre y categoría con tarjeta Catálogo', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const catName = `Escolar sin foto ${SELLO}`;
  const catSlug = `escolar-sin-foto-m02-${SELLO}`;
  const catId = await categoria(page.request, catName, catSlug);
  const name = `Sin foto M02 ${SELLO}`;
  const p = await producto(page.request, name, `sin-foto-m02-${SELLO}`, 3);
  const assigned = await page.request.put(`/api/admin/catalog/products/${p.id}/categories`, {
    headers: await csrf(page.request),
    data: { categoryIds: [catId], primaryCategoryId: catId },
  });
  expect(assigned.ok(), `asignar categoría ${assigned.status()} ${await assigned.text()}`).toBe(true);
  await destacar(page.request, p.id);
  await esperarDestacado(page.request, name, (d) => d.productCategory === catName && d.imageUrl === null);
  await page.goto(`/catalogo/${catSlug}`);
  const catalog = page.locator('.ti-card').filter({ hasText: name });
  await expect(catalog).toBeVisible();
  await expect(catalog.locator('.ti-nophoto__context')).toHaveText(catName);
  await expect(catalog.locator('.ti-nophoto__name')).toHaveText(name);
  await page.goto('/');
  const cms = tarjeta(page, name);
  await expect(cms).toBeVisible();
  await expect(cms.locator('img')).toHaveCount(0);
  await expect(cms.getByText(catName, { exact: true })).toBeVisible();
  await expect(cms.getByRole('link', { name })).toBeVisible();
  // Paridad visual real, no solo que el nombre se repite fuera del cuadro.
  await expect(cms.locator('.ti-nophoto__context')).toHaveText(catName);
  await expect(cms.locator('.ti-nophoto__name')).toHaveText(name);
});

test('[M02-C24] Catálogo inactivo: ninguna solicitud al selector', async ({ page }) => {
  test.setTimeout(240_000);
  await loginAsE2eAdmin(page);
  const p = await producto(page.request, `Capacidad M02 ${SELLO}`, `capacidad-m02-${SELLO}`, 4);
  await destacar(page.request, p.id);
  // Dirección positiva: selector disponible hace una petición real.
  let requests = 0;
  const observe = (r: Request) => {
    if (r.method() === 'GET' && new URL(r.url()).pathname === '/api/admin/cms/featured-products/catalog') {
      requests += 1;
    }
  };
  page.on('request', observe);
  await page.goto(DESTACADOS);
  await page.getByRole('button', { name: /^(Destacar producto|Destacar el primer producto)$/ }).first().click();
  const drawer = page.getByRole('dialog');
  await drawer.getByLabel(/^Buscar producto/).fill('Capacidad');
  await drawer.getByRole('button', { name: 'Buscar' }).click();
  await expect.poll(() => requests).toBeGreaterThan(0);
  await drawer.getByRole('button', { name: 'Cancelar' }).click();
  await modulo(page, 'catalog', 'Desactivar');
  requests = 0;
  try {
    await page.goto(DESTACADOS);
    await expect(page.getByRole('heading', { name: 'Productos destacados' })).toBeVisible();
    await expect(page.getByText(/Catálogo no está disponible/)).toBeVisible();
    await page.waitForLoadState('networkidle');
    expect(requests, 'panel pidió selector con Catálogo inactivo').toBe(0);
    for (const action of ['Destacar producto', 'Destacar el primer producto', 'Actualizar todos', 'Actualizar datos', 'Volver a enlazar']) {
      await expect(page.getByRole('button', { name: action, exact: true })).toHaveCount(0);
    }
    await expect(page.getByLabel(/^Buscar producto/)).toHaveCount(0);
  } finally {
    page.off('request', observe);
    await modulo(page, 'catalog', 'Activar');
  }
  await expect(page.locator('#modulo-catalog')).toContainText('Activo');
});

test('[M02-C25] prefijos explican que hace falta una palabra completa', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await producto(page.request, `Plumón completo ${SELLO}`, `plumon-completo-m02-${SELLO}`, 10);
  await page.goto(DESTACADOS);
  await page.getByRole('button', { name: /^(Destacar producto|Destacar el primer producto)$/ }).first().click();
  const drawer = page.getByRole('dialog');
  const input = drawer.getByLabel(/^Buscar producto/);
  for (const prefix of ['plum', 'lapi', 'cuad']) {
    await input.fill(prefix);
    await drawer.getByRole('button', { name: 'Buscar' }).click();
    await expect(drawer.getByText(new RegExp(`No hay resultados para «${prefix}»`))).toBeVisible();
    await expect(drawer.getByText(/palabra completa/).first()).toBeVisible();
  }
  // Dirección positiva: una palabra completa sí tiene resultado y acción.
  await input.fill('Plumón');
  await drawer.getByRole('button', { name: 'Buscar' }).click();
  await expect(drawer.locator('li').filter({ hasText: `Plumón completo ${SELLO}` })).toBeVisible();
});

const CMS_PAGES = [
  '/admin/contenido/banners', '/admin/contenido/promociones',
  '/admin/contenido/productos-destacados', '/admin/contenido/trabajos-destacados',
  '/admin/contenido/redes-sociales',
];
async function files(root: string): Promise<string[]> {
  const out: string[] = [];
  for (const entry of await readdir(root, { withFileTypes: true })) {
    const full = path.join(root, entry.name);
    if (entry.isDirectory()) out.push(...await files(full));
    else if (/\.(?:css|ts|tsx)$/.test(entry.name)) out.push(full);
  }
  return out;
}

test('[M02-C32] móvil, escritorio, teclado y colores solo desde tokens', async ({ page }) => {
  test.setTimeout(180_000);
  await loginAsE2eAdmin(page);
  for (const viewport of [{ width: 390, height: 844 }, { width: 1280, height: 900 }]) {
    await page.setViewportSize(viewport);
    for (const route of CMS_PAGES) {
      await page.goto(route);
      await expect(page.locator('main')).toBeVisible();
      await page.waitForLoadState('networkidle');
      const width = await page.evaluate(() => ({
        scroll: document.documentElement.scrollWidth,
        client: document.documentElement.clientWidth,
      }));
      expect(width.scroll <= width.client + 1, `${route} desborda a ${viewport.width}px`).toBe(true);
      await page.evaluate(() => document.body.focus());
      await page.keyboard.press('Tab');
      const focus = await page.evaluate(() => {
        const el = document.activeElement as HTMLElement | null;
        if (!el || el === document.body) return null;
        const style = getComputedStyle(el);
        return { visible: style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) > 0 };
      });
      expect(focus, `${route} sin foco a ${viewport.width}px`).not.toBeNull();
      expect(focus!.visible, `${route} oculta foco a ${viewport.width}px`).toBe(true);
      await page.keyboard.press('Enter');
      await expect.poll(() => page.evaluate(() => {
        const main = document.querySelector('main');
        return main === document.activeElement || main?.contains(document.activeElement) === true;
      })).toBe(true);
    }
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    const width = await page.evaluate(() => ({
      scroll: document.documentElement.scrollWidth,
      client: document.documentElement.clientWidth,
    }));
    expect(width.scroll <= width.client + 1, `portada desborda a ${viewport.width}px`).toBe(true);
  }
  const root = fileURLToPath(new URL('../../frontend/src/modules/cms/', import.meta.url));
  const literals: string[] = [];
  const colorLiteral = /#[0-9a-f]{3,8}\b|\b(?:rgb|hsl)a?\s*\(/i;
  const named = /\b(?:color|background(?:Color)?|borderColor)\s*:\s*['"](?:black|white|red|blue|green|gray|grey|yellow|orange|purple|pink|brown)['"]/i;
  for (const name of await files(root)) {
    const lines = (await readFile(name, 'utf8')).split('\n');
    for (const [i, line] of lines.entries()) {
      if (colorLiteral.test(line) || named.test(line)) {
        literals.push(`${path.relative(root, name)}:${i + 1}: ${line.trim()}`);
      }
    }
  }
  expect(literals, `colores literales M02:\n${literals.join('\n')}`).toEqual([]);
});
