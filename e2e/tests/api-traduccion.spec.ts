import type { APIRequestContext } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * Detector de traducción: llama **una vez a cada endpoint de M01** y afirma
 * que ninguno devuelve 500.
 *
 * No es una suite de pruebas y no comprueba que nada haga lo correcto. Cubre
 * el punto ciego exacto de la regla «las pruebas de lógica no tocan la base»:
 * lo que solo se rompe **cuando EF traduce a SQL** es invisible para ellas, y
 * ninguna prueba unitaria puede verlo.
 *
 * Ya ha pasado dos veces —`BrandService.ListAsync` y `CategoryService.ListAsync`
 * proyectando con un método de instancia— y las dos veces el endpoint llevaba
 * días roto sin que nadie lo supiera, porque «listar» no parecía arriesgado.
 * Esta prueba habría cazado las dos el mismo día.
 *
 * **Cómo se amplía:** cada endpoint nuevo de M01 entra en la lista de abajo.
 * Un 404 o un 400 son respuestas legítimas —significan «esa fila no existe» o
 * «esos datos no valen», que es justo lo que se pide con identificadores
 * inventados—; un 500 significa que la consulta ni siquiera llegó a
 * ejecutarse.
 */

/** Un uuid v7 que no existe. Sirve para las rutas con `{id}`. */
const AUSENTE = '01a00000-0000-7000-8000-000000000000';

interface Llamada {
  metodo: 'GET' | 'POST' | 'PUT' | 'DELETE';
  ruta: string;
  /** Cuerpo mínimo para que el endpoint llegue a la base. */
  cuerpo?: unknown;
}

/**
 * Los 27 endpoints de M01, en el orden de sus archivos.
 *
 * Los cuerpos son deliberadamente **válidos en forma**: uno inválido se
 * rechaza en la validación, antes de tocar la base, y entonces la llamada no
 * probaría nada de lo que esta prueba existe para probar.
 */
const ENDPOINTS: Llamada[] = [
  // --- Marcas ---
  { metodo: 'GET', ruta: '/api/catalog/brands' },
  { metodo: 'GET', ruta: '/api/admin/catalog/brands' },
  { metodo: 'POST', ruta: '/api/admin/catalog/brands', cuerpo: { name: 'Detector Marca', slug: 'detector-marca', logoId: null } },
  { metodo: 'PUT', ruta: `/api/admin/catalog/brands/${AUSENTE}`, cuerpo: { name: 'Detector', slug: 'detector', logoId: null, isActive: true } },
  { metodo: 'DELETE', ruta: `/api/admin/catalog/brands/${AUSENTE}` },

  // --- Categorías ---
  { metodo: 'GET', ruta: '/api/catalog/categories' },
  { metodo: 'GET', ruta: '/api/catalog/categories/inexistente' },
  { metodo: 'GET', ruta: '/api/admin/catalog/categories' },
  { metodo: 'POST', ruta: '/api/admin/catalog/categories', cuerpo: { name: 'Detector Categoría', slug: 'detector-categoria', parentId: null, description: null, imageId: null, sortOrder: 0 } },
  { metodo: 'PUT', ruta: `/api/admin/catalog/categories/${AUSENTE}`, cuerpo: { name: 'Detector', slug: 'detector-c', parentId: null, description: null, imageId: null, sortOrder: 0, isActive: true } },
  { metodo: 'DELETE', ruta: `/api/admin/catalog/categories/${AUSENTE}` },

  // --- Productos ---
  { metodo: 'GET', ruta: '/api/catalog/products' },
  { metodo: 'GET', ruta: '/api/catalog/products/inexistente' },
  { metodo: 'GET', ruta: '/api/admin/catalog/products' },
  { metodo: 'POST', ruta: '/api/admin/catalog/products', cuerpo: { name: 'Detector Producto', slug: 'detector-producto', shortDescription: null, description: null, primaryCategoryId: null, categoryIds: [], brandId: null, listPrice: 1, saleUnit: null, variantLabel: null, code: null, barcode: null } },
  { metodo: 'GET', ruta: `/api/admin/catalog/products/${AUSENTE}` },
  { metodo: 'PUT', ruta: `/api/admin/catalog/products/${AUSENTE}`, cuerpo: { name: 'Detector', slug: 'detector-p', shortDescription: null, description: null, brandId: null, listPrice: 1, saleUnit: null, variantLabel: null, isPublic: false, isActive: true } },
  { metodo: 'DELETE', ruta: `/api/admin/catalog/products/${AUSENTE}` },
  { metodo: 'PUT', ruta: `/api/admin/catalog/products/${AUSENTE}/categories`, cuerpo: { categoryIds: [], primaryCategoryId: null } },
  { metodo: 'POST', ruta: `/api/admin/catalog/products/${AUSENTE}/images`, cuerpo: { mediaAssetId: AUSENTE, altText: null, isPrimary: false } },
  { metodo: 'DELETE', ruta: `/api/admin/catalog/products/${AUSENTE}/images/${AUSENTE}` },
  { metodo: 'PUT', ruta: `/api/admin/catalog/products/${AUSENTE}/images/order`, cuerpo: { orderedImageIds: [], primaryImageId: null } },

  // --- Variantes ---
  { metodo: 'GET', ruta: `/api/admin/catalog/products/${AUSENTE}/items` },
  { metodo: 'POST', ruta: `/api/admin/catalog/products/${AUSENTE}/items`, cuerpo: { variantValue: 'Detector', code: null, barcode: null, priceOverride: null, imageId: null } },
  { metodo: 'PUT', ruta: `/api/admin/catalog/items/${AUSENTE}`, cuerpo: { variantValue: 'Detector', code: null, barcode: null, priceOverride: null, imageId: null, sortOrder: 0, isActive: true } },
  { metodo: 'DELETE', ruta: `/api/admin/catalog/items/${AUSENTE}` },
  { metodo: 'GET', ruta: '/api/admin/catalog/items/lookup?codigo=nada' },

  // --- Los mismos listados, pero CON sus filtros puestos ---
  //
  // Esta es la mitad que de verdad importa, y la que faltaba: un listado
  // pelado ejecuta una consulta trivial, y **el filtro es exactamente donde
  // se cuela un `.Where(métodoDeInstancia)`** — la misma familia que rompió
  // `brands` y `categories`. Sin estas llamadas, esas ramas no se ejecutan
  // nunca y el detector daría verde sobre código que nadie ha corrido.
  //
  // Van al final a propósito: usan los slugs que crean los POST de arriba,
  // así que no pueden ir antes.
  { metodo: 'GET', ruta: '/api/catalog/products?category=detector-categoria&brand=detector-marca&q=detector&page=1&pageSize=5' },
  { metodo: 'GET', ruta: '/api/admin/catalog/products?q=detector&isActive=true&page=1&pageSize=5' },
  { metodo: 'GET', ruta: '/api/admin/catalog/products?q=detector&isActive=false&page=2&pageSize=5' },
  { metodo: 'GET', ruta: '/api/catalog/categories/detector-categoria?page=1&pageSize=5' },
  { metodo: 'GET', ruta: '/api/catalog/products/detector-producto' },
];

async function llamar(api: APIRequestContext, llamada: Llamada) {
  const opciones = llamada.cuerpo === undefined ? {} : { data: llamada.cuerpo };

  switch (llamada.metodo) {
    case 'GET':
      return api.get(llamada.ruta);
    case 'POST':
      return api.post(llamada.ruta, opciones);
    case 'PUT':
      return api.put(llamada.ruta, opciones);
    case 'DELETE':
      return api.delete(llamada.ruta);
  }
}

test('Ningún endpoint de M01 revienta al traducir su consulta a SQL', async ({ page }) => {
  test.setTimeout(120_000);

  await loginAsE2eAdmin(page);

  const rotos: string[] = [];

  for (const llamada of ENDPOINTS) {
    const respuesta = await llamar(page.request, llamada);

    if (respuesta.status() >= 500) {
      const cuerpo = (await respuesta.text()).slice(0, 200);
      rotos.push(`${llamada.metodo} ${llamada.ruta} → ${respuesta.status()} ${cuerpo}`);
    }
  }

  expect(
    rotos,
    `${rotos.length} de ${ENDPOINTS.length} endpoints de M01 devuelven 500:\n${rotos.join('\n')}`,
  ).toEqual([]);
});

/** Salto de línea, aparte para que ningún escape se pierda al editar. */
const SALTO = String.fromCharCode(10);

test('Ningún endpoint responde 200 con el cuerpo vacío', async ({ page }) => {
  test.setTimeout(120_000);

  // **Un cuerpo vacío no dice «no hay nada», dice «no hay respuesta».** Es la
  // misma regla que la `url` vacía de la galería, dos capas más abajo, y mordió
  // de verdad: «quién soy» devolvía 200 sin escribir nada —ni `Results.Ok(null)`
  // ni `Results.Json(null)` llegan a escribir el `null`—, el cliente lo recibía
  // como `undefined` en vez de `null`, y el arreglo del 401 quedó a medias sin
  // que nada se pusiera rojo.
  //
  // **Se mira lo que sale por el cable, no la firma del método.** La firma dice
  // qué devuelve el código; el cuerpo dice qué recibe quien lo lee.
  //
  // El 204 es distinto y no entra: ahí la ausencia de cuerpo *es* el contrato.
  await loginAsE2eAdmin(page);

  const mudos: string[] = [];
  let revisados = 0;

  for (const llamada of ENDPOINTS) {
    const respuesta = await llamar(page.request, llamada);

    if (respuesta.status() !== 200) {
      continue;
    }

    revisados += 1;

    if ((await respuesta.text()).trim() === '') {
      mudos.push(`${llamada.metodo} ${llamada.ruta}`);
    }
  }

  // **Un detector que no miró nada cuenta cero igual que uno que miró todo.**
  // Si ninguna llamada devolviera 200 —por un cambio de rutas, por una sesión
  // que caducó—, la lista de mudos saldría vacía y esto pasaría sin haber
  // comprobado un solo cuerpo.
  expect(revisados, 'ninguna llamada devolvió 200: el detector no revisó nada').toBeGreaterThan(0);

  expect(mudos, ['responden 200 sin escribir nada:', ...mudos].join(SALTO)).toEqual([]);
});


test('La lista de endpoints vigilados no se queda corta', async ({ page }) => {
  // La prueba de arriba solo vale si la lista está completa. Este recuento es
  // la parte que se olvida: si alguien añade un endpoint a M01 y no lo mete
  // en ENDPOINTS, arriba seguiría todo verde sin cubrirlo.
  //
  // Se cuentan **rutas distintas**, no llamadas: varias entradas apuntan al
  // mismo endpoint con filtros distintos, y eso no lo hace estar más
  // vigilado. 27 es lo que hay hoy:
  //   rg "\.Map(Get|Post|Put|Delete)\(" backend/Sillar.Modules.Catalog/Endpoints/
  // Al añadir un endpoint, se sube el número **y** se añade a la lista.
  const rutas = new Set(
    ENDPOINTS.map((llamada) => {
      const sinQuery = llamada.ruta.split('?')[0];
      // El uuid inventado y los slugs del detector se normalizan: lo que
      // identifica al endpoint es su forma, no sus valores.
      const forma = sinQuery
        .replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, '{id}')
        .replace(/\/(detector-[a-z-]+|inexistente)$/, '/{slug}');
      return `${llamada.metodo} ${forma}`;
    }),
  );

  expect(rutas.size, `faltan endpoints por vigilar: ${[...rutas].sort().join(', ')}`).toBe(27);

  await loginAsE2eAdmin(page);
});
