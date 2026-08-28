import { expect, test } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';

const CUSTOMER_PASSWORD = 'bosque-cobalto-dieciseis-lunas';

async function createAndLoginCustomer(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/');

  const email =
    `aislamiento-${Date.now()}-${Math.random().toString(16).slice(2)}@sillar.test`;

  const registration = await page.evaluate(
    async ({ email, password }) => {
      const response = await fetch('/api/customer/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fullName: 'Cliente Aislamiento',
          email,
          password,
          phone: null,
        }),
      });

      return {
        status: response.status,
        body: await response.text(),
      };
    },
    { email, password: CUSTOMER_PASSWORD },
  );

  expect(
    registration.status,
    `registro cliente: ${registration.body}`,
  ).toBe(200);

  const login = await page.evaluate(
    async ({ email, password }) => {
      const response = await fetch('/api/customer/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      });

      return {
        status: response.status,
        body: await response.text(),
      };
    },
    { email, password: CUSTOMER_PASSWORD },
  );

  expect(login.status, `login cliente: ${login.body}`).toBe(200);
}

test.describe('M04 — aislamiento HTTP de identidades', () => {
  test('la cookie de cliente no autentica rutas del panel', async ({ page }) => {
    await createAndLoginCustomer(page);

    const cookies = await page.context().cookies();
    expect(cookies.some((cookie) => cookie.name === 'sillar_tienda')).toBe(true);
    expect(cookies.some((cookie) => cookie.name === 'sillar_panel')).toBe(false);

    const response = await page.request.get('/api/admin/settings');

    expect(response.status()).toBe(401);
  });

  test('la cookie administrativa no autentica rutas de cliente', async ({ page }) => {
    await loginAsE2eAdmin(page);

    const cookies = await page.context().cookies();
    expect(cookies.some((cookie) => cookie.name === 'sillar_panel')).toBe(true);
    expect(cookies.some((cookie) => cookie.name === 'sillar_tienda')).toBe(false);

    const response = await page.request.get('/api/customer/profile');

    expect(response.status()).toBe(401);
  });
});
