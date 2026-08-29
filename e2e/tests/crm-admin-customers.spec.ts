import { expect, test } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';

test('M04 — panel crea, edita, invita y reactiva una ficha', async ({ page }) => {
  const email =
    `panel-cliente-${Date.now()}-${Math.random().toString(16).slice(2)}@sillar.test`;

  await loginAsE2eAdmin(page);
  await page.goto('/admin/clientes');

  await expect(page.getByRole('heading', { name: 'Clientes' })).toBeVisible();

  await page.getByRole('button', { name: 'Nuevo cliente' }).click();
  await page.getByLabel('Nombre completo').fill('Cliente Panel');
  await page.getByLabel('Correo').fill(email);
  await page.getByLabel('Teléfono').fill('987654321');
  await page.getByLabel('Notas internas').fill('Creado desde el panel E2E.');
  await page.getByRole('button', { name: 'Crear ficha' }).click();

  await expect(page).toHaveURL(/\/admin\/clientes\/[0-9a-f-]+$/);
  await expect(
    page.getByRole('heading', { name: 'Cliente Panel' }),
  ).toBeVisible();
  await expect(page.getByText('Sin cuenta', { exact: true })).toBeVisible();
  await expect(
    page.getByText('Módulo de pedidos no instalado'),
  ).toBeVisible();

  await page.getByLabel('Notas internas').fill(
    'Ficha revisada y lista para invitación.',
  );
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(page.getByText('Ficha actualizada.')).toBeVisible();

  await page.getByRole('button', { name: 'Enviar invitación' }).click();
  await expect(
    page.getByText('Invitación emitida', { exact: false }),
  ).toBeVisible();
  await expect(page.getByText('Invitada', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Dar de baja' }).click();
  const dialog = page.getByRole('alertdialog');
  await dialog.getByRole('button', { name: 'Dar de baja' }).click();

  await expect(page.getByText('De baja', { exact: true }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reactivar' })).toBeVisible();

  await page.getByRole('button', { name: 'Reactivar' }).click();
  await expect(page.getByText('Cliente reactivado.')).toBeVisible();
  await expect(page.getByText('Invitada', { exact: true })).toBeVisible();
});
