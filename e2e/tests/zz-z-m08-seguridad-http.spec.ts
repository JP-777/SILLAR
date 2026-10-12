import { randomUUID } from 'node:crypto';
import type { APIRequestContext, Page } from '@playwright/test';
import { expect, test } from '../fixtures/base.js';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { capacidadesActivas, cambiarModulo } from '../fixtures/grafoDeModulos.js';

const PASSWORD = 'Nebula!Quartz-5827-Rivers';
const SAME_ORIGIN = { 'Sec-Fetch-Site': 'same-origin' };

async function adminCsrf(api: APIRequestContext): Promise<Record<string, string>> {
  const response = await api.get('/api/admin/auth/csrf');
  const raw = await response.text();
  expect(response.status(), `csrf admin: ${raw}`).toBe(200);
  const body = JSON.parse(raw) as { csrfToken: string };
  expect(body.csrfToken.length).toBeGreaterThan(0);
  return { 'X-CSRF-Token': body.csrfToken };
}

async function customer(
  page: Page,
  email: string,
  fullName: string,
): Promise<{ customerId: string; email: string }> {
  const registration = await page.request.post('/api/customer/auth/register', {
    headers: SAME_ORIGIN,
    data: { email, fullName, password: PASSWORD, phone: null },
  });
  expect(registration.status(), `registro ${email}: ${await registration.text()}`).toBe(200);

  const login = await page.request.post('/api/customer/auth/login', {
    headers: SAME_ORIGIN,
    data: { email, password: PASSWORD },
  });
  expect(login.status(), `login ${email}: ${await login.text()}`).toBe(200);

  const me = await page.request.get('/api/customer/auth/me');
  const raw = await me.text();
  expect(me.status(), `identidad ${email}: ${raw}`).toBe(200);
  const parsed = JSON.parse(raw) as { customerId: string; email: string };
  expect(parsed.email).toBe(email);
  expect(parsed.customerId).toMatch(/^[0-9a-f-]{36}$/i);
  return parsed;
}

test('[M08-SEC-HARD_CRM] Portal no puede activarse sin CRM', async ({ page }) => {
  test.setTimeout(10 * 60_000);

  const initial = await capacidadesActivas(page);
  expect(initial).toContain('crm');
  expect(initial).toContain('b2b');
  expect(initial).not.toContain('portal');

  await loginAsE2eAdmin(page);

  // M07 depende duro de CRM: se suspende primero por la vía pública.
  await cambiarModulo(page, 'b2b', 'Desactivar');
  await cambiarModulo(page, 'crm', 'Desactivar');

  expect(await capacidadesActivas(page)).not.toContain('crm');
  expect(await capacidadesActivas(page)).not.toContain('portal');

  const activation = await page.request.post('/api/admin/modules/portal/activate', {
    headers: await adminCsrf(page.request),
  });
  const raw = await activation.text();

  expect(
    activation.status(),
    `HARD_CRM: Portal se activó sin CRM o no produjo conflicto: ${raw}`,
  ).toBe(409);
  expect(raw.toLowerCase(), 'HARD_CRM: el conflicto no identifica CRM').toContain('crm');
  expect(await capacidadesActivas(page)).not.toContain('portal');

  await cambiarModulo(page, 'crm', 'Activar');
  await cambiarModulo(page, 'b2b', 'Activar');
  expect(await capacidadesActivas(page)).toEqual(expect.arrayContaining(['crm', 'b2b']));
});

test('[M08-SEC-JSON_PRIVACY] overview no expone CustomerId técnico', async ({ page, browser }) => {
  test.setTimeout(10 * 60_000);

  expect(await capacidadesActivas(page)).not.toContain('portal');
  await loginAsE2eAdmin(page);
  await cambiarModulo(page, 'portal', 'Activar');

  const baseURL = new URL(page.url()).origin;
  const context = await browser.newContext({ baseURL });
  try {
    const client = await context.newPage();
    const email = `m08-privacy-${randomUUID()}@sillar.test`;
    const identity = await customer(client, email, 'Cuenta Privacidad Portal');

    const response = await client.request.get('/api/portal/overview');
    const raw = await response.text();

    expect(response.status(), `overview privacidad: ${raw}`).toBe(200);
    expect(
      raw.toLowerCase(),
      `JSON_PRIVACY: M08 expuso customerId en HTTP: ${raw}`,
    ).not.toContain('"customerid"');
    expect(raw, 'JSON_PRIVACY: M08 expuso el UUID técnico de la cuenta')
      .not.toContain(identity.customerId);
  } finally {
    await context.close();
  }

  await cambiarModulo(page, 'portal', 'Desactivar');
  expect(await capacidadesActivas(page)).not.toContain('portal');
});

test('[M08-SEC-GUARD_FRONTEND] Portal OFF no monta /mi-portal ni navegación', async ({ page, browser }) => {
  test.setTimeout(10 * 60_000);

  expect(await capacidadesActivas(page)).not.toContain('portal');
  expect((await page.request.get('/api/portal/overview')).status()).toBe(404);

  // Esta prueba todavía no navegó la página fixture; page.url() sería about:blank
  // y su origin sería "null". Se fija primero un origen HTTP real del Vite E2E.
  await page.goto('/');
  const baseURL = new URL(page.url()).origin;
  const context = await browser.newContext({ baseURL });
  try {
    const client = await context.newPage();
    const email = `m08-guard-${randomUUID()}@sillar.test`;
    await customer(client, email, 'Cuenta Guarda Portal');

    if (process.env.M08_GUARD_FRONTEND_SABOTAGE === '1') {
      // Sabotaje equivalente a una superficie montada indebidamente, sin tocar
      // routes.tsx compartido: solo este navegador recibe Portal como capacidad
      // activa mientras el backend Portal permanece realmente OFF.
      await client.route('**/api/capabilities', async (route) => {
        const upstream = await route.fetch();
        const body = (await upstream.json()) as {
          product: string;
          version: string;
          modules: { code: string; version: string }[];
        };

        if (!body.modules.some((module) => module.code === 'portal')) {
          body.modules.push({ code: 'portal', version: '1.0.0' });
        }

        await route.fulfill({ response: upstream, json: body });
      });
    }

    await client.goto('/mi-portal');
    await client.waitForLoadState('networkidle');

    const pathname = new URL(client.url()).pathname;
    expect(
      pathname,
      'GUARD_FRONTEND: Portal OFF dejó montada la ruta /mi-portal',
    ).not.toBe('/mi-portal');

    expect(
      await client.locator('a[href^="/mi-portal"]').count(),
      'GUARD_FRONTEND: Portal OFF dejó navegación hacia /mi-portal',
    ).toBe(0);

    // La capacidad falsa jamás debe activar el backend.
    expect((await client.request.get('/api/portal/overview')).status()).toBe(404);
  } finally {
    await context.close();
  }
});
