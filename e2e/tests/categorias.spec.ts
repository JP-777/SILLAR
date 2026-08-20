import type { Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * M01 · Categorías — criterios de `ENTREGA-04B-CATEGORIAS.md` §4.
 *
 * Lo que categorías tiene y marcas no: **es un árbol**, la baja **cuenta**
 * los productos que se quedan sin ella (regla 9), y se puede intentar formar
 * un ciclo.
 *
 * Cada prueba usa nombres distintos, para no depender del orden.
 */

const CATEGORIAS = '/admin/catalogo/categorias';

/** Crea una categoría, opcionalmente colgando de otra. */
async function crearCategoria(page: Page, nombre: string, padre?: string) {
  await page.getByRole('button', { name: 'Nueva categoría', exact: true }).click();

  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();

  await panel.getByLabel('Nombre').fill(nombre);

  if (padre) {
    await panel.getByLabel('Cuelga de').selectOption({ label: padre });
  }

  await panel.getByRole('button', { name: 'Crear categoría' }).click();
  await expect(panel).toBeHidden();
}

/** Una fila del listado, por el nombre visible. */
function fila(page: Page, nombre: string) {
  return page.locator('tbody tr').filter({ hasText: nombre });
}

test('Una subcategoría se ve colgando de su padre, no suelta', async ({ page }) => {
  const record = themeRecorder(page, 'categorias');
  await loginAsE2eAdmin(page);
  await page.goto(CATEGORIAS);

  await crearCategoria(page, 'Papelería');
  await crearCategoria(page, 'Cuadernos', 'Papelería');

  const padre = fila(page, 'Papelería');
  const hija = fila(page, 'Cuadernos');

  await expect(padre).toBeVisible();
  await expect(hija).toBeVisible();

  // La jerarquía se ve: la hija va sangrada y con marca de rama. Una lista
  // plana no mentiría menos por ser más simple — mentiría igual.
  await expect(hija.locator('.cat-tree__mark')).toBeVisible();
  await expect(padre.locator('.cat-tree__mark')).toHaveCount(0);

  // Y va **después** de su padre, no ordenada por su cuenta.
  const filas = await page.locator('tbody tr').allInnerTexts();
  const iPadre = filas.findIndex((texto) => texto.includes('Papelería'));
  const iHija = filas.findIndex((texto) => texto.includes('Cuadernos'));
  expect(iHija).toBeGreaterThan(iPadre);

  await record('categorias-con-arbol');
});

test('Una categoría no puede colgar de su propia descendiente', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto(CATEGORIAS);

  await crearCategoria(page, 'Escritura');
  await crearCategoria(page, 'Lápices', 'Escritura');

  // Al editar la abuela, su descendiente no se ofrece como padre: elegirla
  // formaría un ciclo, y ofrecer una opción que siempre falla es enseñar una
  // puerta pintada. El servidor lo rechaza igual — es él quien manda.
  await fila(page, 'Escritura').getByRole('button', { name: 'Editar' }).click();

  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();

  const opciones = await panel.getByLabel('Cuelga de').locator('option').allInnerTexts();
  expect(opciones, 'se ofrece la descendiente como padre: sería un ciclo').not.toContain('Lápices');
  expect(opciones, 'se ofrece a sí misma como padre').not.toContain('Escritura');

  // Y se dice por qué faltan, en vez de dejar un desplegable corto sin
  // explicación.
  await expect(panel).toContainText('sería un ciclo');

  await panel.getByRole('button', { name: 'Cancelar' }).click();
});

test('Dar de baja una categoría con productos dice cuántos se quedan sin ella', async ({ page }) => {
  const record = themeRecorder(page, 'categorias');
  await loginAsE2eAdmin(page);

  // El recuento tiene que ser de verdad, así que hacen falta productos. Se
  // crean por API: esta prueba es de la pantalla de categorías, no de la de
  // productos, que todavía no existe.
  await page.goto(CATEGORIAS);
  await crearCategoria(page, 'Arte');

  const lista = await (await page.request.get('/api/admin/catalog/categories')).json();
  const arte = (lista as { id: string; name: string }[]).find((c) => c.name === 'Arte')!;

  // Las escrituras por API necesitan el token CSRF a mano: quien lo pone en
  // la aplicación es el cliente HTTP compartido, y `page.request` no pasa por
  // él. Es determinista y derivado de la sesión (ADR-012), así que pedirlo
  // una vez basta.
  const csrf = (await (await page.request.get('/api/admin/auth/csrf')).json()) as { csrfToken: string };

  // El slug va aparte del nombre y sin tildes: el formato solo admite
  // `a-z0-9-` (regla del SPEC), así que derivarlo del nombre con un
  // `toLowerCase` deja «témpera-arte» y el servidor lo rechaza — con razón.
  const PRODUCTOS = [
    { nombre: 'Témpera Arte', slug: 'tempera-arte' },
    { nombre: 'Pincel Arte', slug: 'pincel-arte' },
  ];

  for (const { nombre, slug } of PRODUCTOS) {
    const creado = await page.request.post('/api/admin/catalog/products', {
      headers: { 'X-CSRF-Token': csrf.csrfToken },
      data: {
        name: nombre,
        slug,
        shortDescription: null,
        description: null,
        primaryCategoryId: arte.id,
        categoryIds: [arte.id],
        brandId: null,
        listPrice: 10,
        saleUnit: null,
        variantLabel: null,
        code: null,
        barcode: null,
      },
    });
    expect(
      creado.ok(),
      `no se pudo crear «${nombre}»: ${creado.status()} ${await creado.text()}`,
    ).toBe(true);
  }

  await page.goto(CATEGORIAS);

  const arteFila = fila(page, 'Arte');
  await expect(arteFila).toContainText('2');

  await arteFila.getByRole('button', { name: 'Dar de baja' }).click();

  const dialogo = page.getByRole('alertdialog');
  await expect(dialogo).toBeVisible();

  // Aquí SÍ se cuenta, al revés que en marcas: la regla 9 pide el número
  // **antes** de decidir. Y dice que no hay cascada.
  await expect(dialogo).toContainText('2 productos se quedan sin esta categoría');
  await expect(dialogo).toContainText('no se desactiva ninguno');

  await expect(dialogo.getByRole('button', { name: 'Dar de baja' })).toBeVisible();
  await expect(dialogo.getByRole('button', { name: /^\s*aceptar\s*$/i })).toHaveCount(0);

  await record('categorias-aviso-con-recuento');

  await dialogo.getByRole('button', { name: 'Dar de baja' }).click();

  // Baja lógica y sin cascada: la categoría se marca, y **los productos
  // siguen activos**, que es lo que la regla 9 promete.
  await expect(arteFila).toContainText('Oculta');
  await expect(arteFila).toHaveAttribute('data-dimmed', 'true');

  const productos = await (await page.request.get('/api/admin/catalog/products')).json();
  const vivos = (productos as { items?: { name: string; isActive: boolean }[] }).items ?? [];
  for (const { nombre } of PRODUCTOS) {
    const producto = vivos.find((p) => p.name === nombre);
    expect(producto?.isActive, `«${nombre}» se desactivó en cascada`).toBe(true);
  }

  await record('categorias-con-una-oculta');
});

test('El slug repetido de una categoría se explica sin jerga', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto(CATEGORIAS);

  await crearCategoria(page, 'Oficina');

  await page.getByRole('button', { name: 'Nueva categoría', exact: true }).click();
  const panel = page.getByRole('dialog');
  await panel.getByLabel('Nombre').fill('Oficina Dos');
  await panel.getByLabel('Dirección web').fill('oficina');

  const aviso = panel.getByRole('alert');

  await duringExpectedOutage(page, async () => {
    await panel.getByRole('button', { name: 'Crear categoría' }).click();
    await expect(aviso).toBeVisible();
  });

  await expect(aviso).toContainText('Ya existe una categoría');
  await expect(aviso).not.toContainText('409');

  await panel.getByRole('button', { name: 'Cancelar' }).click();
});

test('El foco no se pierde y Escape cierra el panel sin guardar', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto(CATEGORIAS);
  await expect(page.locator('main')).toBeVisible();

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

  await page.getByRole('button', { name: 'Nueva categoría', exact: true }).click();
  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();

  await panel.getByLabel('Nombre').fill('Descartada');
  await page.keyboard.press('Escape');

  await expect(panel).toBeHidden();
  await expect(fila(page, 'Descartada')).toHaveCount(0);
});
