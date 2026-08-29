import { expect, test } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';

const PASSWORD = 'bosque-cobalto-dieciseis-lunas';

test('M04 — registro, login y sesiones cliente/panel coexisten', async ({ page }) => {
  const email =
    `cliente-ui-${Date.now()}-${Math.random().toString(16).slice(2)}@sillar.test`;

  await page.goto('/crear-cuenta');

  await page.getByLabel('Nombre completo').fill('Cliente Prueba UI');
  await page.getByLabel('Correo').fill(email);
  await page.getByLabel('Teléfono').fill('999888777');
  await page.getByLabel('Contraseña').fill(PASSWORD);
  await page.getByRole('button', { name: 'Crear cuenta' }).click();

  await expect(
    page.getByText('Solicitud de registro procesada.', { exact: false }),
  ).toBeVisible();

  await page.goto('/entrar');
  await page.getByLabel('Correo').fill(email);
  await page.getByLabel('Contraseña').fill(PASSWORD);
  await page.getByRole('button', { name: 'Entrar' }).click();

  await expect(page).toHaveURL('/mi-cuenta');

  let cookies = await page.context().cookies();
  expect(cookies.some((cookie) => cookie.name === 'sillar_tienda')).toBe(true);
  expect(cookies.some((cookie) => cookie.name === 'sillar_tienda_csrf')).toBe(true);
  expect(cookies.some((cookie) => cookie.name === 'sillar_panel')).toBe(false);

  // La cookie de sesión es de navegador y debe sobrevivir una recarga.
  await page.reload();

  const customerAfterReload = await page.evaluate(async () => {
    const response = await fetch('/api/customer/auth/me');
    return {
      status: response.status,
      body: await response.json(),
    };
  });

  expect(customerAfterReload.status).toBe(200);
  expect(customerAfterReload.body).toMatchObject({
    email,
    fullName: 'Cliente Prueba UI',
    emailVerified: false,
  });

  // Abrir el panel en el mismo navegador no debe cerrar ni sustituir la tienda.
  await loginAsE2eAdmin(page);

  cookies = await page.context().cookies();
  expect(cookies.some((cookie) => cookie.name === 'sillar_tienda')).toBe(true);
  expect(cookies.some((cookie) => cookie.name === 'sillar_panel')).toBe(true);

  const identities = await page.evaluate(async () => {
    const [customer, admin] = await Promise.all([
      fetch('/api/customer/auth/me'),
      fetch('/api/admin/auth/me'),
    ]);

    return {
      customerStatus: customer.status,
      customer: await customer.json(),
      adminStatus: admin.status,
      admin: await admin.json(),
    };
  });

  expect(identities.customerStatus).toBe(200);
  expect(identities.customer).toMatchObject({ email });
  expect(identities.adminStatus).toBe(200);
  expect(identities.admin).toMatchObject({
    email: 'verificacion@sillar.test',
  });
});
