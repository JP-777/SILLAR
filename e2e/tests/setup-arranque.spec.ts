import { expect, test } from '@playwright/test';

test('migraciones pendientes no abren el asistente', async ({ page }) => {
  let capabilitiesRequests = 0;

  page.on('request', (request) => {
    if (new URL(request.url()).pathname === '/api/capabilities') {
      capabilitiesRequests += 1;
    }
  });

  await page.route('**/api/setup/status', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        setupRequired: true,
        migrationsPending: true,
      }),
    });
  });

  await page.goto('/');

  await expect(
    page.getByRole('heading', { name: 'Base de datos no preparada' }),
  ).toBeVisible();

  await expect(
    page.getByRole('button', { name: 'Comprobar de nuevo' }),
  ).toBeVisible();

  await expect(
    page.locator('input[autocomplete="organization"]'),
  ).toHaveCount(0);

  expect(capabilitiesRequests).toBe(0);
});

test('Ir al acceso espera modo normal antes de cambiar de URL', async ({ page }) => {
  let ready = false;

  await page.route('**/api/setup/status', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        setupRequired: !ready,
        migrationsPending: false,
      }),
    });
  });

  await page.route('**/api/setup', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }

    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({
        businessName: 'Prueba de arranque',
        adminUserId: 1,
        email: 'persona@ejemplo.pe',
      }),
    });
  });

  await page.goto('/');

  const form = page.locator('form');

  await form.getByLabel('Nombre del negocio').fill('Prueba de arranque');
  await form.getByLabel('Nombre completo').fill('Persona Administradora');
  await form.getByLabel('Correo').fill('persona@ejemplo.pe');
  await form.getByLabel('Contraseña').fill('Q7!CobreLuna_4829x');

  await form.locator('button[type="submit"]').click();

  await expect(
    page.getByRole('heading', { name: 'Instalación completada' }),
  ).toBeVisible();

  const urlAntes = page.url();

  await page.getByRole('button', { name: 'Ir al acceso' }).click();

  await expect(
    page.getByText(/todavía está terminando la instalación/i),
  ).toBeVisible();

  expect(page.url()).toBe(urlAntes);

  ready = true;

  await page.getByRole('button', { name: 'Ir al acceso' }).click();
  await page.waitForURL('**/login');
});
