import type { Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * M01 · Marcas — criterios de `ENTREGA-04A-MARCAS.md` §6.
 *
 * Es el primer módulo, aparte de CORE, que se monta a sí mismo en el
 * frontend, así que lo que se afirma aquí es el patrón que van a repetir
 * categorías, productos y variantes.
 *
 * `global-setup.ts` deja M01 activo y **sin una sola marca**: el seed no trae
 * contenido de negocio (SPEC §6.9), que es justo lo que hace observable el
 * estado vacío.
 *
 * Cada prueba usa nombres de marca distintos, para no depender del orden en
 * que se ejecuten.
 */

const MARCAS = '/admin/catalogo/marcas';

/**
 * Rellena el panel lateral y guarda.
 *
 * Usa el botón de la cabecera, que está siempre. El del estado vacío —«Crear
 * la primera marca»— abre el mismo panel, pero solo existe mientras no hay
 * ninguna, y apuntar a los dos a la vez hace que el selector case con dos
 * elementos justo en la pantalla vacía.
 */
async function crearMarca(page: Page, nombre: string) {
  await page.getByRole('button', { name: 'Nueva marca', exact: true }).click();

  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();

  await panel.getByLabel('Nombre').fill(nombre);
  await panel.getByRole('button', { name: 'Crear marca' }).click();
}

/** Una fila del listado, por el nombre visible de la marca. */
function fila(page: Page, nombre: string) {
  return page.locator('tbody tr').filter({ hasText: nombre });
}

test('Una marca creada aparece en el listado con su dirección web', async ({ page }) => {
  const record = themeRecorder(page, 'catalogo');
  await loginAsE2eAdmin(page);
  await page.goto(MARCAS);

  await crearMarca(page, 'Artesco');

  await expect(page.getByRole('status').filter({ hasText: 'Se creó la marca' })).toBeVisible();

  const artesco = fila(page, 'Artesco');
  await expect(artesco).toBeVisible();
  // El slug se generó del nombre, y se muestra porque es la dirección
  // pública. No es un identificador: eso sigue sin verse nunca.
  await expect(artesco).toContainText('artesco');
  await expect(artesco).toContainText('Visible');

  await record('marcas-con-datos');
});

test('Crear ARTESCO existiendo Artesco explica que los nombres no distinguen mayúsculas', async ({
  page,
}) => {
  const record = themeRecorder(page, 'catalogo');
  await loginAsE2eAdmin(page);
  await page.goto(MARCAS);

  await crearMarca(page, 'Faber-Castell');
  await expect(fila(page, 'Faber-Castell')).toBeVisible();

  // El 409 es la respuesta correcta y esperada, no un fallo: el navegador lo
  // anuncia en consola igualmente.
  const aviso = page.getByRole('dialog').getByRole('alert');

  await duringExpectedOutage(page, async () => {
    await crearMarca(page, 'FABER-CASTELL');
    await expect(aviso).toBeVisible();
  });

  // La frase dice **por qué** choca. Un «ya existe» genérico dejaría a quien
  // lo escribió sin entender qué colisiona, porque en pantalla no hay dos
  // nombres iguales: la unicidad usa core.es_ci, que ignora mayúsculas.
  // Nombra **la que ya existe**, con su grafía, no la que se acaba de
  // teclear: en pantalla no hay dos nombres iguales, así que sin ver el otro
  // no se entiende qué choca.
  await expect(aviso).toContainText('Ya existe una marca llamada «Faber-Castell»');
  await expect(aviso).toContainText('no distinguen mayúsculas');
  await expect(aviso).not.toContainText('409');
  await expect(aviso).not.toContainText('slug');

  await record('marcas-conflicto-por-grafia');

  // No se creó nada: sigue habiendo una sola fila con ese nombre.
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();
  await expect(fila(page, 'aber-astell')).toHaveCount(0);
});

test('Dar de baja una marca avisa sin contar productos y no actúa en cascada', async ({ page }) => {
  const record = themeRecorder(page, 'catalogo');
  await loginAsE2eAdmin(page);
  await page.goto(MARCAS);

  await crearMarca(page, 'Stanford');
  const stanford = fila(page, 'Stanford');
  await expect(stanford).toContainText('Visible');

  await stanford.getByRole('button', { name: 'Dar de baja' }).click();

  const dialogo = page.getByRole('alertdialog');
  await expect(dialogo).toBeVisible();

  // Una frase, no un recuento: contar productos por marca es el mismo caso
  // que contar referencias a un archivo, y se descartó por no tener segundo
  // caso real (SPEC §6.8). Y dice que NO hay cascada.
  await expect(dialogo).toContainText('seguirán existiendo');
  await expect(dialogo).not.toContainText(/\d+\s+producto/);

  // El botón nombra la acción, nunca «Aceptar».
  await expect(dialogo.getByRole('button', { name: 'Dar de baja' })).toBeVisible();
  await expect(dialogo.getByRole('button', { name: /^\s*aceptar\s*$/i })).toHaveCount(0);

  await record('marcas-aviso-antes-de-dar-de-baja');

  await dialogo.getByRole('button', { name: 'Dar de baja' }).click();

  // Baja lógica: sigue en la lista, atenuada, no desaparecida. Desaparecerla
  // haría creer que se borró.
  await expect(stanford).toBeVisible();
  await expect(stanford).toContainText('Oculta');
  await expect(stanford).toHaveAttribute('data-dimmed', 'true');

  await record('marcas-con-una-oculta');
});

test('El indicador de carga no parpadea en una respuesta rápida', async ({ page }) => {
  await loginAsE2eAdmin(page);

  // Respuesta deliberadamente lenta, pero por debajo del umbral: 300 ms.
  await page.route('**/api/admin/catalog/brands', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.continue();
      return;
    }

    await new Promise((resolve) => setTimeout(resolve, 300));
    await route.continue();
  });

  await page.goto(MARCAS);

  // Se vigila **el indicador de la tabla**, no cualquier spinner: el arranque
  // de la aplicación tiene el suyo mientras resuelve capacidades y sesión, y
  // ese es legítimo — es una pantalla en blanco lo que evita.
  const indicadorDeLaTabla = page.locator('.ui-table__state .ui-spinner');

  // Durante los primeros 900 ms no debe aparecer: un indicador que entra y
  // sale hace que una respuesta rápida se perciba como lenta.
  const hasta = Date.now() + 900;
  while (Date.now() < hasta) {
    expect(
      await indicadorDeLaTabla.count(),
      'el indicador de la tabla apareció en una carga de 300 ms',
    ).toBe(0);
  }

  // Y la pantalla acabó cargando de verdad, no quedó en blanco.
  await expect(page.locator('.ui-table-wrap')).toBeVisible();
  await expect(page.locator('.ui-table__state .ui-spinner')).toHaveCount(0);

  await page.unroute('**/api/admin/catalog/brands');
});

test('El foco no se pierde y Escape cierra el panel sin guardar', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto(MARCAS);

  await expect(page.locator('main')).toBeVisible();

  // Recorrido con teclado: en ningún salto el foco puede caer al body.
  const enfocables = await page
    .locator('main a[href], main button:not([disabled]), main input:not([disabled])')
    .count();

  for (let salto = 1; salto < Math.max(enfocables, 4); salto += 1) {
    await page.keyboard.press('Tab');

    const perdido = await page.evaluate(
      () => !document.activeElement || document.activeElement === document.body,
    );
    expect(perdido, `tras ${salto} pulsaciones de Tab el foco se perdió`).toBe(false);
  }

  // Escape cierra el panel **sin guardar**: si guardara, «Vinifan» existiría.
  await page.getByRole('button', { name: 'Nueva marca', exact: true }).click();
  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();

  await panel.getByLabel('Nombre').fill('Vinifan');
  await page.keyboard.press('Escape');

  await expect(panel).toBeHidden();
  await expect(fila(page, 'Vinifan')).toHaveCount(0);
});

test('Con M01 desactivado no queda entrada de menú, ni ruta viva, ni hueco en el inicio', async ({
  page,
}) => {
  test.setTimeout(180_000);

  await loginAsE2eAdmin(page);

  // Con M01 activo, la entrada está.
  await page.goto('/admin');
  await expect(page.getByRole('navigation')).toContainText('Marcas');

  await page.goto('/admin/modulos');

  // Desactivar reinicia el host de verdad: el sondeo de reconexión falla a
  // propósito varias veces mientras el contenedor vuelve.
  await duringExpectedOutage(page, async () => {
    await page.locator('#modulo-catalog').getByRole('switch').click();
    await page.getByRole('alertdialog').getByRole('button', { name: /^Desactivar/ }).click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });

  await expect(page.locator('#modulo-catalog')).toContainText('Inactivo');

  // 1 · No hay entrada de menú. No aparece deshabilitada ni tachada: no está.
  await page.goto('/admin');
  await expect(page.getByRole('navigation')).not.toContainText('Marcas');

  // 2 · La ruta no existe. Quien la escriba a mano no encuentra una pantalla
  // rota, sino la redirección de `app/routes.tsx`.
  await page.goto(MARCAS);
  await expect(page).not.toHaveURL(new RegExp(`${MARCAS}$`));

  // 3 · El inicio no queda con un hueco: la lista de módulos activos sale de
  // /api/capabilities, así que catalog sencillamente ya no está.
  //
  // La insignia se busca por su texto exacto: «catalog» a secas es subcadena
  // de «demo_catalog», que sí sigue activo, y buscarla suelta daría un falso
  // positivo permanente.
  await page.goto('/admin');
  await expect(page.getByText('Módulos activos')).toBeVisible();
  await expect(page.getByText(/^catalog\s/)).toHaveCount(0);
  await expect(page.getByText(/^demo_catalog\s/)).toHaveCount(1);

  // Se deja como se encontró: el arnés no puede dejar el entorno distinto
  // para quien corra la siguiente prueba.
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
