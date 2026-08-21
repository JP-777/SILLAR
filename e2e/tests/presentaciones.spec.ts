import type { Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * M01 · Presentaciones — criterios de `ENTREGA-04D-VARIANTES-CATEGORIAS.md` §9.
 *
 * Cada prueba crea **su propio producto**, con su nombre, y lo busca por él:
 * ninguna se fía de la posición en el listado ni de lo que dejaron las demás.
 */

const PRODUCTOS = '/admin/catalogo/productos';

/** Crea un producto y abre su ficha. */
async function abrirFicha(page: Page, nombre: string, precio?: string, codigo?: string) {
  await page.goto(PRODUCTOS);
  await page.getByRole('button', { name: 'Nuevo producto', exact: true }).click();

  const panel = page.getByRole('dialog');
  await panel.getByLabel(/^Nombre/).fill(nombre);

  if (precio !== undefined) {
    await panel.getByLabel('Precio').fill(precio);
  }

  if (codigo !== undefined) {
    await panel.getByLabel('Código', { exact: true }).fill(codigo);
  }

  await panel.getByRole('button', { name: 'Crear producto' }).click();
  await expect(panel).toBeHidden();

  await page.goto(`${PRODUCTOS}?`);
  await page.getByLabel('Buscar').fill(nombre);
  const fila = page.locator('tbody tr').filter({ hasText: nombre });
  await expect(fila).toBeVisible();
  await fila.getByRole('button', { name: 'Editar' }).click();

  const ficha = page.getByRole('dialog');
  await expect(ficha).toBeVisible();
  return ficha;
}

test('El momento: los valores se quedan, se anuncia con palabras y el cursor va a la segunda', async ({
  page,
}) => {
  const record = themeRecorder(page, 'presentaciones');
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Plumón de pizarra', '4.90', 'PLU-ART-PG');

  // **Con la ficha ya cargada**: que el drawer sea visible no dice que sus
  // campos hayan llegado, y sobre una ficha en blanco «no aparece la palabra
  // variante» se cumple sola.
  await expect(ficha.getByLabel('Código', { exact: true })).toHaveValue('PLU-ART-PG');

  // Antes de pulsar, la palabra no existe.
  await expect(ficha).not.toContainText(/variante/i);

  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();

  // 1 · **Los valores se quedan donde estaban.** Es lo que impide leerlo como
  //     pérdida, y no depende de ninguna animación.
  const primeraFila = ficha.locator('.cat-variants__row').nth(1);
  await expect(primeraFila.getByLabel(/^Código de la presentación 1/)).toHaveValue('PLU-ART-PG');

  // 2 · Un aviso `role="status"` lo dice con palabras…
  const aviso = ficha.getByRole('status').filter({ hasText: 'primera presentación' });
  await expect(aviso).toBeVisible();

  // …**sin robar el foco**: el foco está en la segunda fila, no en el aviso.
  const enElAviso = await page.evaluate(() => {
    const status = document.querySelector('[role="status"] .cat-variants__announce');
    return status?.contains(document.activeElement) ?? false;
  });
  expect(enElAviso, 'el aviso se llevó el foco').toBe(false);

  // 3 · Y el cursor está en la segunda fila, que convierte el aviso en
  //     instrucción.
  const enSegunda = await page.evaluate(() => {
    const filas = document.querySelectorAll('.cat-variants__row');
    // [0] es la cabecera; [1] la primera presentación; [2] la segunda.
    return filas[2]?.contains(document.activeElement) ?? false;
  });
  expect(enSegunda, 'el cursor no entró en la segunda presentación').toBe(true);

  await record('presentaciones-el-momento');
});

test('El momento funciona entero con movimiento reducido', async ({ page }) => {
  // La comprobación obligatoria de la entrega. Con la preferencia activa no
  // queda nada de la animación: si la seguridad dependiera de ella, para
  // estas personas no habría ninguna.
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Marcador reducido', '3.00', 'MRC-RED');

  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();

  // Las tres cosas, iguales que sin la preferencia.
  await expect(
    ficha.locator('.cat-variants__row').nth(1).getByLabel(/^Código de la presentación 1/),
  ).toHaveValue('MRC-RED');
  await expect(ficha.getByRole('status').filter({ hasText: 'primera presentación' })).toBeVisible();

  const enSegunda = await page.evaluate(() => {
    const filas = document.querySelectorAll('.cat-variants__row');
    return filas[2]?.contains(document.activeElement) ?? false;
  });
  expect(enSegunda, 'sin animación, el cursor tampoco llegó a la segunda').toBe(true);
});

test('La vuelta atrás cambia de nombre según si destruye algo', async ({ page }) => {
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Resaltador vuelta', '2.00');
  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();

  // Segunda fila vacía: volver no destruye nada.
  await expect(ficha.getByRole('button', { name: 'Volver a una sola presentación' })).toBeVisible();

  // En cuanto se escribe algo, el mismo botón dice lo que de verdad hace.
  await ficha.getByLabel(/^Valor de la presentación 2/).fill('Azul');
  await expect(ficha.getByRole('button', { name: 'Quitar la última presentación' })).toBeVisible();
  await expect(ficha.getByRole('button', { name: 'Volver a una sola presentación' })).toHaveCount(0);
});

test('Ninguna celda de precio queda en blanco: dice de qué hereda y con qué valor', async ({
  page,
}) => {
  const record = themeRecorder(page, 'presentaciones');
  await loginAsE2eAdmin(page);

  // Hereda un número.
  const ficha = await abrirFicha(page, 'Cuaderno hereda', '4.90');
  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();
  await expect(ficha.getByRole('button', { name: /Hereda S\/\s*4[.,]90/ }).first()).toBeVisible();

  // Y es pulsable: pasa a precio propio **con el heredado ya cargado**.
  await ficha.locator('.cat-variants__inherit').first().click();
  await expect(ficha.getByLabel(/^Precio de la presentación 1/)).toHaveValue('4.9');

  await record('presentaciones-celda-que-hereda');

  await ficha.getByRole('button', { name: 'Cancelar' }).click();

  // Hereda «a consultar», que no es lo mismo que heredar un número.
  const sinPrecio = await abrirFicha(page, 'Torta hereda consultar');
  await sinPrecio
    .getByRole('button', { name: 'Este producto viene en varias presentaciones' })
    .click();
  await expect(sinPrecio.getByRole('button', { name: /Hereda: a consultar/ }).first()).toBeVisible();
});

test('Dos presentaciones sin código conviven, y la interfaz dice que está bien', async ({
  page,
}) => {
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Sobres sin codigo', '1.00');
  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();

  await ficha.getByLabel(/^Valor de la presentación 1/).fill('Chico');
  await ficha.getByLabel(/^Valor de la presentación 2/).fill('Grande');

  // **No es un conflicto.** Ni marca ni asterisco: una frase que dice que así
  // está bien, porque dos casillas vacías seguidas parecen un olvido.
  await expect(ficha.getByText(/Varias presentaciones sin código: está bien/)).toBeVisible();
  await expect(ficha.getByRole('alert')).toHaveCount(0);

  await ficha.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(ficha).toBeHidden();

  // Y la base las acepta: la unicidad de `code` no se viola con dos nulos.
  await page.getByLabel('Buscar').fill('Sobres sin codigo');
  await page.locator('tbody tr').filter({ hasText: 'Sobres sin codigo' }).getByRole('button', { name: 'Editar' }).click();

  const vuelta = page.getByRole('dialog');
  await expect(vuelta.getByLabel(/^Valor de la presentación 1/)).toHaveValue('Chico');
  await expect(vuelta.getByLabel(/^Valor de la presentación 2/)).toHaveValue('Grande');
});

test('El caso del plumón: tres colores, tres códigos de barras, un solo nombre y precio', async ({
  page,
}) => {
  test.setTimeout(120_000);
  const record = themeRecorder(page, 'presentaciones');
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Plumón trío', '5.50');
  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();

  await ficha.getByLabel('Qué cambia entre ellas').fill('Color de la tinta');

  const colores = [
    { valor: 'Negro', barras: '7751234000011' },
    { valor: 'Azul', barras: '7751234000028' },
    { valor: 'Rojo', barras: '7751234000035' },
  ];

  await ficha.getByLabel(/^Color de la tinta de la presentación 1/).fill(colores[0].valor);
  await ficha.getByLabel(/^Código de barras de la presentación 1/).fill(colores[0].barras);
  await ficha.getByLabel(/^Color de la tinta de la presentación 2/).fill(colores[1].valor);
  await ficha.getByLabel(/^Código de barras de la presentación 2/).fill(colores[1].barras);

  await ficha.getByRole('button', { name: 'Añadir presentación' }).click();
  await ficha.getByLabel(/^Color de la tinta de la presentación 3/).fill(colores[2].valor);
  await ficha.getByLabel(/^Código de barras de la presentación 3/).fill(colores[2].barras);

  await record('presentaciones-caso-del-plumon');

  await ficha.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(ficha).toBeHidden();

  // **Cada código de barras resuelve a la suya**, que es lo que el criterio
  // pide de verdad: es lo que hará la caja.
  for (const color of colores) {
    const resuelto = await page.request.get(
      `/api/admin/catalog/items/lookup?codigo=${color.barras}`,
    );
    expect(resuelto.ok(), `«${color.barras}» no resuelve: ${resuelto.status()}`).toBe(true);

    // El lookup devuelve la variante **con su producto**, que es lo que la
    // caja necesita: `{ item, productName, productSlug }`.
    const encontrado = (await resuelto.json()) as {
      item: { variantValue: string | null };
      productName: string;
    };

    expect(encontrado.item.variantValue, `«${color.barras}» resolvió a otra presentación`).toBe(
      color.valor,
    );
    expect(encontrado.productName).toBe('Plumón trío');
  }
});

test('El caso del cuaderno: dos precios propios conviven con el del producto', async ({ page }) => {
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Cuaderno dos precios', '4.90');
  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();

  await ficha.getByLabel(/^Valor de la presentación 1/).fill('100 hojas');
  await ficha.getByLabel(/^Valor de la presentación 2/).fill('200 hojas');

  // La primera hereda; la segunda tiene el suyo.
  await ficha.locator('.cat-variants__inherit').last().click();
  await ficha.getByLabel(/^Precio de la presentación 2/).fill('8.50');

  await ficha.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(ficha).toBeHidden();

  // El `list_price` del producto sigue ahí y la que hereda lo usa; la otra no.
  await page.getByLabel('Buscar').fill('Cuaderno dos precios');
  await page
    .locator('tbody tr')
    .filter({ hasText: 'Cuaderno dos precios' })
    .getByRole('button', { name: 'Editar' })
    .click();

  const vuelta = page.getByRole('dialog');
  await expect(vuelta.getByRole('button', { name: /Hereda S\/\s*4[.,]90/ })).toBeVisible();
  await expect(vuelta.getByLabel(/^Precio de la presentación 2/)).toHaveValue('8.5');
});

test('Desactivar la última presentación activa propone desactivar el producto', async ({ page }) => {
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Ultima presentacion', '1.00');
  await ficha.getByRole('button', { name: 'Cancelar' }).click();

  // Se intenta por API, que es donde vive la regla 8: la interfaz nunca
  // ofrece quitar la única, así que el camino real es este.
  const csrf = (await (await page.request.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };

  const lista = (await (
    await page.request.get('/api/admin/catalog/products?q=Ultima%20presentacion')
  ).json()) as { items: { id: string }[] };

  const detalle = (await (
    await page.request.get(`/api/admin/catalog/products/${lista.items[0].id}`)
  ).json()) as { items: { id: string }[] };

  const intento = await page.request.delete(
    `/api/admin/catalog/items/${detalle.items[0].id}`,
    { headers: { 'X-CSRF-Token': csrf.csrfToken } },
  );

  expect(intento.status(), 'quitar la última activa debería dar 409').toBe(409);

  const problema = (await intento.json()) as { title: string };

  // **Propone la salida**, no dice «no se puede» y punto.
  expect(problema.title.toLowerCase()).toContain('producto');
  expect(problema.title).not.toContain('409');
});

test('Un código repetido dice con qué producto choca', async ({ page }) => {
  await loginAsE2eAdmin(page);

  await abrirFicha(page, 'Dueno del codigo', '1.00', 'COD-UNICO-1');
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();

  const ficha = await abrirFicha(page, 'Ladron del codigo', '1.00');
  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();
  await ficha.getByLabel(/^Valor de la presentación 1/).fill('Uno');
  await ficha.getByLabel(/^Código de la presentación 1/).fill('COD-UNICO-1');
  await ficha.getByLabel(/^Valor de la presentación 2/).fill('Dos');

  const aviso = ficha.getByRole('alert');

  await duringExpectedOutage(page, async () => {
    await ficha.getByRole('button', { name: 'Guardar cambios' }).click();
    await expect(aviso).toBeVisible();
  });

  // El código es único en toda la instalación, así que hay que decir **con
  // cuál** se choca: sin eso, quien lo escribió busca a ciegas.
  await expect(aviso).toContainText('COD-UNICO-1');
  await expect(aviso).toContainText('Dueno del codigo');
});

test('A 390 px la tabla es una tarjeta por presentación, sin perder campos ni acciones', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await loginAsE2eAdmin(page);

  const ficha = await abrirFicha(page, 'Temperas movil', '6.00', 'TMP-MOV');
  await ficha.getByRole('button', { name: 'Este producto viene en varias presentaciones' }).click();

  await ficha.getByLabel(/^Valor de la presentación 1/).fill('Rojo');
  await ficha.getByLabel(/^Valor de la presentación 2/).fill('Azul');

  // 1 · Deja de ser tabla: la cabecera de columnas no se pinta, porque cada
  //     celda se titula sola.
  //
  //     **Primero que exista, y luego que esté escondida.** `toBeHidden()` se
  //     cumple también con el elemento ausente del DOM —comprobado—, así que
  //     sin esto la afirmación pasaría igual si la tabla no se hubiera
  //     renderizado nunca, que es justo el fallo que buscaría.
  await expect(ficha.locator('.cat-variants__row--head')).toBeAttached();
  await expect(ficha.locator('.cat-variants__row--head')).toBeHidden();

  // 2 · **No se pierde ningún campo.** Los cinco de cada presentación siguen
  //     ahí y siguen siendo usables — se afirma sobre los controles, no sobre
  //     el ancho de la rejilla.
  for (const fila of [1, 2]) {
    for (const campo of ['Valor', 'Código', 'Código de barras']) {
      await expect(
        ficha.getByLabel(new RegExp(`^${campo} de la presentación ${fila}`)),
      ).toBeVisible();
    }
    await expect(
      ficha.getByRole('button', { name: new RegExp(`^Precio de la presentación ${fila}`) }),
    ).toBeVisible();
  }

  // 3 · Ni ninguna acción: quitar, añadir y volver.
  await expect(ficha.getByRole('button', { name: 'Quitar', exact: true }).first()).toBeVisible();
  await expect(ficha.getByRole('button', { name: 'Añadir presentación' })).toBeVisible();
  await expect(ficha.getByRole('button', { name: 'Quitar la última presentación' })).toBeVisible();

  // 4 · Y cada celda dice de qué campo es, que es lo que hacía la cabecera.
  const rótulos = await ficha.locator('.cat-variants__row [data-label]').first().getAttribute('data-label');
  expect(rótulos, 'las celdas perdieron su etiqueta al volverse tarjeta').toBeTruthy();

  // 5 · Y la página no se desborda a lo ancho. **El mensaje nombra al
  //     culpable**: «se sale algo» obliga a abrir el navegador para saber
  //     qué, y eso es media hora cada vez que reaparezca.
  const culpables = await page.evaluate(() => {
    const ancho = document.documentElement.clientWidth;
    return Array.from(document.querySelectorAll<HTMLElement>('body *'))
      .filter((el) => el.getBoundingClientRect().right > ancho + 1)
      .slice(0, 6)
      .map((el) => `${el.tagName.toLowerCase()}.${el.className || '(sin clase)'}`);
  });

  expect(culpables, `se salen por la derecha a 390 px: ${culpables.join(', ')}`).toHaveLength(0);
});
