import { expect, test } from '@playwright/test';
import {
  loginAsE2eAdmin,
  loginComoAdminMenor,
} from '../fixtures/auth.js';

test('CORE/M04 — super_admin configura y diagnostica SMTP sin exponer el secreto', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/configuracion');

  await expect(page.getByRole('heading', { name: 'Correo saliente' })).toBeVisible();

  const serverRow = page.locator('.set-row').filter({ hasText: 'smtp_server' });
  await serverRow
    .getByLabel('Servidor SMTP de correo saliente')
    .fill('smtp.example.test');
  await serverRow.getByRole('button', { name: 'Guardar' }).click();

  const fromRow = page.locator('.set-row').filter({ hasText: 'smtp_from' });
  await fromRow
    .getByLabel('Correo remitente y usuario SMTP')
    .fill('no-reply@sillar.test');
  await fromRow.getByRole('button', { name: 'Guardar' }).click();

  await expect(page.getByText('Nunca probado', { exact: true })).toBeVisible();
  await expect(page.getByText('SILLAR_SMTP_PASSWORD')).toBeVisible();
  await expect(page.getByLabel(/contraseña smtp/i)).toHaveCount(0);

  await page.getByRole('button', { name: 'Enviar correo de prueba' }).click();

  await expect(
    page.getByText('Falta la variable de entorno SILLAR_SMTP_PASSWORD.'),
  ).toBeVisible();
  await expect(
    page.getByText('Última prueba fallida', { exact: true }),
  ).toBeVisible();

  const publicResponse = await page.request.get('/api/settings/public');
  expect(publicResponse.ok()).toBeTruthy();
  const publicSettings = (await publicResponse.json()) as Record<string, string>;

  expect(publicSettings.smtp_server).toBeUndefined();
  expect(publicSettings.smtp_port).toBeUndefined();
  expect(publicSettings.smtp_from).toBeUndefined();
});

test('CORE/M04 — admin puede ver SMTP pero no editarlo ni probarlo', async ({ page }) => {
  await loginComoAdminMenor(page);
  await page.goto('/admin/configuracion');

  await expect(page.getByRole('heading', { name: 'Correo saliente' })).toBeVisible();
  await expect(page.getByLabel('Servidor SMTP de correo saliente')).toBeDisabled();
  await expect(
    page
      .getByText(
        'Editar la configuración de correo exige el rol de administrador principal.',
        { exact: true },
      )
      .first(),
  ).toBeVisible();

  await expect(
    page.getByRole('button', { name: 'Enviar correo de prueba' }),
  ).toHaveCount(0);
});
