import type { Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * M01 · Productos — criterios de `ENTREGA-04C-PRODUCTOS.md` §5.
 *
 * Es la pantalla que el módulo existe para tener, y la regla que la gobierna
 * es una: **la variante es invisible mientras haya una sola.**
 */

const PRODUCTOS = '/admin/catalogo/productos';

/** Crea un producto rellenando solo lo que se le pase. */
async function crearProducto(
  page: Page,
  campos: { nombre: string; precio?: string; codigo?: string },
) {
  await page.getByRole('button', { name: 'Nuevo producto', exact: true }).click();

  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();

  await panel.getByLabel(/^Nombre/).fill(campos.nombre);

  if (campos.precio !== undefined) {
    await panel.getByLabel('Precio').fill(campos.precio);
  }

  if (campos.codigo !== undefined) {
    await panel.getByLabel('Código', { exact: true }).fill(campos.codigo);
  }

  await panel.getByRole('button', { name: 'Crear producto' }).click();
  await expect(panel).toBeHidden();
}

/**
 * La fila de un producto, **buscándolo primero**.
 *
 * El listado pagina, así que filtrar `tbody tr` por texto solo encuentra lo
 * que esté en la página 1. Sola, esta spec cabía; con la suite entera, sus
 * productos caían a la página 2 y la fila no existía. **Se busca por
 * identidad, no se confía en la posición** — que es la regla de la casa, y
 * esta era la tercera vez.
 */
async function fila(page: Page, nombre: string) {
  await page.getByLabel('Buscar').fill(nombre);

  const encontrada = page.locator('tbody tr').filter({ hasText: nombre });
  await expect(encontrada.first()).toBeVisible();
  return encontrada.first();
}

test('El caso del restaurante: un plato sin código ni precio, y sin la palabra «variante»', async ({
  page,
}) => {
  const record = themeRecorder(page, 'productos');
  await loginAsE2eAdmin(page);
  await page.goto(PRODUCTOS);

  // Un plato del menú: sin código, sin código de barras y sin precio.
  await crearProducto(page, { nombre: 'Lomo saltado del día' });

  const plato = await fila(page, 'Lomo saltado del día');
  await expect(plato).toBeVisible();

  // Nulo se lee «Consultar», no como un hueco ni como cero.
  await expect(plato).toContainText('Consultar');

  // **La regla de esta entrega.** La palabra no aparece en el listado…
  await expect(page.locator('main')).not.toContainText(/variante/i);

  // …ni al abrir la ficha, que es donde más fácil se cuela.
  await plato.getByRole('button', { name: 'Editar' }).click();
  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();
  await expect(panel).not.toContainText(/variante/i);

  // Y sus campos están ahí, como campos del producto.
  await expect(panel.getByLabel('Código', { exact: true })).toBeVisible();
  await expect(panel.getByLabel('Precio')).toBeVisible();

  await record('productos-caso-restaurante');

  // **Publicado**, que el criterio lo pide entero: el plato llega a la web sin
  // código, sin código de barras y sin precio. Nace así — el alta no pregunta
  // por la publicación, y eso es correcto: un producto que se da de alta es
  // para venderlo.
  await expect(panel.getByRole('switch', { name: 'Visible en la web' })).toBeChecked();

  await panel.getByRole('button', { name: 'Cancelar' }).click();
  await expect(panel).toBeHidden();

  await expect(plato).toContainText('En la web');

  // Publicado y sigue sin mencionar variantes en ninguna pantalla.
  await expect(page.locator('main')).not.toContainText(/variante/i);
});

test('Nulo es «consultar» y cero es «gratis», y no se confunden', async ({ page }) => {
  const record = themeRecorder(page, 'productos');
  await loginAsE2eAdmin(page);
  await page.goto(PRODUCTOS);

  await crearProducto(page, { nombre: 'Bolsa de regalo', precio: '0' });
  await crearProducto(page, { nombre: 'Torta por encargo' });

  // Al leer: dos palabras distintas, no un hueco frente a un 0.
  await expect(await fila(page, 'Bolsa de regalo')).toContainText('Gratis');
  await expect(await fila(page, 'Torta por encargo')).toContainText('Consultar');

  await record('productos-precio-nulo-y-cero');

  // Al editar: el cero se conserva como cero, y el nulo como vacío. Un
  // formulario que convirtiera el vacío en 0 al guardar haría que «consultar»
  // se volviera «gratis» sin que nadie lo pidiera.
  await (await fila(page, 'Bolsa de regalo')).getByRole('button', { name: 'Editar' }).click();
  await expect(page.getByRole('dialog').getByLabel('Precio')).toHaveValue('0');
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();

  await (await fila(page, 'Torta por encargo')).getByRole('button', { name: 'Editar' }).click();
  await expect(page.getByRole('dialog').getByLabel('Precio')).toHaveValue('');
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();
});

test('Renombrar un producto no cambia su dirección web', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto(PRODUCTOS);

  await crearProducto(page, { nombre: 'Cuaderno universitario' });

  const cuaderno = await fila(page, 'Cuaderno universitario');
  await expect(cuaderno).toContainText('cuaderno-universitario');

  await cuaderno.getByRole('button', { name: 'Editar' }).click();
  const panel = page.getByRole('dialog');

  await panel.getByLabel(/^Nombre/).fill('Cuaderno universitario cuadriculado');
  await panel.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(panel).toBeHidden();

  // El slug es el de antes: los enlaces que ya circulen siguen valiendo.
  const renombrado = await fila(page, 'Cuaderno universitario cuadriculado');
  await expect(renombrado).toContainText('cuaderno-universitario');
});

test('El código de la variante única se guarda y se relee como campo del producto', async ({
  page,
}) => {
  await loginAsE2eAdmin(page);
  await page.goto(PRODUCTOS);

  await crearProducto(page, { nombre: 'Plumón grueso', precio: '3.5', codigo: 'PLU-001' });

  await (await fila(page, 'Plumón grueso')).getByRole('button', { name: 'Editar' }).click();
  const panel = page.getByRole('dialog');

  // Se creó en la variante única sin que nadie la mencionara, y vuelve.
  await expect(panel.getByLabel('Código', { exact: true })).toHaveValue('PLU-001');

  // Y al editarlo, el cambio persiste — que es la costura de las dos
  // peticiones que resuelve la capa de servicios.
  await panel.getByLabel('Código', { exact: true }).fill('PLU-002');
  await panel.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(panel).toBeHidden();

  await (await fila(page, 'Plumón grueso')).getByRole('button', { name: 'Editar' }).click();
  await expect(page.getByRole('dialog').getByLabel('Código', { exact: true })).toHaveValue('PLU-002');
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();
});

test('El listado pagina, y se llega a la página 2 con controles alcanzables en móvil', async ({
  page,
}) => {
  test.setTimeout(120_000);

  await loginAsE2eAdmin(page);

  // Once productos para pasar de la página de diez. Se crean por API: esta
  // prueba es de la paginación, no del formulario.
  const csrf = (await (await page.request.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };

  for (let i = 1; i <= 11; i += 1) {
    const respuesta = await page.request.post('/api/admin/catalog/products', {
      headers: { 'X-CSRF-Token': csrf.csrfToken },
      data: {
        name: `Paginado ${i}`,
        slug: `paginado-${i}`,
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
    expect(respuesta.ok(), `no se pudo crear «Paginado ${i}»: ${respuesta.status()}`).toBe(true);
  }

  // En móvil, que es donde el tamaño del control decide si se puede o no.
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(PRODUCTOS);

  const siguiente = page.getByRole('button', { name: 'Siguiente' });
  await expect(siguiente).toBeVisible();

  // El control tiene que ser alcanzable con el dedo: con `sm` medía 26,8 px.
  // No se afirma un número mágico sino el mínimo por debajo del cual un
  // objetivo táctil deja de ser usable.
  const caja = await siguiente.boundingBox();
  expect(caja?.height ?? 0, 'el botón de paginación es demasiado bajo para el dedo').toBeGreaterThanOrEqual(32);

  await siguiente.click();

  // Se llegó de verdad a la página 2.
  await expect(page.getByRole('button', { name: 'Anterior' })).toBeEnabled();
  await expect(page.locator('tbody tr')).not.toHaveCount(0);
});

test('Un campo y un botón puestos en la misma fila miden lo mismo', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto(PRODUCTOS);

  // La altura de los controles se declara, no se deduce de sumar relleno y
  // tipo. Antes un botón medía 38,5 y un campo 46 por un `line-height` que
  // solo tenía uno de los dos.
  const alturaCampo = (await page.getByLabel('Buscar').boundingBox())?.height ?? 0;
  const alturaBoton =
    (await page.getByRole('button', { name: 'Nuevo producto' }).boundingBox())?.height ?? 0;

  expect(alturaCampo).toBeGreaterThan(0);
  expect(
    Math.abs(alturaCampo - alturaBoton),
    `campo ${alturaCampo}px contra botón ${alturaBoton}px`,
  ).toBeLessThanOrEqual(1);
});
