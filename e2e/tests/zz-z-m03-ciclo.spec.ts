/** M03: pago tardío manual, permisos, autoría, estados y ciclo físico del esquema. */
import path from 'node:path';
import type { Page } from '@playwright/test';
import { test, expect, duringExpectedOutage } from '../fixtures/ciclo.js';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { cambiarModulo, capacidadesActivas } from '../fixtures/grafoDeModulos.js';
import { login, waitApiReady } from '../setup/api.js';
import { psql, psqlArchivo, serviceRuntimeIdentity, waitServiceRestarted } from '../setup/docker.js';
import { API_URL, CONNECTION_STRING, ROOT } from '../setup/env.js';
import { run } from '../setup/shell.js';

test.use({ aislamientoDeNavegador: import.meta.url });
let actorId: number | null = null;
async function headers(page: Page) {
  const response = await page.request.get('/api/admin/auth/csrf'); expect(response.ok()).toBe(true);
  return { 'X-CSRF-Token': (await response.json()).csrfToken as string };
}
async function migrate() {
  await run('dotnet', ['ef', 'database', 'update', '--no-build', '--project', 'Sillar.Modules.Sales', '--startup-project', 'Sillar.Api'],
    { cwd: path.join(ROOT, 'backend'), env: { ConnectionStrings__Default: CONNECTION_STRING } });
  await psqlArchivo('/scripts/modules/sales/02_seed.sql');
}
async function restore() {
  await migrate();
  if (actorId !== null) {
    if (!Number.isSafeInteger(actorId) || actorId < 1) throw new Error('Identidad de prueba inválida.');
    await psql(`DELETE FROM core.module_operation_permissions WHERE admin_user_id=${actorId} AND module_code='sales'`);
  }
  await waitApiReady();
  const response = await fetch(`${API_URL}/api/capabilities`, { signal: AbortSignal.timeout(10_000) });
  if (!response.ok) throw new Error('No se pudo consultar la activación al restaurar Ventas.');
  const body = await response.json() as { modules: { code: string }[] };
  if (!body.modules.some(m => m.code === 'sales')) return;
  const session = await login(); const before = await serviceRuntimeIdentity('api');
  const changed = await fetch(`${API_URL}/api/admin/modules/sales/deactivate`, { method: 'POST',
    headers: { Cookie: session.cookie, 'X-CSRF-Token': session.csrfToken }, signal: AbortSignal.timeout(10_000) });
  if (!changed.ok) throw new Error('No se pudo desactivar Ventas al restaurar el ciclo.');
  await waitServiceRestarted('api', before); await waitApiReady();
}
test.afterEach(async ({ page }, info) => {
  if (info.status !== info.expectedStatus) await duringExpectedOutage(page, restore);
});

test('[M03-CICLO] pago vencido, reintento, permisos, entrega y reinstalación', async ({ page }, info) => {
  test.setTimeout(900_000); await loginAsE2eAdmin(page);
  const initial = (await capacidadesActivas(page)).sort(); expect(initial).not.toContain('sales');
  const response = await page.request.get('/api/admin/auth/me'); expect(response.ok()).toBe(true);
  const actor = await response.json() as { id: number; fullName: string }; actorId = actor.id;
  const customer = 'f0300000-0000-7000-8000-000000000001';
  await psql(`INSERT INTO crm.customers (customer_id,full_name,email,origin_node)
    VALUES ('${customer}','Cliente de pago tardío','pago-tardio@ciclo.test','principal');
    INSERT INTO sales.orders (order_id,order_code,customer_id,customer_full_name,customer_email,status,payment_due_at,total_amount,currency_code,origin_node)
    VALUES ('f0300000-0000-7000-8000-000000000002','2026-9101','${customer}','Cliente de pago tardío','pago-tardio@ciclo.test','expired',now()-interval '1 hour',10,'PEN','principal'),
           ('f0300000-0000-7000-8000-000000000003','2026-9102','${customer}','Cliente de pago tardío','pago-tardio@ciclo.test','pending_payment',now()-interval '1 hour',20,'PEN','principal');`);
  const foreign = await psql("SELECT (SELECT count(*) FROM catalog.products)::text || ':' || (SELECT count(*) FROM crm.customers)::text");
  await cambiarModulo(page, 'sales', 'Activar'); await page.goto('/admin/pedidos');
  await expect(page.getByRole('heading', { name: 'Pedidos en línea', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Pedido 2026-9101', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Vencido', exact: true })).toBeVisible();
  await page.getByLabel('Referencia de la operación', { exact: true }).fill('YAPE-CICLO-M03');
  const requestPromise = page.waitForRequest(r => r.url().endsWith('/2026-9101/payments') && r.method() === 'POST');
  await page.getByRole('button', { name: 'Registrar pago verificado', exact: true }).click();
  const paymentRequest = (await requestPromise).postDataJSON() as Record<string, unknown>;
  await expect(page.getByText('El total del pedido ya está registrado.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Vencido', exact: true })).toBeVisible();
  await expect(page.getByText('Referencia: YAPE-CICLO-M03', { exact: true })).toBeVisible();
  expect(await psql("SELECT count(*) FROM sales.order_payments WHERE was_late AND source='staff' AND registered_by_admin_user_local_id IS NOT NULL AND registered_by_admin_user_home_node IS NOT NULL AND external_settlement_id IS NULL AND manual_request_id IS NOT NULL")).toBe('1');
  await expect(page.getByText(new RegExp(`Registró ${actor.fullName}`))).toBeVisible();
  const duplicate = await page.request.post('/api/admin/sales/orders/2026-9101/payments', { headers: await headers(page), data: paymentRequest });
  expect(duplicate.ok()).toBe(true); expect((await duplicate.json()).replayed).toBe(true);
  expect(await psql('SELECT count(*) FROM sales.order_payments')).toBe('1');
  const deny = await page.request.put(`/api/admin/users/${actorId}/module-permissions/sales/pay`, { headers: await headers(page), data: { allowed: false } });
  expect(deny.status()).toBe(204);
  const refused = await page.request.post('/api/admin/sales/orders/2026-9101/payments', { headers: await headers(page), data: paymentRequest });
  expect(refused.status()).toBe(403);
  await page.goto('/admin/pedidos/2026-9101');
  await expect(page.getByRole('heading', { name: 'Registrar un pago verificado' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Confirmar atención y preparar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Preparando', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Marcar listo para recoger', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Listo para recoger', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Registrar entrega', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Entregado', exact: true })).toBeVisible();
  await info.attach('m03-pedido-entregado.png', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
  await page.goto('/admin/pedidos/2026-9102');
  await expect(page.getByRole('heading', { name: 'Vencido', exact: true })).toBeVisible();
  expect(await psql("SELECT count(*) FROM sales.order_status_changes WHERE order_id='f0300000-0000-7000-8000-000000000003' AND to_status='expired' AND changed_by IS NULL AND changed_by_admin_user_local_id IS NULL AND changed_by_admin_user_home_node IS NULL")).toBe('1');
  await page.getByLabel('Motivo de cancelación').fill('El cliente solicitó cancelar');
  await page.getByRole('button', { name: 'Cancelar pedido con este motivo', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Cancelado', exact: true })).toBeVisible();
  expect(await psql("SELECT reason FROM sales.order_status_changes WHERE order_id='f0300000-0000-7000-8000-000000000003' AND to_status='cancelled'")).toBe('El cliente solicitó cancelar');
  await cambiarModulo(page, 'sales', 'Desactivar');
  expect((await page.request.get('/api/admin/sales/orders')).status()).toBe(404);
  expect((await page.request.get('/api/sales/my-orders')).status()).toBe(404);
  await expect(page.locator('nav a[href="/admin/pedidos"]')).toHaveCount(0);
  await psqlArchivo('/scripts/modules/sales/99_drop.sql'); await psqlArchivo('/scripts/modules/sales/99_drop.sql');
  expect(await psql("SELECT count(*) FROM pg_namespace WHERE nspname='sales'")).toBe('0');
  expect((await page.request.post('/api/admin/modules/sales/activate', { headers: await headers(page) })).status()).toBe(409);
  expect(await psql("SELECT count(*) FROM pg_namespace WHERE nspname='sales'")).toBe('0');
  await migrate(); await migrate();
  await page.request.put(`/api/admin/users/${actorId}/module-permissions/sales/pay`, { headers: await headers(page), data: { allowed: null } });
  await cambiarModulo(page, 'sales', 'Activar'); await page.goto('/admin/pedidos');
  await expect(page.getByRole('heading', { name: 'No hay pedidos para estos filtros', exact: true })).toBeVisible();
  await cambiarModulo(page, 'sales', 'Desactivar');
  expect((await capacidadesActivas(page)).sort()).toEqual(initial);
  expect(await psql("SELECT (SELECT count(*) FROM catalog.products)::text || ':' || (SELECT count(*) FROM crm.customers)::text")).toBe(foreign);
});
