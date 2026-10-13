import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { psql } from '../setup/docker.js';

const UUID = '3bb862d9-1acd-417b-8247-b94b2cd93113';
const cases = [
  {
    entityId: 'smtp_test',
    recipient: 'persona.normal@sillar.test',
    original: "Correo 'smtp_test' a 'persona.normal@sillar.test': enviado.",
    visible: "Correo 'smtp_test': enviado.",
  },
  {
    entityId: 'email_verification',
    recipient: `m08-audit-${UUID}@sillar.test`,
    original: `Correo 'email_verification' a 'm08-audit-${UUID}@sillar.test': falló. Falta configurar smtp_server.`,
    visible: "Correo 'email_verification': falló. Falta configurar smtp_server.",
  },
] as const;

const sql = (value: string) => `'${value.replaceAll("'", "''")}'`;

test('email_send oculta cualquier destinatario y conserva el summary original en detalle', async ({ page }) => {
  await loginAsE2eAdmin(page);

  for (const item of cases) {
    await psql(`
      INSERT INTO core.audit_log
        (admin_user_id, admin_user_email, module_code, entity_type, entity_id, action, summary, ip_address)
      VALUES
        (NULL, NULL, 'crm', 'email', ${sql(item.entityId)}, 'email_send', ${sql(item.original)}, '127.0.0.1')
    `);
  }

  await page.goto('/admin/auditoria');
  await page.waitForLoadState('networkidle');
  await expect(page.locator('main .ui-spinner')).toHaveCount(0);

  const before = await page.locator('main').innerText();

  for (const item of cases) {
    expect(before, `se presenta el destinatario ${item.recipient}`).not.toContain(item.recipient);
    await expect(page.getByText(item.visible, { exact: true })).toBeVisible();

    const row = page.locator('tbody tr').filter({ has: page.getByText(item.visible, { exact: true }) });
    await expect(row).toHaveCount(1);

    const detail = row.locator('details').filter({ hasText: 'Texto original' });
    const original = detail.getByText(item.original, { exact: true });

    await expect(original).toBeHidden();
    await detail.getByText('Ver texto original', { exact: true }).click();
    await expect(original).toBeVisible();
    await expect(original).toHaveText(item.original);
    await detail.getByText('Ver texto original', { exact: true }).click();
    await expect(original).toBeHidden();
  }

  const response = await page.request.get('/api/admin/audit?page=1&pageSize=100');
  expect(response.ok()).toBe(true);
  const data = (await response.json()) as {
    items: { action: string; entityId: string | null; summary: string | null }[];
  };

  for (const item of cases) {
    expect(data.items.some((entry) =>
      entry.action === 'email_send'
      && entry.entityId === item.entityId
      && entry.summary === item.original,
    )).toBe(true);
  }
});
