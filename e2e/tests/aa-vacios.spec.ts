import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
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

/**
 * Las filas **con datos**, que no son todas las del `tbody`.
 *
 * `Table` pinta el estado vacío como una fila más —un `<tr>` con un `<td
 * class="ui-table__state">` (`patterns.tsx:279-281`)—, así que contar
 * `tbody tr` da **1** con la tabla vacía, no 0.
 *
 * Estas tres pruebas exigían `toBe(0)` y pasaban igualmente: contaban antes
 * de que la página pintara, cuando no había ninguna fila de nada. **Nunca
 * habrían pasado sobre una tabla pintada, ni vacía ni llena**, y solo se vio
 * al hacer que navegar esperara al armazón.
 */
function filasConDatos(page: import('@playwright/test').Page) {
  return page.locator('tbody tr').filter({ hasNot: page.locator('.ui-table__state') });
}

const MARCAS = '/admin/catalogo/marcas';
const CATEGORIAS = '/admin/catalogo/categorias';
const PRODUCTOS = '/admin/catalogo/productos';

test('Sin marcas, la pantalla invita a crear la primera', async ({ page }) => {
  const record = themeRecorder(page, 'catalogo');
  await loginAsE2eAdmin(page);
  await page.goto(MARCAS);

  const filas = filasConDatos(page);

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
    await filasConDatos(page).count(),
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
    await filasConDatos(page).count(),
    'esta prueba necesita empezar sin productos — ¿otra los creó antes?',
  ).toBe(0);

  await expect(page.getByText('Todavía no hay productos')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Crear el primer producto' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('productos-sin-ninguno-todavia');
});

test('El sitio recién instalado tiene nombre, sin que nadie lo escriba dos veces', async ({
  page,
}) => {
  // **La instalación obliga a poner el nombre del negocio, así que la portada
  // no puede salir sin él.** Hasta hoy sí podía: el nombre iba a la fila de la
  // instalación y el ajuste público se quedaba en `PENDIENTE_DEFINIR` hasta
  // que alguien lo editara en Configuración — el mismo dato en dos sitios, y
  // el que se quedaba atrás era el que ve el público.
  //
  // Esta prueba existe para que esa rama vuelva a ser inalcanzable: si alguien
  // separa otra vez los dos caminos, la portada se queda sin encabezado y esto
  // se pone rojo.
  const ajuste = await (await page.request.get('/api/settings/public')).json();

  expect(
    (ajuste as Record<string, string>).business_name,
    'el ajuste público business_name se quedó en el marcador del seed',
  ).not.toBe('PENDIENTE_DEFINIR');

  // Sin sesión, que es como llega quien visita la tienda — y por eso hay que
  // apartar dos 401: la aplicación pide `/admin/auth/me` y `/admin/auth/csrf`
  // al arrancar en cualquier ruta y los maneja a propósito. Está en Pendientes
  // como lo que es: un visitante anónimo no debería provocar peticiones a
  // `/api/admin/`, y quitarlas es tocar el arranque de CORE.
  await duringExpectedOutage(page, async () => {
    await page.goto('/');
    await expect(
      page.getByRole('heading', { level: 1 }),
      'la portada de un sitio instalado no enseña el nombre del negocio',
    ).toBeVisible();
  });
});
