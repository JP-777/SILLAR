import type { APIRequestContext } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * La segunda mitad del criterio de imágenes del SPEC de M01.
 *
 * Que se **asocian** desde la galería de CORE ya lo afirman las pruebas de
 * marcas, categorías y el recorrido. Lo que faltaba es lo contrario, y es lo
 * que la interfaz promete por escrito —«Quitarlas aquí no borra el archivo»,
 * `ProductForm.tsx:507`—: **quitar la asociación deja el archivo en pie.**
 *
 * Importa porque es exactamente la clase de cosa que se rompe sin ruido: un
 * `DELETE` que además borra el archivo no falla, y el síntoma aparece días
 * después en otro producto que usaba la misma foto.
 */

const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

/** Sube un archivo y devuelve su identificador. */
async function subir(api: APIRequestContext, cabeceras: Record<string, string>, nombre: string) {
  const respuesta = await api.post('/api/admin/media', {
    headers: cabeceras,
    multipart: {
      ownerModuleCode: 'catalog',
      file: { name: nombre, mimeType: 'image/png', buffer: PNG },
    },
  });

  expect(respuesta.ok(), `subir «${nombre}»: ${respuesta.status()} ${await respuesta.text()}`).toBe(
    true,
  );

  return ((await respuesta.json()) as { mediaAssetId: string }).mediaAssetId;
}

test('Quitar la asociación de una imagen no borra el archivo, y otro producto la sigue viendo', async ({
  page,
}) => {
  await loginAsE2eAdmin(page);
  const api = page.request;
  const sello = Date.now();

  const { csrfToken } = (await (await api.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };
  const cabeceras = { 'X-CSRF-Token': csrfToken };

  const archivo = await subir(api, cabeceras, `compartido-${sello}.png`);

  // **Dos productos con la misma foto.** Es la condición que hace que el
  // defecto duela: si quitar la asociación borrara el archivo, el segundo se
  // quedaría sin imagen sin que nadie tocara nada suyo.
  const creados: string[] = [];

  for (const nombre of [`Cuaderno foto A ${sello}`, `Cuaderno foto B ${sello}`]) {
    const respuesta = await api.post('/api/admin/catalog/products', {
      headers: cabeceras,
      data: {
        name: nombre,
        slug: nombre.toLowerCase().replace(/[^a-z0-9]+/g, '-'),
        shortDescription: null,
        description: null,
        primaryCategoryId: null,
        categoryIds: [],
        brandId: null,
        listPrice: 5,
        saleUnit: null,
        variantLabel: null,
        code: null,
        barcode: null,
      },
    });

    expect(respuesta.ok(), `crear «${nombre}»: ${respuesta.status()}`).toBe(true);
    const id = ((await respuesta.json()) as { id: string }).id;
    creados.push(id);

    const asociada = await api.post(`/api/admin/catalog/products/${id}/images`, {
      headers: cabeceras,
      data: { mediaAssetId: archivo, isPrimary: true },
    });
    expect(asociada.ok(), `asociar en «${nombre}»: ${asociada.status()}`).toBe(true);
  }

  const [primero, segundo] = creados;

  // Se quita la asociación **por pantalla**, que es como lo hace una persona
  // y donde está escrita la promesa.
  await page.goto('/admin/catalogo/productos');
  await page.getByLabel('Buscar').fill(`Cuaderno foto A ${sello}`);
  await page
    .locator('tbody tr')
    .filter({ hasText: `Cuaderno foto A ${sello}` })
    .getByRole('button', { name: 'Editar' })
    .click();

  const ficha = page.getByRole('dialog');
  await expect(ficha.locator('.cat-images__item')).toHaveCount(1);

  // La promesa, escrita donde se lee: si desaparece de la interfaz, esta
  // prueba deja de tener sentido y hay que enterarse.
  await expect(ficha.getByText('Quitarlas aquí no borra el archivo.')).toBeVisible();

  await ficha.locator('.cat-images__item').getByRole('button', { name: 'Quitar' }).click();
  await expect(ficha.locator('.cat-images__item')).toHaveCount(0);

  // 1 · El primero se queda sin ella.
  const sinImagen = (await (
    await api.get(`/api/admin/catalog/products/${primero}`)
  ).json()) as { images: unknown[] };
  expect(sinImagen.images, 'quitar la asociación no la quitó').toHaveLength(0);

  // 2 · **El archivo sigue en la galería, activo.** Es la mitad que faltaba.
  const galeria = (await (
    await api.get('/api/admin/media?pageSize=200')
  ).json()) as { items: { mediaAssetId: string; isActive: boolean }[] };

  const enLaGaleria = galeria.items.find((f) => f.mediaAssetId === archivo);
  expect(enLaGaleria, 'quitar la asociación borró el archivo de la galería').toBeTruthy();
  expect(enLaGaleria!.isActive, 'quitar la asociación dio de baja el archivo').toBe(true);

  // 3 · Y el otro producto lo sigue viendo, con su URL servida de verdad.
  const otro = (await (
    await api.get(`/api/admin/catalog/products/${segundo}`)
  ).json()) as { images: { url: string }[] };

  expect(otro.images, 'el otro producto perdió su imagen').toHaveLength(1);

  const archivoServido = await api.get(otro.images[0].url);
  expect(archivoServido.status(), 'el archivo ya no se sirve').toBe(200);
});
