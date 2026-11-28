/** M10: licencia, permiso en la operación, CSV auditado y ciclo físico sin datos duplicados. */
import { readFile } from 'node:fs/promises';
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
const period = { startInclusive: '2026-11-01T00:00:00-05:00', endExclusive: '2026-12-01T00:00:00-05:00', zone: 'America/Lima' };
const reportQuery = '?start=2026-11-01T00%3A00%3A00-05%3A00&end=2026-12-01T00%3A00%3A00-05%3A00';
async function headers(page: Page) {
  const response = await page.request.get('/api/admin/auth/csrf'); expect(response.ok()).toBe(true);
  return { 'X-CSRF-Token': (await response.json()).csrfToken as string };
}
async function permission(page: Page, operation: string, allowed: boolean | null) {
  expect(actorId).not.toBeNull();
  const response = await page.request.put(`/api/admin/users/${actorId}/module-permissions/reporting/${operation}`, {
    headers: await headers(page), data: { allowed },
  });
  expect(response.status(), await response.text()).toBe(204);
}
async function migrateReporting() {
  await run('dotnet', ['ef', 'database', 'update', '--no-build', '--project', 'Sillar.Modules.Reports', '--startup-project', 'Sillar.Api'],
    { cwd: path.join(ROOT, 'backend'), env: { ConnectionStrings__Default: CONNECTION_STRING } });
  await psqlArchivo('/scripts/modules/reporting/02_seed.sql');
}
async function foreignCounts() {
  return psql("SELECT (SELECT count(*) FROM catalog.products)::text || ':' || (SELECT count(*) FROM crm.customers)::text || ':' || (SELECT count(*) FROM sales.orders)::text");
}
async function consult(page: Page) {
  await page.goto('/admin/reportes');
  await expect(page.getByRole('heading', { name: 'Reportes de pedidos', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Hoy', exact: true }).click();
  await page.getByRole('button', { name: 'Consultar período', exact: true }).click();
}
async function restore() {
  // Reconstruye mediante el instalador incluso si el ciclo falló tras la baja.
  await migrateReporting();
  await psql("UPDATE core.module_licenses SET is_licensed=true WHERE module_code='reporting'");
  if (actorId !== null) {
    if (!Number.isSafeInteger(actorId) || actorId < 1) throw new Error('Identidad de prueba inválida.');
    await psql(`DELETE FROM core.module_operation_permissions WHERE admin_user_id=${actorId} AND module_code='reporting'`);
  }
  await waitApiReady();
  for (const code of ['reporting', 'sales']) {
    const response = await fetch(`${API_URL}/api/capabilities`, { signal: AbortSignal.timeout(10_000) });
    if (!response.ok) throw new Error(`No se pudo leer el estado al restaurar M10: ${response.status}.`);
    const body = await response.json() as { modules: { code: string }[] };
    if (!body.modules.some(m => m.code === code)) continue;
    const session = await login(); const before = await serviceRuntimeIdentity('api');
    const changed = await fetch(`${API_URL}/api/admin/modules/${code}/deactivate`, {
      method: 'POST', headers: { Cookie: session.cookie, 'X-CSRF-Token': session.csrfToken }, signal: AbortSignal.timeout(10_000),
    });
    if (!changed.ok) throw new Error(`No se pudo restaurar ${code}: ${changed.status}.`);
    await waitServiceRestarted('api', before); await waitApiReady();
  }
}
test.afterEach(async ({ page }, info) => {
  if (info.status !== info.expectedStatus) await duringExpectedOutage(page, restore);
});

test('[M10-CICLO] licencia previa, permisos, archivo auditado, baja y reinstalación', async ({ page }, info) => {
  test.setTimeout(900_000);
  await loginAsE2eAdmin(page);
  const initial = (await capacidadesActivas(page)).sort();
  expect(initial).not.toContain('reporting'); expect(initial).not.toContain('sales');
  const me = await page.request.get('/api/admin/auth/me'); expect(me.ok()).toBe(true);
  const actor = await me.json() as { id: number; fullName: string }; actorId = actor.id;
  const originalCounts = await foreignCounts();
  expect(await psql("SELECT count(*) FROM reporting.__migrations")).toBe('1');

  // Inyección controlada del estado no licenciado, en la base efímera del arnés.
  await psql("UPDATE core.module_licenses SET is_licensed=false WHERE module_code='reporting'");
  await page.goto('/admin/modulos');
  await expect(page.locator('#modulo-reporting')).toContainText('Sin licencia');
  await expect(page.locator('#modulo-reporting').getByRole('switch')).toHaveCount(0);
  const deniedActivation = await page.request.post('/api/admin/modules/reporting/activate', { headers: await headers(page) });
  expect(deniedActivation.status()).toBe(404);
  expect((await page.request.get('/api/admin/reporting/installation-zone')).status()).toBe(404);
  await psql("UPDATE core.module_licenses SET is_licensed=true WHERE module_code='reporting'");
  await cambiarModulo(page, 'reporting', 'Activar');
  await consult(page);
  await expect(page.getByRole('heading', { name: 'Fuente de pedidos no disponible' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Pedidos creados', exact: true })).toHaveCount(0);

  await cambiarModulo(page, 'sales', 'Activar');
  await consult(page);
  await expect(page.getByRole('heading', { name: 'Pedidos creados', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Exportar recuentos del período (CSV)' })).toBeVisible();

  // Cambia el permiso desde el panel real; CORE conserva la decisión.
  await page.goto('/admin/usuarios');
  await page.getByRole('row').filter({ hasText: actor.fullName }).getByRole('button', { name: 'Permisos', exact: true }).click();
  await page.getByLabel('Reportes · Exportar indicadores', { exact: true }).selectOption('deny');
  await expect(page.getByText('Permiso de exportar indicadores guardado.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Cerrar', exact: true }).last().click();
  await consult(page);
  await expect(page.getByRole('heading', { name: 'Pedidos creados', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Exportar recuentos del período (CSV)' })).toHaveCount(0);
  const beforeAudit = await psql("SELECT count(*) FROM core.audit_log WHERE action='export'");
  const refused = await page.request.post('/api/admin/reporting/orders/export', { headers: await headers(page), data: period });
  expect(refused.status()).toBe(403);
  expect(refused.headers()['content-type']).not.toContain('text/csv');
  expect(await psql("SELECT count(*) FROM core.audit_log WHERE action='export'")).toBe(beforeAudit);

  await permission(page, 'export', null);
  await permission(page, 'read', false);
  expect((await page.request.get('/api/admin/reporting/orders' + reportQuery)).status()).toBe(403);
  await page.goto('/admin/reportes');
  await expect(page.getByRole('alert')).toContainText('Tu cuenta no tiene permiso para esta operación.');
  await expect(page.locator('nav a[href="/admin/reportes"]')).toHaveCount(0);
  await permission(page, 'read', null);
  await consult(page);
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Exportar recuentos del período (CSV)' }).click();
  const download = await downloadPromise;
  expect(await download.failure()).toBeNull();
  const downloadPath = await download.path(); expect(downloadPath).not.toBeNull();
  const csv = await readFile(downloadPath!, 'utf8');
  expect(csv).toContain('Cohorte,Inicio (incluido),Fin (excluido),Zona horaria,Observado,Fuente,Indicador,Pedidos');
  expect(csv).toContain('America/Lima'); expect(csv).not.toContain(actor.fullName);
  expect(Number(await psql("SELECT count(*) FROM core.audit_log WHERE action='export'"))).toBe(Number(beforeAudit) + 1);
  expect(await psql("SELECT count(*) FROM core.audit_log WHERE action='export' AND admin_user_name IS NOT NULL AND admin_user_home_node IS NOT NULL AND admin_user_id IS NOT NULL")).toBe(String(Number(beforeAudit) + 1));
  await info.attach('m10-reportes.png', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });

  await cambiarModulo(page, 'reporting', 'Desactivar');
  expect((await page.request.get('/api/admin/reporting/orders' + reportQuery)).status()).toBe(404);
  await expect(page.locator('nav a[href="/admin/reportes"]')).toHaveCount(0);
  await psqlArchivo('/scripts/modules/reporting/99_drop.sql');
  await psqlArchivo('/scripts/modules/reporting/99_drop.sql');
  expect(await psql("SELECT count(*) FROM pg_namespace WHERE nspname='reporting'")).toBe('0');
  const noSchema = await page.request.post('/api/admin/modules/reporting/activate', { headers: await headers(page) });
  expect(noSchema.status()).toBe(409);
  expect(await psql("SELECT count(*) FROM pg_namespace WHERE nspname='reporting'")).toBe('0');
  await migrateReporting(); await migrateReporting();
  expect(await psql("SELECT count(*) FROM reporting.__migrations")).toBe('1');
  await cambiarModulo(page, 'reporting', 'Activar');
  await consult(page);
  await expect(page.getByRole('heading', { name: 'Pedidos creados', exact: true })).toBeVisible();
  await cambiarModulo(page, 'reporting', 'Desactivar');
  await cambiarModulo(page, 'sales', 'Desactivar');
  expect((await capacidadesActivas(page)).sort()).toEqual(initial);
  expect(await foreignCounts()).toBe(originalCounts);
});
