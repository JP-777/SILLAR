import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * Recorrido con teclado — `VERIFICACION-VISUAL-CORE.md` §5.
 *
 * La interfaz tiene que ser navegable sin ratón, y quien la recorre así
 * necesita saber en todo momento dónde está. Las tres afirmaciones son sobre
 * el DOM: dónde cae el foco, si sale del diálogo, y si `Escape` ejecuta algo.
 */

const ENFOCABLES = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(', ');

/** Qué tiene el foco ahora mismo, en forma legible para un mensaje de fallo. */
async function focoActual(page: import('@playwright/test').Page): Promise<string> {
  return page.evaluate(() => {
    const el = document.activeElement;
    if (!el || el === document.body) {
      return '(ninguno — el foco se perdió en el body)';
    }
    const label = el.getAttribute('aria-label') ?? el.textContent?.trim().slice(0, 40) ?? '';
    return `${el.tagName.toLowerCase()}${label ? ` «${label}»` : ''}`;
  });
}

/**
 * Recorre la pantalla con Tab y comprueba que el foco nunca se pierde.
 *
 * El número de saltos se calcula, no se fija: al llegar al final del
 * documento el navegador pasa el foco a **su propia barra**, y ahí
 * `document.activeElement` vuelve legítimamente al `body`. Un bucle de N
 * saltos fijos daría un fallo falso en cuanto la pantalla tuviera menos de N
 * elementos enfocables — que es justo lo que pasó al escribir esta prueba.
 */
async function recorrerConTab(page: import('@playwright/test').Page, pantalla: string) {
  const cuantos = await page.locator(ENFOCABLES).count();
  expect(cuantos, `«${pantalla}» no tiene nada enfocable`).toBeGreaterThan(3);

  for (let salto = 1; salto < cuantos; salto += 1) {
    await page.keyboard.press('Tab');
    const foco = await focoActual(page);
    expect(
      foco,
      `en «${pantalla}», tras ${salto} de ${cuantos - 1} pulsaciones de Tab el foco se perdió`,
    ).not.toContain('ninguno');
  }
}

test('El foco nunca se pierde al recorrer Módulos con Tab', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');
  await expect(page.locator('#modulo-core')).toBeVisible();

  await recorrerConTab(page, 'Módulos');
});

test('El foco nunca se pierde al recorrer Usuarios con Tab', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/usuarios');
  await expect(page.locator('main')).toBeVisible();

  await recorrerConTab(page, 'Usuarios');
});

test('El Tab no se escapa del diálogo abierto', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');

  await page.locator('#modulo-demo_crm').getByRole('switch').click();
  const dialogo = page.getByRole('alertdialog');
  await expect(dialogo).toBeVisible();

  // Más saltos que elementos enfocables tiene el diálogo: si no atrapara el
  // foco, alguno acabaría en la página de detrás.
  for (let salto = 1; salto <= 12; salto += 1) {
    await page.keyboard.press('Tab');

    const dentro = await page.evaluate(() => {
      const dialogo = document.querySelector('[role="alertdialog"]');
      return dialogo?.contains(document.activeElement) ?? false;
    });

    expect(dentro, `tras ${salto} pulsaciones el foco salió del diálogo`).toBe(true);
  }

  await page.keyboard.press('Escape');
});

test('Escape cierra el diálogo sin ejecutar la acción', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');

  // demo_crm está inactivo. Si Escape ejecutara la acción, se activaría — y
  // además reiniciaría el host, que es la forma más cara de descubrirlo.
  await expect(page.locator('#modulo-demo_crm')).toContainText('Inactivo');

  await page.locator('#modulo-demo_crm').getByRole('switch').click();
  const dialogo = page.getByRole('alertdialog');
  await expect(dialogo).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(dialogo).toBeHidden();

  // Lo que de verdad prueba la regla: sigue inactivo, y no hay reinicio.
  await expect(page.locator('#modulo-demo_crm')).toContainText('Inactivo');
  await expect(page.getByRole('alertdialog', { name: 'Aplicando el cambio' })).toHaveCount(0);
});

test('El anillo de foco se ve al abrir el diálogo con el ratón', async ({ page }) => {
  const record = themeRecorder(page, 'teclado');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');

  // El caso que no se resuelve leyendo código: `:focus-visible` es una
  // decisión del navegador, y con clic de ratón suele NO pintar el anillo.
  // Aquí el foco no se queda en lo que se clicó —el interruptor— sino que
  // salta al panel del diálogo, y esa es justamente la situación donde el
  // navegador puede decidir que no hay que pintarlo.
  await page.locator('#modulo-demo_crm').getByRole('switch').click();
  const dialogo = page.getByRole('alertdialog');
  await expect(dialogo).toBeVisible();

  // La captura es el entregable: esto se mira una vez, no es una regresión
  // que vigilar para siempre. Queda en la galería, en los dos temas.
  await record('foco-tras-abrir-el-dialogo-con-raton');

  // Lo que sí se puede afirmar sin juicio humano: el foco entró al diálogo.
  // Que el anillo se pinte o no se decide mirando la captura de arriba.
  const dentro = await page.evaluate(() => {
    const dialogo = document.querySelector('[role="alertdialog"]');
    return dialogo?.contains(document.activeElement) ?? false;
  });
  expect(dentro, 'el foco no entró al diálogo al abrirlo con el ratón').toBe(true);

  await page.keyboard.press('Escape');
});
