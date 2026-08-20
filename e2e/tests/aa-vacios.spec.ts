import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * Los estados vacíos, que **solo existen antes de que nadie cree nada**.
 *
 * Estaban repartidos por sus ficheros y pasaban por accidente: `catalogo`,
 * `categorias` y `productos` van antes alfabéticamente que casi todo lo que
 * siembra datos. En cuanto aparecieron `medios-compartidos` y
 * `presentaciones`, la de productos empezó a fallar en la suite entera
 * —sola seguía pasando— y las otras dos quedaron a una letra de hacerlo.
 *
 * Se juntan aquí con el prefijo `aa-` para que el orden **sea una decisión y
 * no un accidente del abecedario**. Es lo único de la suite que depende de
 * correr primero, y por eso se dice en el nombre del fichero.
 */

const MARCAS = '/admin/catalogo/marcas';
const CATEGORIAS = '/admin/catalogo/categorias';
const PRODUCTOS = '/admin/catalogo/productos';

test('Sin marcas, la pantalla invita a crear la primera', async ({ page }) => {
  const record = themeRecorder(page, 'catalogo');
  await loginAsE2eAdmin(page);
  await page.goto(MARCAS);

  const filas = page.locator('tbody tr');

  // Si esto falla, no es que el estado vacío esté roto: es que otra prueba
  // creó marcas antes. Se dice explícitamente en vez de dejar que la
  // aserción de abajo pase en vacío o falle sin explicar por qué.
  expect(
    await filas.count(),
    'esta prueba necesita empezar sin marcas — ¿otra las creó antes?',
  ).toBe(0);

  // Vacía, no rota: dice qué falta y ofrece la acción, sin parecer un error.
  await expect(page.getByText('Todavía no hay marcas')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Crear la primera marca' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('marcas-sin-ninguna-todavia');
});

test('Sin categorías, la pantalla invita a crear la primera', async ({ page }) => {
  const record = themeRecorder(page, 'categorias');
  await loginAsE2eAdmin(page);
  await page.goto(CATEGORIAS);

  expect(
    await page.locator('tbody tr').count(),
    'esta prueba necesita empezar sin categorías — ¿otra las creó antes?',
  ).toBe(0);

  await expect(page.getByText('Todavía no hay categorías')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Crear la primera categoría' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('categorias-sin-ninguna-todavia');
});

test('Sin productos, la pantalla invita a crear el primero', async ({ page }) => {
  const record = themeRecorder(page, 'productos');
  await loginAsE2eAdmin(page);
  await page.goto(PRODUCTOS);

  expect(
    await page.locator('tbody tr').count(),
    'esta prueba necesita empezar sin productos — ¿otra los creó antes?',
  ).toBe(0);

  await expect(page.getByText('Todavía no hay productos')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Crear el primer producto' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('productos-sin-ninguno-todavia');
});
