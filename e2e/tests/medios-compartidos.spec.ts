import type { APIRequestContext } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * El quinto criterio de `ENTREGA-04D-VARIANTES-CATEGORIAS.md` §9.
 *
 * **Un mismo archivo usado por marca, categoría, presentación y producto.** Se
 * da de baja, y ninguna de las cuatro pantallas puede fallar con un error de
 * base: las tres primeras quedan sin imagen y el producto pierde esa fila de
 * galería.
 *
 * Va por API entero a propósito: lo que se afirma es que **el sistema** no se
 * rompe, y montarlo por pantalla haría cuatro veces el mismo camino ya
 * probado en sus entregas.
 */

const PNG_1X1 = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

test('Dar de baja un archivo usado por cuatro cosas no rompe ninguna', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const api: APIRequestContext = page.request;

  const csrf = (await (await api.get('/api/admin/auth/csrf')).json()) as { csrfToken: string };
  const cabeceras = { 'X-CSRF-Token': csrf.csrfToken };

  // --- El archivo, uno solo ------------------------------------------------
  // El nombre lleva la marca de tiempo: subir dos veces el mismo contenido
  // avisa de duplicado, y esta prueba corre en cada vuelta de la suite.
  const subida = await api.post('/api/admin/media', {
    headers: cabeceras,
    multipart: {
      // `ownerModuleCode` es obligatorio: el archivo sabe de qué módulo es.
      ownerModuleCode: 'catalog',
      file: { name: `compartida-${Date.now()}.png`, mimeType: 'image/png', buffer: PNG_1X1 },
    },
  });

  expect(subida.ok(), `subir: ${subida.status()} ${await subida.text()}`).toBe(true);
  const archivo = (await subida.json()) as { mediaAssetId: string };

  // --- Los cuatro que lo usan ---------------------------------------------
  const sello = Date.now();

  const marca = await api.post('/api/admin/catalog/brands', {
    headers: cabeceras,
    data: {
      name: `Marca compartida ${sello}`,
      slug: `marca-compartida-${sello}`,
      description: null,
      logoId: archivo.mediaAssetId,
    },
  });
  expect(marca.ok(), `marca: ${marca.status()} ${await marca.text()}`).toBe(true);
  const marcaId = ((await marca.json()) as { id: string }).id;

  const categoria = await api.post('/api/admin/catalog/categories', {
    headers: cabeceras,
    data: {
      name: `Categoria compartida ${sello}`,
      slug: `categoria-compartida-${sello}`,
      parentId: null,
      description: null,
      imageId: archivo.mediaAssetId,
      sortOrder: 0,
    },
  });
  expect(categoria.ok(), `categoría: ${categoria.status()} ${await categoria.text()}`).toBe(true);
  const categoriaId = ((await categoria.json()) as { id: string }).id;

  const producto = await api.post('/api/admin/catalog/products', {
    headers: cabeceras,
    data: {
      name: `Producto compartido ${sello}`,
      slug: `producto-compartido-${sello}`,
      shortDescription: null,
      description: null,
      primaryCategoryId: null,
      categoryIds: [],
      brandId: marcaId,
      listPrice: 1,
      saleUnit: null,
      variantLabel: null,
      code: null,
      barcode: null,
    },
  });
  expect(producto.ok(), `producto: ${producto.status()} ${await producto.text()}`).toBe(true);
  const creado = (await producto.json()) as { id: string; items: { id: string }[] };

  // La presentación única lo lleva como su imagen…
  const presentación = await api.put(`/api/admin/catalog/items/${creado.items[0].id}`, {
    headers: cabeceras,
    data: {
      variantValue: null,
      code: null,
      barcode: null,
      priceOverride: null,
      imageId: archivo.mediaAssetId,
      sortOrder: 0,
      isActive: true,
    },
  });
  expect(presentación.ok(), `presentación: ${presentación.status()} ${await presentación.text()}`).toBe(true);

  // …y el producto lo lleva **además** como fila de su galería, que es una
  // relación distinta: la cuarta.
  const galería = await api.post(`/api/admin/catalog/products/${creado.id}/images`, {
    headers: cabeceras,
    data: { mediaAssetId: archivo.mediaAssetId, isPrimary: true },
  });
  expect(galería.ok(), `galería: ${galería.status()} ${await galería.text()}`).toBe(true);

  const antes = (await (await api.get(`/api/admin/catalog/products/${creado.id}`)).json()) as {
    images: unknown[];
  };
  expect(antes.images, 'la galería no llegó a tener la imagen').toHaveLength(1);

  // --- Se da de baja el archivo -------------------------------------------
  const baja = await api.delete(`/api/admin/media/${archivo.mediaAssetId}`, { headers: cabeceras });
  expect(baja.ok(), `baja del archivo: ${baja.status()} ${await baja.text()}`).toBe(true);

  // --- Y ninguna de las cuatro falla --------------------------------------
  // Se afirma sobre **cada lectura por separado**: un 500 en cualquiera de
  // ellas es exactamente el fallo que este criterio existe para cazar.
  const marcaDespués = await api.get('/api/admin/catalog/brands');
  expect(marcaDespués.status(), 'listar marcas tras la baja').toBe(200);

  const categoríaDespués = await api.get('/api/admin/catalog/categories');
  expect(categoríaDespués.status(), 'listar categorías tras la baja').toBe(200);

  const fichaDespués = await api.get(`/api/admin/catalog/products/${creado.id}`);
  expect(fichaDespués.status(), 'la ficha del producto tras la baja').toBe(200);

  const ficha = (await fichaDespués.json()) as {
    images: unknown[];
    items: { imageUrl: string | null }[];
  };

  // El producto **pierde esa fila de galería**, no se queda con una imagen
  // que ya no existe.
  expect(ficha.images, 'la galería conservó una fila de un archivo dado de baja').toHaveLength(0);

  // Y las tres primeras quedan **sin imagen**, que no es lo mismo que
  // apuntando a un archivo muerto.
  const marcas = (await marcaDespués.json()) as { id: string; logoUrl: string | null }[];
  expect(marcas.find((m) => m.id === marcaId)?.logoUrl ?? null, 'la marca conservó el logo').toBeNull();

  const categorías = (await categoríaDespués.json()) as { id: string; imageUrl: string | null }[];
  expect(
    categorías.find((c) => c.id === categoriaId)?.imageUrl ?? null,
    'la categoría conservó la imagen',
  ).toBeNull();

  expect(ficha.items[0].imageUrl ?? null, 'la presentación conservó la imagen').toBeNull();

  // Y la pantalla pública del producto tampoco revienta.
  const pública = await api.get(`/api/catalog/products/producto-compartido-${sello}`);
  expect([200, 404], `la ficha pública dio ${pública.status()}`).toContain(pública.status());
});
