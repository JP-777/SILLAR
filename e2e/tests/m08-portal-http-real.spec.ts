/**
 * [M08-HTTP] Prueba REAL por HTTP, contra el host y la DB efímera del arnés.
 * Esta PRUEBA DE HUMO no sustituye el futuro ciclo [M08-CICLO], ni demuestra
 * todavía privacidad A/B/NULL con órdenes sembradas o sabotaje VRV.
 */
import { randomUUID } from 'node:crypto';
import type { Page } from '@playwright/test';
import { expect, test } from '../fixtures/base.js';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { capacidadesActivas, cambiarModulo } from '../fixtures/grafoDeModulos.js';

const PASSWORD = 'bosque-cobalto-dieciseis-lunas';
const NOT_OWN = 'S-2026-INEXISTENTE';

type PortalOverview = {
  profile: { fullName: string; email: string; phone: string | null };
  orders: { state: string; items: unknown[] };
  work: { state: string; items: unknown[] };
};

async function registerAndLogin(page: Page, email: string, fullName: string): Promise<void> {
  // HTTP real de Playwright: el APIRequestContext del Page comparte cookies
  // con el navegador. No navegamos a '/' entre reinicios del host: una
  // navegación de React puede destruir el contexto de page.evaluate().
  // Sec-Fetch-Site emula la petición same-origin que emite el navegador.
  const headers = { 'Sec-Fetch-Site': 'same-origin' };
  const registration = await page.request.post('/api/customer/auth/register', {
    headers,
    data: { fullName, email, password: PASSWORD, phone: null },
  });
  const registrationBody = await registration.text();
  expect(registration.status(), `registro ${registrationBody}`).toBe(200);

  const login = await page.request.post('/api/customer/auth/login', {
    headers,
    data: { email, password: PASSWORD },
  });
  const loginBody = await login.text();
  expect(login.status(), `login ${loginBody}`).toBe(200);

  const cookies = await page.context().cookies();
  expect(cookies.some(c => c.name === 'sillar_tienda')).toBe(true);
}

async function overview(page: Page): Promise<{ response: PortalOverview; raw: string }> {
  const r = await page.request.get('/api/portal/overview');
  const raw = await r.text();
  expect(r.status(), `GET overview: ${raw}`).toBe(200);
  const response = JSON.parse(raw) as PortalOverview;
  // Comprobación HTTP de ausencia de miembros prohibidos, incluso cuando no hay filas.
  for (const key of [
    'customerId', 'receivedNotes', 'trackingNotes', 'boardPriority',
    'internalDueAt', 'performedBy', 'adminUserId', 'serviceOrderId',
  ]) {
    expect(raw.toLowerCase(), `Campo sensible en HTTP: ${key}`).not.toContain(key.toLowerCase());
  }
  return { response, raw };
}

test('[M08-HTTP] activación, sesiones A/B, autorización, providers, desactivación', async ({ page, browser }) => {
  test.setTimeout(12 * 60_000);
  const activeAtStart = await capacidadesActivas(page);
  expect(activeAtStart).toContain('crm');
  expect(activeAtStart).toContain('catalog');
  expect(activeAtStart).not.toContain('portal');
  expect(activeAtStart).not.toContain('sales');
  expect(activeAtStart).not.toContain('tracking');

  // Cuando M08 está OFF, no hay endpoints expuestos.
  expect((await page.request.get('/api/portal/overview')).status()).toBe(404);
  await loginAsE2eAdmin(page);
  await cambiarModulo(page, 'portal', 'Activar');
  expect(await capacidadesActivas(page)).toContain('portal');

  // Una cookie de administrador NO sirve como credencial de cliente.
  expect((await page.request.get('/api/portal/overview')).status()).toBe(401);
  expect((await page.request.get(`/api/portal/work/${NOT_OWN}`)).status()).toBe(401);

  const emailA = `m08-a-${randomUUID()}@sillar.test`;
  const emailB = `m08-b-${randomUUID()}@sillar.test`;
  await registerAndLogin(page, emailA, 'Cliente Portal A');
  const a0 = (await overview(page)).response;
  expect(a0.profile.email).toBe(emailA);
  expect(a0.orders.state).toBe('unavailable');
  expect(a0.work.state).toBe('unavailable');
  expect(a0.orders.items).toEqual([]);
  expect(a0.work.items).toEqual([]);
  expect((await page.request.get(`/api/portal/work/${NOT_OWN}`)).status()).toBe(503);

  // Un ID de cliente enviado por el navegador nunca debe cambiar el sujeto.
  const forged = randomUUID();
  const injected = await page.request.get(`/api/portal/overview?customerId=${forged}`);
  expect(injected.status()).toBe(200);
  expect(((await injected.json()) as PortalOverview).profile.email).toBe(emailA);

  // Nueva sesión B sin cookies A o admin; dos clientes distinguidos por CRM.
  const bctx = await browser.newContext({ baseURL: new URL(page.url()).origin });
  try {
    const bpage = await bctx.newPage();
    await registerAndLogin(bpage, emailB, 'Cliente Portal B');
    expect((await overview(bpage)).response.profile.email).toBe(emailB);
    expect((await overview(page)).response.profile.email).toBe(emailA);
    expect((await bpage.request.get(`/api/portal/work/${NOT_OWN}`)).status()).toBe(503);
  } finally {
    await bctx.close();
  }

  // Sales presente y sano, pero sin pedidos: EMPTY no equivale a UNAVAILABLE.
  await cambiarModulo(page, 'sales', 'Activar');
  const withSales = (await overview(page)).response;
  expect(withSales.orders.state).toBe('empty');
  expect(withSales.work.state).toBe('unavailable');

  // Tracking y su cadena real se activan en orden de dependencia.
  for (const moduleCode of ['services', 'service_orders', 'tracking'] as const) {
    await cambiarModulo(page, moduleCode, 'Activar');
  }
  const withTracking = (await overview(page)).response;
  expect(withTracking.orders.state).toBe('empty');
  expect(withTracking.work.state).toBe('empty');
  expect((await page.request.get(`/api/portal/work/${NOT_OWN}`)).status()).toBe(404);

  // Portal apagado: sus rutas dejan de existir; resto del sistema sigue vivo.
  await cambiarModulo(page, 'portal', 'Desactivar');
  expect(await capacidadesActivas(page)).not.toContain('portal');
  expect((await page.request.get('/api/portal/overview')).status()).toBe(404);
  expect((await page.request.get('/api/customer/profile')).status()).toBe(200);
  console.log('[M08-HTTP] PASS: cookie admin != cliente, A/B, ON/OFF, absent/empty, 404/503');
});
