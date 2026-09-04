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

  // El identificador del mensaje, de la propia URL del detalle
  // (`crm/routes.tsx:83`, `mensajes/:contactMessageId`). Se saca aquí y no se
  // escribe a mano: un valor inventado no correspondería a ninguna fila.
  const contactMessageId = new URL(page.url()).pathname.split('/').pop()!;
  expect(contactMessageId, 'la URL del detalle no lleva el identificador').toMatch(/^\d+$/);
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

  // --- La auditoría de la baja no repite el identificador -----------------
  //
  // El resumen decía «Baja del mensaje de contacto #42.», y la regla de
  // producto no habla de `uuid`: habla de identificadores internos. Que éste
  // sea un entero lo hace igual de interno y **solo lo hace más difícil de
  // ver** — la prueba transversal busca `uuid` y un `#42` le pasa por delante.
  // Por eso esta comprobación es del productor y no de la pantalla: una regex
  // global de «cualquier número» daría falsos positivos con fechas, precios y
  // teléfonos, así que lo que no puede reconocer una heurística tiene que
  // afirmarlo alguien aquí.
  const auditoria = (await (
    await page.request.get(
      `/api/admin/audit?entityType=contact_message&entityId=${contactMessageId}&action=delete`,
    )
  ).json()) as { items: { entityId: string | null; summary: string | null }[] };

  expect(auditoria.items, 'dar de baja un mensaje dejó de dejar entrada de auditoría').toHaveLength(1);
  const entrada = auditoria.items[0];

  // Sigue sabiéndose exactamente qué fila fue.
  expect(entrada.entityId, 'la auditoría perdió el identificador del mensaje').toBe(contactMessageId);

  // Pero el texto que se lee ya no lo repite.
  expect(
    entrada.summary ?? '',
    'el resumen de la baja vuelve a contener el identificador del mensaje',
  ).not.toContain(contactMessageId);
  expect(entrada.summary ?? '', 'el resumen de la baja lleva un «#»').not.toContain('#');

  // Y sigue diciendo qué pasó, que es para lo que está.
  expect(entrada.summary ?? '', 'el resumen dejó de describir la acción').toContain(
    'Baja del mensaje de contacto',
  );
});
