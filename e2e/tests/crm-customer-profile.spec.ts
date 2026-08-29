import { expect, test } from '@playwright/test';

const PASSWORD = 'bosque-cobalto-dieciseis-lunas';

test('M04 — perfil muestra verificación y cambia dirección preferida', async ({ page }) => {
  const email =
    `perfil-ui-${Date.now()}-${Math.random().toString(16).slice(2)}@sillar.test`;

  await page.goto('/crear-cuenta');
  await page.getByLabel('Nombre completo').fill('Cliente Perfil');
  await page.getByLabel('Correo').fill(email);
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
  await expect(
    page.getByText('Correo pendiente de verificar'),
  ).toBeVisible();

  // Prueba también que C8/C9 reconstruyen el CSRF después de una recarga.
  await page.reload();

  await page.getByLabel('Etiqueta').fill('Casa');
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill('Av. Siempre Viva 123');
  await page.getByLabel('Distrito').fill('Cayma');
  await page.getByLabel('Provincia').fill('Arequipa');
  await page.getByLabel('Departamento').fill('Arequipa');
  await page.getByLabel('Marcar como preferida').check();
  await page.getByRole('button', { name: 'Añadir dirección' }).click();

  const casa = page.getByRole('article', { name: 'Dirección Casa' });
  await expect(casa).toContainText('Preferida');

  await page.getByLabel('Etiqueta').fill('Oficina');
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill('Calle Mercaderes 456');
  await page.getByLabel('Distrito').fill('Arequipa');
  await page.getByLabel('Provincia').fill('Arequipa');
  await page.getByLabel('Departamento').fill('Arequipa');
  await page.getByRole('button', { name: 'Añadir dirección' }).click();

  const oficina = page.getByRole('article', { name: 'Dirección Oficina' });
  await expect(oficina).toBeVisible();
  await expect(oficina).not.toContainText('Preferida');

  await oficina.getByRole('button', { name: 'Usar como preferida' }).click();

  await expect(oficina).toContainText('Preferida');
  await expect(casa).not.toContainText('Preferida');
});
