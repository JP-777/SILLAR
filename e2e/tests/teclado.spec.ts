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
  // Desde H29 existen controles móviles en el DOM también en escritorio,
  // pero `display:none` los saca correctamente del recorrido de teclado.
  // Contarlos como si fueran tabbables hacía pulsar Tab una vez de más y
  // confundía el paso del foco a la barra del navegador con una pérdida.
  const cuantos = await page.locator(ENFOCABLES).evaluateAll((elements) =>
    elements.filter((element) => {
      const style = getComputedStyle(element);
      return (
        style.display !== 'none' &&
        style.visibility !== 'hidden' &&
        element.getClientRects().length > 0
      );
    }).length,
  );
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

/**
 * **H17: un aviso que caduca no mueve el foco del panel ni del diálogo.**
 *
 * `useToasts` quita cada aviso a los 4 s con un `setState` en la página, y eso
 * vuelve a pintarla sin que nadie toque nada. El trap se rearmaba con cada
 * pintado —dependía de `onEscape`, que las páginas pasan como flecha en
 * línea— y mandaba el foco al primer control: «Cerrar» en el panel, donde el
 * siguiente espacio lo cerraba y tiraba lo escrito, y «Cancelar» en la
 * confirmación, donde el Enter cancelaba en vez de dar de baja.
 *
 * Las pruebas de arriba no podían verlo: abren el diálogo y pulsan Tab sin
 * que nada vuelva a pintar la página entremedias. Por eso aquí se exige que
 * el aviso siga **vivo** con el foco ya dentro, antes de dejarlo caducar: si
 * llegara caducado, la prueba pasaría sin haber provocado nada.
 */
test('Un aviso que caduca no mueve el foco del panel ni del diálogo abiertos', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/catalogo/marcas');

  const sufijo = Date.now().toString(36);
  const primera = `Foco primera ${sufijo}`;
  const segunda = `Foco segunda ${sufijo}`;
  const nueva = page.getByRole('button', { name: 'Nueva marca', exact: true });
  const fila = (nombre: string) => page.locator('tbody tr').filter({ hasText: nombre });

  async function crear(nombre: string) {
    await nueva.click();
    const alta = page.getByRole('dialog');
    await alta.getByLabel('Nombre').fill(nombre);
    await alta.getByRole('button', { name: 'Crear marca' }).click();
    await expect(alta).toBeHidden();
    await expect(fila(nombre)).toContainText('Visible');
  }

  // --- 1 · Escribiendo en el panel mientras caduca el aviso de alta --------
  await crear(primera);
  const avisoAlta = page.getByRole('status').filter({ hasText: 'Se creó la marca' });

  await nueva.click();
  const panel = page.getByRole('dialog');
  await expect(panel).toBeVisible();
  const nombre = panel.getByLabel('Nombre');
  await nombre.click();
  await page.keyboard.type('Marca que');

  await expect(avisoAlta, 'el aviso tiene que seguir vivo con el foco dentro, o esto no provoca nada').toBeVisible();
  await expect(avisoAlta).toBeHidden({ timeout: 8_000 });

  await expect(nombre, 'al caducar el aviso el foco salió del campo').toBeFocused();
  // Con el defecto, el foco estaba en «Cerrar» y este espacio cerraba el panel.
  await page.keyboard.type(' no se pierde');
  await expect(panel, 'el panel se cerró solo y se llevó lo escrito').toBeVisible();
  await expect(nombre).toBeFocused();
  await expect(nombre).toHaveValue('Marca que no se pierde');

  // Tab sigue atrapado, y volver atrás devuelve al mismo campo.
  await page.keyboard.press('Tab');
  expect(await page.evaluate(() => document.querySelector('[role="dialog"]')?.contains(document.activeElement) ?? false),
    'el Tab salió del panel').toBe(true);
  await page.keyboard.press('Shift+Tab');
  await expect(nombre).toBeFocused();

  // Escape cierra sin guardar y devuelve el foco a quien abrió.
  await page.keyboard.press('Escape');
  await expect(panel).toBeHidden();
  await expect(nueva).toBeFocused();
  await expect(fila('Marca que no se pierde')).toHaveCount(0);

  // --- 2 · El foco en la acción destructiva mientras caduca el aviso de baja
  await crear(segunda);

  await fila(primera).getByRole('button', { name: 'Dar de baja' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await expect(fila(primera)).toContainText('Oculta');
  const avisoBaja = page.getByRole('status').filter({ hasText: `«${primera}» ya no aparece en la web.` });
  await expect(avisoBaja).toBeVisible();

  const abrir = fila(segunda).getByRole('button', { name: 'Dar de baja' });
  await abrir.click();
  const dialogo = page.getByRole('alertdialog');
  await expect(dialogo).toBeVisible();
  const destructiva = dialogo.getByRole('button', { name: 'Dar de baja' });
  const cancelar = dialogo.getByRole('button', { name: 'Cancelar' });

  await page.keyboard.press('Tab'); // de «Cancelar» a «Dar de baja»
  await expect(destructiva).toBeFocused();

  await expect(avisoBaja, 'el aviso tiene que seguir vivo con el foco dentro, o esto no provoca nada').toBeVisible();
  await expect(avisoBaja).toBeHidden({ timeout: 8_000 });

  // Con el defecto, aquí el foco estaba en «Cancelar».
  await expect(destructiva, 'al caducar el aviso el foco dejó la acción elegida').toBeFocused();

  await page.keyboard.press('Tab');
  await expect(cancelar).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(destructiva).toBeFocused();

  // Escape no ejecuta la baja y devuelve el foco a la fila.
  await page.keyboard.press('Escape');
  await expect(dialogo).toBeHidden();
  await expect(abrir).toBeFocused();
  await expect(fila(segunda)).toContainText('Visible');

  // Se deja todo oculto: las marcas de esta prueba no deben asomar en la tienda
  // que recorren las pruebas que vienen después.
  await abrir.click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await expect(fila(segunda)).toContainText('Oculta');
});
