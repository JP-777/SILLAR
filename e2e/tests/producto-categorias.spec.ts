import type { APIRequestContext, Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * M01 · Asignación de categorías — `ENTREGA-04D-VARIANTES-CATEGORIAS.md` §1.
 *
 * El control N:M y su principal: un producto está en varias categorías y solo
 * una da la miga de pan (regla 6).
 */

const PRODUCTOS = '/admin/catalogo/productos';

/** Deja creadas las dos categorías del cono. Idempotente. */
async function categorías(api: APIRequestContext) {
  const csrf = (await (await api.get('/api/admin/auth/csrf')).json()) as { csrfToken: string };

  async function crear(name: string, slug: string) {
    const r = await api.post('/api/admin/catalog/categories', {
      headers: { 'X-CSRF-Token': csrf.csrfToken },
      data: { name, slug, parentId: null, description: null, imageId: null, sortOrder: 0 },
    });

    if (r.status() === 409) {
      const todas = (await (await api.get('/api/admin/catalog/categories')).json()) as {
        id: string;
        name: string;
        slug: string;
      }[];
      const encontrada = todas.find((c) => c.slug === slug);
      expect(encontrada, `«${name}» dio 409 y no aparece en el listado`).toBeTruthy();
      return encontrada!;
    }

    expect(r.ok(), `categoría «${name}»: ${r.status()} ${await r.text()}`).toBe(true);
    return (await r.json()) as { id: string; name: string; slug: string };
  }

  return {
    deporte: await crear('Deporte asignacion', 'deporte-asignacion'),
    juguetes: await crear('Juguetes asignacion', 'juguetes-asignacion'),
  };
}

/** Crea un producto por pantalla y devuelve su ficha abierta. */
async function abrirFicha(page: Page, nombre: string) {
  await page.goto(PRODUCTOS);
  await page.getByRole('button', { name: 'Nuevo producto', exact: true }).click();

  const panel = page.getByRole('dialog');
  await panel.getByLabel(/^Nombre/).fill(nombre);
  await panel.getByRole('button', { name: 'Crear producto' }).click();
  await expect(panel).toBeHidden();

  await page.getByLabel('Buscar').fill(nombre);
  const fila = page.locator('tbody tr').filter({ hasText: nombre });
  await expect(fila).toBeVisible();
  await fila.getByRole('button', { name: 'Editar' }).click();

  const ficha = page.getByRole('dialog');
  await expect(ficha).toBeVisible();
  return ficha;
}

test('Con una sola categoría no hay nada que elegir, y se dice por qué', async ({ page }) => {
  const record = themeRecorder(page, 'producto-categorias');
  await loginAsE2eAdmin(page);
  const { deporte } = await categorías(page.request);

  const ficha = await abrirFicha(page, 'Pelota una categoria');

  await ficha.getByRole('checkbox', { name: deporte.name }).check();

  // Una frase, no un botón de radio solitario: elegir entre una opción no es
  // elegir.
  await expect(ficha.getByText(`«${deporte.name}» es la principal, porque es la única.`)).toBeVisible();
  // Se afirma sobre los propios botones: `radiogroup` no es un rol que un
  // `fieldset` tome solo, así que contarlo daba cero hubiera radios o no.
  await expect(ficha.getByRole('radio')).toHaveCount(0);

  await record('producto-categorias-una-sola');
});

test('Dos categorías: se elige la principal, y la elección sobrevive al guardado', async ({
  page,
}) => {
  const record = themeRecorder(page, 'producto-categorias');
  await loginAsE2eAdmin(page);
  const { deporte, juguetes } = await categorías(page.request);

  const ficha = await abrirFicha(page, 'Cono asignacion');

  await ficha.getByRole('checkbox', { name: deporte.name }).check();
  await ficha.getByRole('checkbox', { name: juguetes.name }).check();

  // La primera marcada es la principal: un producto con categorías y sin
  // principal no tiene miga de pan.
  const principalDeporte = ficha.getByRole('radio', { name: deporte.name });
  await expect(principalDeporte).toBeChecked();

  // El estado con radios es el que estrena marcado: axe lo mira aquí, en los
  // dos temas.
  await record('producto-categorias-principal');

  // Se cambia a mano, que es la mitad que importa del control.
  await ficha.getByRole('radio', { name: juguetes.name }).check();
  await ficha.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(ficha).toBeHidden();

  // **Se vuelve a abrir la ficha**: lo que se afirma es que se guardó, no que
  // la pantalla siga enseñando lo que se acaba de teclear.
  await page.getByLabel('Buscar').fill('Cono asignacion');
  await page
    .locator('tbody tr')
    .filter({ hasText: 'Cono asignacion' })
    .getByRole('button', { name: 'Editar' })
    .click();

  const vuelta = page.getByRole('dialog');
  await expect(vuelta.getByRole('checkbox', { name: deporte.name })).toBeChecked();
  await expect(vuelta.getByRole('radio', { name: juguetes.name })).toBeChecked();
});

test('Quitar la principal promueve otra y lo dice con palabras', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const { deporte, juguetes } = await categorías(page.request);

  const ficha = await abrirFicha(page, 'Aro promocion');

  await ficha.getByRole('checkbox', { name: deporte.name }).check();
  await ficha.getByRole('checkbox', { name: juguetes.name }).check();
  await expect(ficha.getByRole('radio', { name: deporte.name })).toBeChecked();

  // Se quita la que era principal.
  await ficha.getByRole('checkbox', { name: deporte.name }).uncheck();

  // **No se cambia en silencio**: cambiar la principal cambia la dirección de
  // la miga de pan, y quien lo hizo sin querer tiene que enterarse.
  await expect(
    ficha.getByRole('status').filter({ hasText: `Ahora la principal es «${juguetes.name}»` }),
  ).toBeVisible();

  // Y de verdad quedó esa: el aviso y el estado dicen lo mismo.
  await expect(ficha.getByText(`«${juguetes.name}» es la principal, porque es la única.`)).toBeVisible();
});
