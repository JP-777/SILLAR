/**
 * M05b — ciclo físico real con protección de M05a y M06.
 *
 * PRECONDICIÓN: stack Docker E2E efímero de esta worktree; NUNCA ejecutar SQL
 * sobre sillar_dev ni PostgreSQL de producción. Se ordena antes de M06-CICLO.
 *
 * La prueba demuestra que las guardas rechazan ANTES de un DROP; los probes
 * usan una subtransacción para revertir aun una implementación defectuosa.
 * Deja M05a/M05b inactivos, tracking migrado y seed sin órdenes de negocio.
 */
import { randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import type { APIRequestContext, Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/ciclo.js';
import { cambiarModulo, capacidadesActivas } from '../fixtures/grafoDeModulos.js';
import { psql, psqlArchivo } from '../setup/docker.js';
import { API_URL, ROOT } from '../setup/env.js';
import { migrate, seed } from '../setup/migrate.js';

test.use({ aislamientoDeNavegador: import.meta.url });

const CLIENTE = `Cliente [M05B-CICLO] ${Date.now()}`;
const SERVICIO = `Servicio [M05B-CICLO] ${Date.now()}`;
const SLUG = `servicio-ciclo-m05b-${Date.now()}`;
const PANEL = '/admin/ordenes-servicio';

interface Orden { serviceOrderId: string; visibleCode: string; currentStatus: string }

async function csrf(api: APIRequestContext): Promise<Record<string, string>> {
  const r = await api.get('/api/admin/auth/csrf');
  expect(r.ok(), `CSRF: ${r.status()} ${await r.text()}`).toBe(true);
  return { 'X-CSRF-Token': ((await r.json()) as { csrfToken: string }).csrfToken };
}

async function asegurarSerie(api: APIRequestContext): Promise<boolean> {
  const r = await api.get('/api/admin/settings');
  expect(r.ok(), `settings: ${r.status()} ${await r.text()}`).toBe(true);
  const rows = (await r.json()) as Array<{
    key: string; value: string; isPublic: boolean; needsSetup: boolean;
  }>;
  const serie = rows.find(x => x.key === 'service_orders.series_label');
  expect(serie, 'faltó el seed service_orders.series_label en CORE').toBeDefined();
  expect(serie?.isPublic, 'la configuración de serie debe ser privada').toBe(false);
  if (!serie!.needsSetup) {
    expect(serie!.value.trim()).not.toBe('');
    expect(serie!.value).not.toBe('PENDIENTE_DEFINIR');
    return false;
  }
  expect(serie!.value).toBe('PENDIENTE_DEFINIR');
  const update = await api.put('/api/admin/settings/service_orders.series_label', {
    headers: await csrf(api), data: { value: 'S', isPublic: null },
  });
  expect(update.ok(), `configurar serie: ${update.status()} ${await update.text()}`).toBe(true);
  expect(((await update.json()) as { needsSetup: boolean }).needsSetup).toBe(false);
  return true;
}

async function restaurarSerie(api: APIRequestContext): Promise<void> {
  const r = await api.put('/api/admin/settings/service_orders.series_label', {
    headers: await csrf(api), data: { value: 'PENDIENTE_DEFINIR', isPublic: null },
  });
  expect(r.ok(), `restaurar serie: ${r.status()} ${await r.text()}`).toBe(true);
  expect(((await r.json()) as { needsSetup: boolean }).needsSetup).toBe(true);
}

async function crearServicio(api: APIRequestContext): Promise<number> {
  const r = await api.post('/api/admin/services', {
    headers: await csrf(api),
    data: {
      name: SERVICIO, slug: SLUG, shortDescription: 'Centinela real M05b',
      description: null, price: 25, saleUnit: 'unidad', imageId: null, imageAltText: null,
    },
  });
  expect(r.ok(), `crear servicio: ${r.status()} ${await r.text()}`).toBe(true);
  const id = ((await r.json()) as { id: number }).id;
  const pub = await api.post(`/api/admin/services/${id}/publish`, { headers: await csrf(api) });
  expect(pub.ok(), `publicar servicio: ${pub.status()} ${await pub.text()}`).toBe(true);
  return id;
}

async function crearOrden(api: APIRequestContext, serviceId: number): Promise<Orden> {
  const r = await api.post('/api/admin/service-orders', {
    headers: await csrf(api),
    data: {
      idempotencyKey: randomUUID(), customerId: null, customerName: CLIENTE,
      customerPhone: '999111222', customerEmail: null,
      receivedNotes: 'Centinela creado en [M05B-CICLO]', receivedAt: null,
      promisedAt: new Date(Date.now() + 48 * 3600_000).toISOString(),
      assignToMe: true,
      items: [{ serviceId, requestedDetails: 'Trabajo de restaurabilidad', quantity: 1,
        agreedUnitPrice: 25 }],
    },
  });
  expect(r.status(), `crear orden: ${await r.text()}`).toBe(201);
  return (await r.json()) as Orden;
}

async function menuTieneOrdenes(page: Page): Promise<boolean> {
  await page.goto('/admin');
  const nav = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(nav).toBeVisible();
  return (await nav.locator(`a[href="${PANEL}"]`).count()) === 1;
}

async function rutaOrdenesViva(page: Page): Promise<boolean> {
  await page.goto(PANEL);
  await page.waitForLoadState('networkidle');
  return new URL(page.url()).pathname === PANEL;
}

async function swaggerM05b(api: APIRequestContext): Promise<string[]> {
  const r = await api.get(`${API_URL}/swagger/v1/swagger.json`);
  expect(r.ok(), `Swagger: ${r.status()} ${await r.text()}`).toBe(true);
  return Object.keys(((await r.json()) as { paths: Record<string, unknown> }).paths)
    .filter(p => p.startsWith('/api/admin/service-orders')).sort();
}

function sqlIdent(v: string): string { return `"${v.replaceAll('"', '""')}"`; }

async function inventario(schemas: readonly string[]): Promise<string[]> {
  const selected = schemas.map(v => `'${v.replaceAll("'", "''")}'`).join(', ');
  const raw = await psql(`SELECT table_schema || '.' || table_name FROM information_schema.tables
    WHERE table_type='BASE TABLE' AND table_schema IN (${selected})
      AND NOT (table_schema='core' AND table_name='audit_log') ORDER BY 1`);
  const tables = raw ? raw.split('\n').filter(Boolean) : [];
  const counts: string[] = [];
  for (const name of tables) {
    const dot = name.indexOf('.');
    counts.push(`${name}=${await psql(`SELECT count(*) FROM ${sqlIdent(name.slice(0, dot))}.${sqlIdent(name.slice(dot + 1))}`)}`);
  }
  return counts;
}

/** Un probe NO puede borrar aunque la guarda esté rota: RAISE final revierte el DO entero. */
async function guardaRechazaSinEfectos(script: string, fragmento: string): Promise<void> {
  expect(script).not.toContain('$probe_script$');
  const sql = `DO $probe$
    DECLARE causa text;
    BEGIN
      BEGIN
        EXECUTE $probe_script$${script}$probe_script$;
      EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS causa = MESSAGE_TEXT;
        IF position('${fragmento}' IN lower(causa)) = 0 THEN
          RAISE EXCEPTION 'La guarda rechazó por motivo equivocado: %', causa;
        END IF;
        RETURN;
      END;
      RAISE EXCEPTION 'La guarda dejó ejecutar el desmontaje: se revierte todo el probe';
    END $probe$;`;
  await psql(sql);
}

function verificarParidadM05b(): void {
  const migrador = readFileSync(path.join(ROOT, 'e2e', 'setup', 'migrate.ts'), 'utf8');
  const host = readFileSync(path.join(ROOT, 'backend', 'Sillar.Api', 'Sillar.Api.csproj'), 'utf8');
  const module = readFileSync(path.join(ROOT, 'backend', 'Sillar.Modules.ServiceOrders', 'ServiceOrdersModule.cs'), 'utf8');
  const token = "await applyMigrations('Sillar.Modules.ServiceOrders');";
  const omisiones = (txt: string) => txt.includes(token) ? [] : ['M05b/service_orders: ausente en migrate()'];
  expect(omisiones(migrador), 'paridad de migración M05b').toEqual([]);
  // Sabotaje en memoria, no sobre archivos: el detector DEBE ponerse rojo y nombrar M05b.
  expect(omisiones(migrador.replace(token, ''))).toEqual(['M05b/service_orders: ausente en migrate()']);
  expect(migrador).toMatch(/'service_orders'\s*,\s*'tracking'/);
  expect(host).toMatch(/Sillar\.Modules\.ServiceOrders/);
  expect(module).toContain('public const string ModuleCode = "service_orders"');
  readFileSync(path.join(ROOT, 'database', 'modules', 'service_orders', '02_seed.sql'), 'utf8');
}

/** Reactivar y desactivar en orden de dependencias, sin tocar módulos ajenos. */
async function restaurarEstadoInicial(page: Page, inicial: readonly string[]): Promise<void> {
  const estados = await capacidadesActivas(page);
  for (const code of ['tracking', 'service_orders', 'services'] as const) {
    if (estados.includes(code) && !inicial.includes(code)) {
      await cambiarModulo(page, code, 'Desactivar');
      estados.splice(estados.indexOf(code), 1);
    }
  }
}

test('[M05B-CICLO] activación, guardas M05a/M06, desmontaje, reinstalación y restauración', async ({ page }, testInfo) => {
  test.setTimeout(900_000);
  await loginAsE2eAdmin(page);
  const inicial = [...(await capacidadesActivas(page))].sort();
  for (const code of ['services', 'service_orders', 'tracking']) {
    expect(inicial, `el arnés debe dejar ${code} inactivo al iniciar`).not.toContain(code);
  }

  verificarParidadM05b();
  expect(await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name='tracking'")).toBe('1');
  const registro = await psql("SELECT count(*) FROM core.modules WHERE code='service_orders'");
  expect(registro, 'el setup no registró M05b').toBe('1');
  let devolverSerie = false;
  const observaciones: Record<string, unknown> = { initialModules: inicial };
  try {
    devolverSerie = await asegurarSerie(page.request);
    await cambiarModulo(page, 'services', 'Activar');
    await cambiarModulo(page, 'service_orders', 'Activar');
    expect(await capacidadesActivas(page)).toEqual(expect.arrayContaining(['services', 'service_orders']));

    const serviceId = await crearServicio(page.request);
    const order = await crearOrden(page.request, serviceId);
    observaciones.orderId = order.serviceOrderId;
    expect(order.currentStatus).toBe('received');
    expect(order.visibleCode.trim()).not.toBe('');
    expect(await psql(`SELECT count(*) FROM service_orders.service_orders WHERE service_order_id='${order.serviceOrderId}'::uuid`)).toBe('1');
    expect(await menuTieneOrdenes(page), 'M05b no aporta navegación').toBe(true);
    expect(await rutaOrdenesViva(page), 'M05b no monta su bandeja').toBe(true);
    await expect(page.getByRole('heading', { level: 1, name: 'Órdenes de servicio' })).toBeVisible();
    const detalle = await page.request.get(`/api/admin/service-orders/${order.serviceOrderId}`);
    expect(detalle.status(), 'M05b no devuelve su orden').toBe(200);
    const swaggerActivo = await swaggerM05b(page.request);
    expect(swaggerActivo.length, 'Swagger no publicó M05b').toBeGreaterThan(0);

    // Dependencia dura: ni la UI ni el SQL de M05a deben permitir retirar services.
    await page.goto('/admin/modulos');
    await expect(page.locator('#modulo-services').getByRole('switch')).toBeDisabled();
    const dropM05a = readFileSync(path.join(ROOT, 'database', 'modules', 'services', '99_drop.sql'), 'utf8');
    expect(dropM05a, 'M05a todavía ejecuta DROP SCHEMA CASCADE (no seguro)').not.toMatch(/DROP\s+SCHEMA(?:\s+IF\s+EXISTS)?\s+services\s+CASCADE\s*;/i);
    expect(dropM05a).toContain('service_orders');
    await guardaRechazaSinEfectos(dropM05a, 'service_orders');
    expect(await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name='services'")).toBe('1');

    // M06 existe físicamente aunque esté inactivo: su FK impide desmontar M05b.
    const dropM05b = readFileSync(path.join(ROOT, 'database', 'modules', 'service_orders', '99_drop.sql'), 'utf8');
    await guardaRechazaSinEfectos(dropM05b, 'tracking');
    expect(await psql(`SELECT count(*) FROM service_orders.service_orders WHERE service_order_id='${order.serviceOrderId}'::uuid`)).toBe('1');

    const auditoriaAntes = Number(await psql('SELECT count(*) FROM core.audit_log'));
    const ajenosAntes = await inventario(['core', 'cms', 'crm', 'catalog', 'services', 'sales', 'b2b']);
    await cambiarModulo(page, 'service_orders', 'Desactivar');
    expect(await capacidadesActivas(page)).not.toContain('service_orders');
    expect(await menuTieneOrdenes(page)).toBe(false);
    expect(await rutaOrdenesViva(page)).toBe(false);
    const apiApagada = await page.request.get('/api/admin/service-orders');
    expect(apiApagada.status(), 'M05b apagado aún expone API').toBe(404);
    expect(await swaggerM05b(page.request)).toEqual([]);
    // Desactivar NO borra datos ni snapshots.
    expect(await psql(`SELECT count(*) FROM service_orders.service_orders WHERE service_order_id='${order.serviceOrderId}'::uuid`)).toBe('1');

    // Desinstalar dependiente M06 inactivo, luego M05b; sin CASCADE y con doble DROP.
    await psqlArchivo('/scripts/modules/tracking/99_drop.sql');
    await psqlArchivo('/scripts/modules/tracking/99_drop.sql');
    expect(await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name='tracking'")).toBe('0');
    await psqlArchivo('/scripts/modules/service_orders/99_drop.sql');
    await psqlArchivo('/scripts/modules/service_orders/99_drop.sql');
    expect(await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name='service_orders'")).toBe('0');
    expect(await inventario(['core', 'cms', 'crm', 'catalog', 'services', 'sales', 'b2b']))
      .toEqual(ajenosAntes);
    expect(Number(await psql('SELECT count(*) FROM core.audit_log'))).toBe(auditoriaAntes + 1);

    await migrate();
    await seed();
    expect(await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name='service_orders'")).toBe('1');
    expect(await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name='tracking'")).toBe('1');
    expect(await psql('SELECT count(*) FROM service_orders.service_orders')).toBe('0');
    expect(await psql('SELECT count(*) FROM tracking.order_tracking')).toBe('0');
    expect(await inventario(['core', 'cms', 'crm', 'catalog', 'services', 'sales', 'b2b']))
      .toEqual(ajenosAntes);
    expect(Number(await psql('SELECT count(*) FROM core.audit_log'))).toBe(auditoriaAntes + 1);

    await cambiarModulo(page, 'service_orders', 'Activar');
    expect(await menuTieneOrdenes(page)).toBe(true);
    expect(await rutaOrdenesViva(page)).toBe(true);
    const orderNew = await crearOrden(page.request, serviceId);
    expect(orderNew.serviceOrderId).not.toBe(order.serviceOrderId);
    observaciones.recreatedOrderId = orderNew.serviceOrderId;
    observaciones.swaggerRoutes = swaggerActivo;
    observaciones.auxiliaryTablesPreserved = ajenosAntes.length;
  } finally {
    try {
      await restaurarEstadoInicial(page, inicial);
    } finally {
      if (devolverSerie) await restaurarSerie(page.request);
    }
  }
  expect([...(await capacidadesActivas(page))].sort()).toEqual(inicial);
  await testInfo.attach('m05b-ciclo-expediente.json', {
    body: JSON.stringify(observaciones, null, 2), contentType: 'application/json',
  });
});
