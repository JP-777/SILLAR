import { expect, test } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';

test('M04 — contacto público llega al panel y admite baja lógica', async ({ page }) => {
  const marker = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const subject = `Consulta E2E ${marker}`;

  await page.goto('/contacto');
  await page.getByLabel('Nombre completo').fill('Visitante Contacto');
  await page.getByLabel('Correo').fill(`contacto-${marker}@sillar.test`);
  await page.getByLabel('Asunto').fill(subject);
  await page.getByLabel('Mensaje').fill(
    'Necesito información sobre los productos disponibles.',
  );
  await page.getByRole('button', { name: 'Enviar mensaje' }).click();

  await expect(page.getByText('Recibimos tu mensaje.')).toBeVisible();

  await loginAsE2eAdmin(page);
  await page.goto('/admin/mensajes');

  const row = page.getByRole('row').filter({ hasText: subject });
  await expect(row).toBeVisible();
  await expect(row).toContainText('Visitante');

  await row.getByRole('link', { name: 'Ver mensaje' }).click();

  await expect(page.getByRole('heading', { name: subject })).toBeVisible();
  await expect(
    page.getByText('Necesito información sobre los productos disponibles.'),
  ).toBeVisible();

  await page.getByRole('button', { name: 'Dar de baja' }).click();
  const dialog = page.getByRole('alertdialog');
  await dialog.getByRole('button', { name: 'Dar de baja' }).click();

  await expect(page.getByText('De baja', { exact: true })).toBeVisible();

  await page.getByRole('link', { name: 'Volver a mensajes' }).click();
  await expect(
    page.getByRole('row').filter({ hasText: subject }),
  ).toHaveCount(0);

  await page.getByLabel('Incluir mensajes dados de baja').check();
  const inactiveRow = page.getByRole('row').filter({ hasText: subject });
  await expect(inactiveRow).toBeVisible();
  await expect(inactiveRow).toContainText('De baja');
});
