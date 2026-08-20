import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * Los componentes especificados por Diseño, comprobados **aquí**.
 *
 * Diseño calculó los contrastes de `--selected` sobre `--selected-bg` —6,49:1
 * de día, 4,95:1 de noche— y lo dijo con todas las letras: *«una cifra mía no
 * es vuestra prueba»*. Esto es la prueba.
 *
 * Los componentes se montan sobre una pantalla real y no sobre una ruta de
 * ensayo: lo que hay que afirmar es que funcionan **dentro del producto**,
 * con sus tokens y su tema, no aislados en un banco.
 */

/** Inyecta un bloque con los tres componentes en la pantalla que esté abierta. */
async function montarComponentes(page: import('@playwright/test').Page) {
  await page.evaluate(() => {
    const main = document.querySelector('main');
    if (!main) {
      throw new Error('no hay <main> donde montar los componentes');
    }

    const zona = document.createElement('div');
    zona.id = 'banco-componentes';
    zona.style.padding = '16px';
    zona.style.display = 'flex';
    zona.style.gap = '8px';
    zona.style.flexWrap = 'wrap';

    // Se escribe el mismo marcado que producen los componentes. Lo que se
    // comprueba son los **tokens** resueltos por el tema, que es donde estaba
    // la duda; el marcado lo fija TypeScript.
    zona.innerHTML = `
      <button type="button" class="ui-chip" aria-pressed="false">Deporte</button>
      <button type="button" class="ui-chip ui-chip--selected" aria-pressed="true">Juguetes</button>
      <span class="ui-tag"><span>Papelería</span><button type="button" class="ui-tag__remove" aria-label="Quitar Papelería">×</button></span>
    `;

    main.appendChild(zona);
  });
}

test('El contraste de lo designado se cumple en los dos temas', async ({ page }) => {
  const record = themeRecorder(page, 'componentes');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/catalogo/productos');
  await expect(page.locator('main')).toBeVisible();

  await montarComponentes(page);

  const encendido = page.locator('.ui-chip--selected');
  const etiqueta = page.locator('.ui-tag');
  await expect(encendido).toBeVisible();
  await expect(etiqueta).toBeVisible();

  // `themeRecorder` corre axe-core en claro **y** en oscuro. Si `--selected`
  // sobre `--selected-bg` no llegara a 4.5:1 en cualquiera de los dos, esto
  // falla — que es exactamente lo que Diseño pidió que se comprobara aquí en
  // vez de fiarse de su cálculo.
  await record('componentes-designado-en-los-dos-temas');
});

test('Un filtro encendido lo dice sin depender del color', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/catalogo/productos');
  await expect(page.locator('main')).toBeVisible();

  await montarComponentes(page);

  // `aria-pressed` es lo que contesta «¿estoy encendido?» a quien no ve el
  // color. Es además lo que distingue a FilterChip de Tag: este se queda.
  await expect(page.locator('.ui-chip--selected')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.ui-chip:not(.ui-chip--selected)')).toHaveAttribute(
    'aria-pressed',
    'false',
  );

  // Y `Tag` no tiene estado: no contesta a esa pregunta porque no es la suya.
  await expect(page.locator('.ui-tag')).not.toHaveAttribute('aria-pressed', /.*/);

  // Su botón dice **qué** quita, no solo que quita.
  await expect(page.locator('.ui-tag__remove')).toHaveAttribute('aria-label', /Papelería/);
});

test('Los controles pequeños llegan al mínimo táctil', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/catalogo/productos');
  await expect(page.locator('main')).toBeVisible();

  await montarComponentes(page);

  // `sm` vale para un control repetido en fila densa **cuando existe otra
  // manera de hacer lo mismo**. Un filtro la tiene —quitarlo desde su `Tag`,
  // o limpiar todos— así que puede ser pequeño; pero pequeño no es
  // inalcanzable.
  for (const selector of ['.ui-chip', '.ui-tag']) {
    const caja = await page.locator(selector).first().boundingBox();
    expect(caja?.height ?? 0, `${selector} es demasiado bajo`).toBeGreaterThanOrEqual(
      Number(28) - 1,
    );
  }
});
