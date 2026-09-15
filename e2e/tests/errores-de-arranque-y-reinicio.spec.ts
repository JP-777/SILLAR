import { expect, test, type Page, type Route } from '@playwright/test';

/**
 * H03, H08 y H22: cómo cuenta la interfaz un fallo cuya causa no conoce.
 *
 * **Estas pruebas no fijan ninguna redacción.** Exigen propiedades: que dos
 * situaciones distintas no se presenten igual, que no se afirme una causa que
 * no se sabe, y que reintentar funcione en cuanto el servidor vuelve. La copia
 * final está sin decidir, y aquí no se decide.
 *
 * Todo `/api` se intercepta en el navegador: ninguna petición sale hacia un
 * backend. Así el comportamiento que se mide es el del frontend, no el de un
 * servidor concreto que esté o no en pie.
 */

const json = (status: number, body: unknown) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

const CAPACIDADES = { modules: [{ code: 'core' }], routes: [], menu: [], home: [], footer: [] };
const INSTALADA = { businessName: 'Negocio de prueba', adminUserId: 1, email: 'instala@ejemplo.test' };

/** El 503 real del instalador (H-27) cuando el destino no es seguro, con su forma. */
const NEGATIVA_DEL_INSTALADOR = {
  type: 'about:blank',
  status: 503,
  title: 'No se instala en la base sillar_dev (localhost:5432): tiene cosas que no son de SILLAR.',
  detail: 'No se ha aplicado ninguna migración ni se ha modificado nada.',
};

/** Nada sale: todo `/api` que la prueba no intercepte explícitamente responde 404. */
async function sinBackend(page: Page) {
  await page.route('**/api/**', (route) => route.fulfill({ status: 404, body: '' }));
}

async function enviarInstalacion(page: Page) {
  const form = page.locator('form');
  await form.getByLabel('Nombre del negocio').fill('Negocio de prueba');
  await form.getByLabel('Nombre completo').fill('Persona Que Instala');
  await form.getByLabel('Correo').fill('instala@ejemplo.test');
  await form.getByLabel('Contraseña').fill('Q7!CobreLuna_4829x');
  await form.locator('button[type="submit"]').click();
}

async function tituloDelAviso(page: Page, setupPost: (route: Route) => Promise<void>) {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) => route.fulfill(json(200, { setupRequired: true })));
  await page.route('**/api/setup', (route) =>
    route.request().method() === 'POST' ? setupPost(route) : route.fallback());
  await page.goto('/');
  await enviarInstalacion(page);

  const aviso = page.getByRole('alert').first();
  await expect(aviso).toBeVisible();
  return (await aviso.innerText()).split('\n')[0];
}

// ==========================================================================
// H03 — un fallo del POST no siempre es un fallo del instalador.
// ==========================================================================

test('H03: un 503 sin respuesta del servidor no se presenta igual que una negativa del instalador', async ({ browser }) => {
  const conProblema = await tituloDelAviso(await browser.newPage(), (route) =>
    route.fulfill({ ...json(503, NEGATIVA_DEL_INSTALADOR), contentType: 'application/problem+json' }));
  const sinCuerpo = await tituloDelAviso(await browser.newPage(), (route) => route.fulfill({ status: 503, body: '' }));

  // El primero es el instalador negándose, con su motivo. El segundo es que el
  // servidor no estaba disponible, y nadie sabe por qué. Enseñarlos bajo el
  // mismo título atribuye el segundo al instalador.
  expect(sinCuerpo).not.toBe(conProblema);
});

test('H03: tras un corte de red en modo instalación, reintentar vuelve a enviar la instalación', async ({ page }) => {
  let caida = true;
  let envios = 0;

  await sinBackend(page);
  await page.route('**/api/setup/status', (route) =>
    caida ? route.abort('connectionrefused') : route.fulfill(json(200, { setupRequired: true })));
  await page.route('**/api/setup', (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    envios += 1;
    return caida ? route.abort('connectionrefused') : route.fulfill(json(201, INSTALADA));
  });

  // El primer arranque ve el asistente; después el servidor se cae un momento.
  caida = false;
  await page.goto('/');
  caida = true;
  await enviarInstalacion(page);
  await expect(page.getByRole('alert').first()).toBeVisible();

  // Vuelve, en modo instalación: /api/capabilities no existe (404 por defecto).
  caida = false;
  await page.waitForTimeout(4000);
  await page.locator('form button[type="submit"]').click();

  await expect.poll(() => envios, {
    message: 'el segundo POST no llegó a salir: el cliente sigue esperando a /api/capabilities',
    timeout: 10_000,
  }).toBe(2);
});

// ==========================================================================
// H08 — el reinicio planificado tras instalar no es un fallo.
// ==========================================================================

async function servidorQueVuelve(page: Page, modo: 'normal' | 'instalacion') {
  let caida = true;

  await sinBackend(page);
  await page.route('**/api/setup/status', (route) =>
    caida ? route.abort('connectionrefused')
      : route.fulfill(json(200, { setupRequired: modo === 'instalacion' })));
  await page.route('**/api/capabilities', (route) =>
    caida ? route.abort('connectionrefused')
      : modo === 'normal' ? route.fulfill(json(200, CAPACIDADES)) : route.fulfill({ status: 404, body: '' }));
  await page.route('**/api/admin/auth/me', (route) =>
    caida ? route.abort('connectionrefused') : route.fulfill(json(200, null)));

  return () => { caida = false; };
}

test('H08: tras el reinicio, el primer Reintentar ya alcanza al servidor en cuanto ha vuelto', async ({ page }) => {
  const levantar = await servidorQueVuelve(page, 'normal');

  // Recarga en mitad del reinicio.
  await page.goto('/login');
  await expect(page.getByRole('button', { name: 'Reintentar' })).toBeVisible();

  // Se pulsa en cuanto vuelve, sin dar tiempo a que el sondeo se recupere por
  // su cuenta. La primera versión esperaba 1,5 s y a veces pasaba: el sondeo
  // llegaba antes que el clic. Una prueba que depende de esa carrera no afirma
  // nada, y la propiedad es justo la contraria: que no haga falta esperar.
  levantar();
  await page.waitForTimeout(200);
  await page.getByRole('button', { name: 'Reintentar' }).click();

  await expect(page.getByRole('heading', { name: 'Acceso al panel' })).toBeVisible({ timeout: 5000 });
});

test('H08: si el servidor vuelve todavía en modo instalación, Reintentar lo alcanza', async ({ page }) => {
  const levantar = await servidorQueVuelve(page, 'instalacion');

  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Reintentar' })).toBeVisible();

  levantar();
  await page.waitForTimeout(4000);
  await page.getByRole('button', { name: 'Reintentar' }).click();

  await expect(page.getByRole('heading', { name: 'Instalación' })).toBeVisible({ timeout: 5000 });
});

// ==========================================================================
// H22 — la página de error no afirma una causa que no conoce.
// ==========================================================================

test('H22: un fallo de /api/setup/status no se atribuye a los módulos', async ({ page }) => {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) =>
    route.fulfill(json(500, { title: 'An error occurred while processing your request.', status: 500 })));

  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Reintentar' })).toBeVisible();

  // Falló la primera pregunta del arranque, no la de los módulos.
  await expect(page.locator('body')).not.toContainText('qué módulos están activos');
});

test('H22: un corte de red no aconseja revisar un módulo recién activado si nadie activó ninguno', async ({ page }) => {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) => route.abort('connectionrefused'));

  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Reintentar' })).toBeVisible();

  await expect(page.locator('body')).not.toContainText('Si acabas de activar un módulo');
});

test('H22 (contraste): si fallan las capacidades, se ve una página de error con Reintentar, no una pantalla vacía', async ({ page }) => {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) => route.fulfill(json(200, { setupRequired: false })));
  await page.route('**/api/capabilities', (route) =>
    route.fulfill(json(500, { title: 'An error occurred while processing your request.', status: 500 })));

  await page.goto('/');

  await expect(page.getByRole('button', { name: 'Reintentar' })).toBeVisible();
});
