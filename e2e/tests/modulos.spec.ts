import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * Pantalla de módulos — criterios de aceptación de
 * `docs/modules/core/ENTREGA-04A-PANTALLAS-MODULOS-USUARIOS.md` §4, los que
 * el propio documento marca `[ ]` "requiere navegador".
 *
 * El grafo lo monta `global-setup.ts`: core (núcleo), demo_catalog y
 * demo_sales (activos), demo_crm y demo_services (inactivos, activables),
 * demo_service_orders y demo_tracking (bloqueados).
 */

test('Las cuatro variantes de tarjeta se ven correctas', async ({ page }) => {
  const record = themeRecorder(page, 'modulos');
  await loginAsE2eAdmin(page);

  await page.goto('/admin/modulos');
  await expect(page.locator('#modulo-core')).toBeVisible();

  await expect(page.locator('#modulo-core')).toContainText('Núcleo');
  await expect(page.locator('#modulo-demo_catalog')).toContainText('Activo');
  await expect(page.locator('#modulo-demo_crm')).toContainText('Inactivo');
  await expect(page.locator('#modulo-demo_tracking')).toContainText('Bloqueado');

  await record('cuatro-variantes-de-tarjeta');
});

test('Se pinta una tarjeta por cada módulo que el servidor conoce', async ({ page }) => {
  await loginAsE2eAdmin(page);

  // Lo que protege la ADR-019: que ningún módulo desaparezca en silencio. La
  // guía manual lo comprobaba leyendo «Módulos descubiertos: N» del arranque
  // y contando tarjetas a ojo; el mismo hecho se afirma comparando lo que el
  // servidor dice contra lo que el DOM pinta, sin depender del log.
  const response = await page.request.get('/api/admin/modules');
  expect(response.ok()).toBe(true);
  const modules = (await response.json()) as { code: string }[];

  await page.goto('/admin/modulos');
  await expect(page.locator('#modulo-core')).toBeVisible();

  expect(modules.length).toBeGreaterThan(1);
  await expect(page.locator('.mod-grid article.mod')).toHaveCount(modules.length);

  // Y una por una: un recuento correcto con la tarjeta equivocada seguiría
  // pasando la aserción de arriba.
  for (const module of modules) {
    await expect(
      page.locator(`#modulo-${module.code}`),
      `falta la tarjeta de '${module.code}'`,
    ).toHaveCount(1);
  }
});

test('CORE aparece sin interruptor, no con uno deshabilitado', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');

  // **El contraste va primero**, y no solo porque explique mejor: sin él, la
  // ausencia del interruptor de CORE se cumple sola en una pantalla que aún no
  // ha pintado sus tarjetas. Que otra tarjeta sí lo tenga es la prueba de que
  // hay tarjetas que mirar.
  await expect(page.locator('#modulo-demo_catalog').getByRole('switch')).toBeVisible();

  // No es "hay un interruptor, pero deshabilitado": la tarjeta de CORE no
  // monta ningún elemento con role="switch". Es una operación que no existe,
  // no una que está bloqueada.
  await expect(page.locator('#modulo-core').getByRole('switch')).toHaveCount(0);

  // Cualquier otra tarjeta sí lo tiene, para contrastar que la ausencia es
  // deliberada y no un fallo general del render.
  await expect(page.locator('#modulo-demo_catalog').getByRole('switch')).toHaveCount(1);
});

test('Una tarjeta bloqueada nombra el módulo que le falta y enlaza a él', async ({ page }) => {
  const record = themeRecorder(page, 'modulos');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');

  const blocked = page.locator('#modulo-demo_tracking');
  // "Seguimiento de Servicios (demostración)" depende de "Órdenes de
  // Servicio (demostración)", que sigue inactivo: por eso está bloqueado.
  await expect(blocked).toContainText('Falta');
  await expect(blocked.locator('.mod__jump')).toBeVisible();

  await record('tarjeta-bloqueada-antes-de-seguir-el-enlace');

  await blocked.locator('.mod__jump').click();

  // El enlace lleva a la tarjeta que falta y la resalta: no basta con que la
  // página se desplace, la tarjeta tiene que marcarse como destino.
  const target = page.locator('#modulo-demo_service_orders');
  await expect(target).toHaveClass(/is-target/);
  await expect(target).toBeInViewport();

  await record('tras-seguir-el-enlace-a-quien-bloquea');
});

test('El diálogo de confirmación dice la verdad sobre el reinicio', async ({ page }) => {
  const record = themeRecorder(page, 'modulos');
  await loginAsE2eAdmin(page);

  await test.step('instalación real: Modules:RestartAfterActivation en true', async () => {
    await page.goto('/admin/modulos');

    await page.locator('#modulo-demo_crm').getByRole('switch').click();
    const dialog = page.getByRole('alertdialog');

    await expect(dialog).toContainText('El sistema se reiniciará');
    await expect(dialog).toContainText('volverás aquí automáticamente');
    await expect(dialog).not.toContainText('Hace falta reiniciar el sistema a mano');

    await record('dialogo-cuando-la-instalacion-se-reinicia-sola');

    // Cancelar, no confirmar: esta prueba no muta el grafo. La activación de
    // verdad, con el reinicio de verdad, es la última prueba del archivo.
    await dialog.getByRole('button', { name: 'Cancelar' }).click();
    await expect(dialog).toBeHidden();
  });

  await test.step('una instalación con Modules:RestartAfterActivation en false', async () => {
    // No hay una segunda instalación real: se simula la única diferencia que
    // importa —el dato que manda en la interfaz— interceptando la respuesta
    // que ya trae ese campo. Ver ModuleResponse.RestartsAutomatically.
    await page.route('**/api/admin/modules', async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue();
        return;
      }

      const response = await route.fetch();
      const modules = (await response.json()) as { code: string; restartsAutomatically: boolean }[];
      const patched = modules.map((module) =>
        module.code === 'demo_crm' ? { ...module, restartsAutomatically: false } : module,
      );

      await route.fulfill({ response, json: patched });
    });

    await page.goto('/admin/modulos');

    await page.locator('#modulo-demo_crm').getByRole('switch').click();
    const dialog = page.getByRole('alertdialog');

    // Este es el hallazgo 1: antes de la corrección, esta frase aparecía
    // siempre, sin importar restartsAutomatically.
    await expect(dialog).toContainText('Hace falta reiniciar el sistema a mano');
    await expect(dialog).not.toContainText('volverás aquí automáticamente');

    await record('dialogo-cuando-la-instalacion-no-se-reinicia-sola');

    await dialog.getByRole('button', { name: 'Cancelar' }).click();
    await page.unroute('**/api/admin/modules');
  });
});

test('El 409 al desactivar nombra quién bloquea y no reinicia nada', async ({ page }) => {
  const record = themeRecorder(page, 'modulos');
  await loginAsE2eAdmin(page);

  // demo_catalog está activo pero demo_sales depende de él de forma dura, así
  // que el servidor ya lo sabe y la interfaz deshabilita su interruptor —
  // correctamente, es justo lo que esta prueba comprueba desde el otro lado.
  // Para forzar el intento real contra el servidor (y no solo comprobar que
  // el botón está deshabilitado, que ya prueba el resto del archivo) se
  // simula una interfaz con datos desactualizados: exactamente lo que pasaría
  // si otra pestaña activó demo_sales un segundo después de que esta cargara
  // la lista. El POST que sigue es real, contra el servidor real.
  await page.route('**/api/admin/modules', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.continue();
      return;
    }

    const response = await route.fetch();
    const modules = (await response.json()) as { code: string; canDeactivate: boolean }[];
    const patched = modules.map((module) =>
      module.code === 'demo_catalog' ? { ...module, canDeactivate: true } : module,
    );

    await route.fulfill({ response, json: patched });
  });

  await page.goto('/admin/modulos');

  await page.locator('#modulo-demo_catalog').getByRole('switch').click();

  // El 409 es la respuesta correcta y esperada, no un fallo: es justo lo que
  // esta prueba provoca a propósito. Como con el reinicio real, el navegador
  // igual lo anuncia en consola por su cuenta (ver duringExpectedOutage).
  const conflict = page.getByRole('status').filter({ hasText: 'Ventas Online (demostración)' });
  await duringExpectedOutage(page, async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: /^Desactivar/ }).click();

    // tone="warning" en ConflictNotice, no "danger": su role es "status", no
    // "alert" (ver Alert en shared/ui/index.tsx). Es un aviso que nombra el
    // obstáculo, no un fallo del sistema. Nombra a "Ventas Online
    // (demostración)" —el nombre visible de demo_sales—, no su código: es lo
    // que la interfaz compone a partir de blockedBy.
    await expect(conflict).toBeVisible();
  });

  await expect(conflict).toContainText('no se puede desactivar');
  await expect(conflict).toContainText('Ventas Online (demostración)');

  // La frase es para quien administra su negocio: nombra el obstáculo, no el
  // código de estado. Que no diga «409» ni «Conflict» es parte de la regla de
  // CLAUDE.md sobre los mensajes del API como texto de interfaz.
  const conflictText = await conflict.innerText();
  expect(conflictText).not.toMatch(/\b409\b/);
  expect(conflictText.toLowerCase()).not.toContain('conflict');

  await record('409-al-desactivar-nombra-a-quien-bloquea');

  // No reinicia nada: nunca aparece el estado de reconexión, y la tarjeta
  // sigue mostrando el módulo activo, no a medio camino.
  // Primero lo que sí tiene que estar: la tarjeta sigue diciendo que el módulo
  // está activo. Sin eso, «no apareció el estado de reconexión» se cumple sola
  // mientras la pantalla no haya pintado nada.
  await expect(page.locator('#modulo-demo_catalog')).toContainText('Activo');
  await expect(page.getByRole('alertdialog', { name: 'Aplicando el cambio' })).toHaveCount(0);

  await page.unroute('**/api/admin/modules');
});

test('Activar lleva al estado de reconexión y vuelve solo, con la sesión abierta', async ({ page }) => {
  // **Espera un reinicio de verdad del proceso**, no un mock: activar un
  // módulo detiene el host a propósito y Docker lo relanza. Los 90 segundos
  // de antes bastaban con una suite más corta; con la suite entera —donde el
  // reinicio compite con lo demás y hay más datos que cargar al arrancar— se
  // pasó una vez. Sola sigue tardando poco más de un minuto.
  //
  // Se sube el margen, no se relaja la afirmación: lo que se comprueba sigue
  // siendo que vuelve **solo** y con la sesión abierta.
  test.setTimeout(180_000);

  const record = themeRecorder(page, 'modulos');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');

  await page.locator('#modulo-demo_services').getByRole('switch').click();

  // Desde aquí hasta que el overlay se oculta, el contenedor real está
  // cayendo y volviendo a subir: el sondeo de reconexión falla a propósito
  // varias veces mientras tanto (ver duringExpectedOutage), así que es la
  // única porción de este archivo con la vigilancia de consola en pausa.
  await duringExpectedOutage(page, async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: /^Activar/ }).click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toContainText('Tu sesión sigue abierta');

    await record('reconectando-tras-activar-de-verdad');

    // El contenedor real se reinicia solo (restart: unless-stopped) y la
    // app recarga la página entera al recuperar la conexión
    // (connection.onRecovered en app/App.tsx). Es la única prueba de este
    // archivo que depende de eso, y por eso necesita más tiempo.
    await expect(overlay).toBeHidden({ timeout: 60_000 });

    // Vuelve sola a la pantalla de módulos, con la sesión abierta: sin pasar
    // por /login otra vez.
    await expect(page).toHaveURL(/\/admin\/modulos$/);
  });

  // Ya de vuelta y estable: la vigilancia de consola vuelve a exigir cero.
  await expect(page.locator('#modulo-demo_services')).toContainText('Activo');

  // El menú sigue en pie tras el reinicio. NO se afirma que aparezcan las
  // entradas del módulo recién activado, que es lo que pedía el punto 5 de
  // VERIFICACION-VISUAL-CORE.md: los módulos de demostración son solo de
  // backend y no aportan navegación —`layout/navigation.ts:42` monta
  // únicamente `coreNavigation`—, así que hoy no hay nada que pudiera
  // aparecer. Se afirmará con M01, que será el primer módulo con interfaz.
  await expect(page.getByRole('navigation')).toContainText('Módulos');

  await record('de-vuelta-tras-el-reinicio-con-la-sesion-abierta');
});
