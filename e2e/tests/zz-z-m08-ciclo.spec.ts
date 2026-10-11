/**
 * M08 Portal — ciclo integral de composición, activación y preservación.
 *
 * Territorio autorizado: esta spec solamente. Portal v1 no tiene schema propio:
 * el ciclo acredita alta/baja funcional, preservación de los dueños y paridad
 * migrate()+seed() sin inventar DDL de Portal.
 */
import { randomUUID } from 'node:crypto';
import type { APIRequestContext, Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { cambiarModulo, capacidadesActivas } from '../fixtures/grafoDeModulos.js';
import { psql } from '../setup/docker.js';
import { migrate, seed } from '../setup/migrate.js';

const PASSWORD = 'Q7!vZ2#pL9@xR4';
const PORTAL_ROOT = '/mi-portal';
const PRIVATE_NOTE = `M08 nota interna ${Date.now()}-${randomUUID()}`;
const PRIVATE_RECEIVED = `M08 recibido interno ${Date.now()}-${randomUUID()}`;

interface AuthenticatedCustomer {
  customerId: string;
  email: string;
}

interface CreatedWork {
  serviceOrderId: string;
  visibleCode: string;
  customerId: string | null;
}

interface CreatedOrder {
  orderCode: string;
}

interface PortalResponse {
  profile: { email: string };
  orders: { state: string; items: unknown[] };
  work: { state: string; items: { visibleCode: string }[] };
}

async function customer(page: Page, email: string, fullName: string): Promise<AuthenticatedCustomer> {
  // Estas dos escrituras son anónimas todavía: el filtro CSRF exige la misma
  // señal que añade un navegador para una petición originada en la SPA.
  const sameOrigin = { 'Sec-Fetch-Site': 'same-origin' };

  const registration = await page.request.post('/api/customer/auth/register', {
    headers: sameOrigin,
    data: { fullName, email, password: PASSWORD, phone: null },
  });
  expect(
    registration.status(),
    `registro ${email}: ${await registration.text()}`,
  ).toBe(200);

  const login = await page.request.post('/api/customer/auth/login', {
    headers: sameOrigin,
    data: { email, password: PASSWORD },
  });
  expect(login.status(), `login ${email}: ${await login.text()}`).toBe(200);

  const me = await page.request.get('/api/customer/auth/me');
  expect(me.status(), `/me ${email}: ${await me.text()}`).toBe(200);
  const own = (await me.json()) as { customerId: string; email: string };
  expect(own.customerId).toMatch(/^[0-9a-f-]{36}$/i);
  expect(own.email.toLowerCase()).toBe(email.toLowerCase());

  return { customerId: own.customerId, email: own.email };
}

async function customerCsrf(page: Page): Promise<Record<string, string>> {
  const cookie = (await page.context().cookies()).find((entry) => entry.name === 'sillar_tienda_csrf');
  expect(cookie?.value, 'la sesión cliente no dejó cookie CSRF').toBeTruthy();
  return { 'X-CSRF-Token': cookie!.value };
}

async function verifyCustomer(customerId: string): Promise<void> {
  await psql(`
    UPDATE crm.customer_accounts
       SET email_verified_at = now()
     WHERE customer_id = '${customerId}'::uuid
  `);
  expect(
    await psql(`
      SELECT count(*)
        FROM crm.customer_accounts
       WHERE customer_id = '${customerId}'::uuid
         AND email_verified_at IS NOT NULL
    `),
    `no se verificó la cuenta efímera ${customerId}`,
  ).toBe('1');
}

async function adminCsrf(api: APIRequestContext): Promise<Record<string, string>> {
  const response = await api.get('/api/admin/auth/csrf');
  expect(response.ok(), `CSRF admin: ${response.status()} ${await response.text()}`).toBe(true);
  const data = (await response.json()) as { csrfToken: string };
  return { 'X-CSRF-Token': data.csrfToken };
}

async function configureSetting(
  api: APIRequestContext,
  key: 'sales.order_series_label' | 'service_orders.series_label',
  value: string,
): Promise<void> {
  const list = await api.get('/api/admin/settings');
  expect(list.ok(), `listar settings: ${list.status()} ${await list.text()}`).toBe(true);
  const settings = (await list.json()) as { key: string; isPublic: boolean; needsSetup: boolean }[];
  expect(settings.some((setting) => setting.key === key), `no existe setting ${key}`).toBe(true);

  const update = await api.put(`/api/admin/settings/${encodeURIComponent(key)}`, {
    headers: await adminCsrf(api),
    data: { value, isPublic: null },
  });
  expect(update.ok(), `configurar ${key}: ${update.status()} ${await update.text()}`).toBe(true);
}

async function configureSalesSeries(): Promise<void> {
  // M03 no siembra esta clave a propósito: la serie pertenece al nodo y debe
  // definirse por instalación. La prueba aceptada de Sales usa el nodo efímero
  // `principal` con serie P; aquí hacemos lo mismo, solo dentro de esta base E2E.
  await psql(`
    INSERT INTO core.site_settings
      (setting_key, setting_value, value_type, description, is_public, is_active)
    VALUES
      (
        'sales.order_series_label',
        'P',
        'text',
        'Etiqueta visible de la serie anual de pedidos para este nodo',
        false,
        true
      )
    ON CONFLICT (setting_key) DO UPDATE
       SET setting_value = EXCLUDED.setting_value,
           value_type = EXCLUDED.value_type,
           description = EXCLUDED.description,
           is_public = false,
           is_active = true
  `);

  expect(
    await psql(`
      SELECT setting_value || '|' || is_public::text || '|' || is_active::text
        FROM core.site_settings
       WHERE setting_key = 'sales.order_series_label'
    `),
    'la serie efímera de M03 no quedó configurada',
  ).toBe('P|false|true');
}

async function createCatalogItem(api: APIRequestContext): Promise<string> {
  const suffix = `${Date.now()}-${randomUUID().slice(0, 8)}`;
  const response = await api.post('/api/admin/catalog/products', {
    headers: await adminCsrf(api),
    data: {
      name: `Producto ciclo M08 ${suffix}`,
      slug: `producto-ciclo-m08-${suffix}`.toLowerCase(),
      shortDescription: 'Producto real para [M08-CICLO]',
      description: null,
      primaryCategoryId: null,
      categoryIds: [],
      brandId: null,
      listPrice: 19.9,
      saleUnit: 'unidad',
      variantLabel: null,
      code: null,
      barcode: null,
    },
  });
  expect(response.status(), `crear producto: ${await response.text()}`).toBe(201);
  const product = (await response.json()) as { items: { id: string }[] };
  expect(product.items).toHaveLength(1);
  expect(product.items[0].id).toMatch(/^[0-9a-f-]{36}$/i);
  return product.items[0].id;
}

async function createOrder(page: Page, itemId: string): Promise<CreatedOrder> {
  const response = await page.request.post('/api/sales/orders', {
    headers: await customerCsrf(page),
    data: { lines: [{ itemId, quantity: 1 }] },
  });
  expect(response.status(), `crear pedido: ${await response.text()}`).toBe(201);
  const order = (await response.json()) as CreatedOrder;
  expect(order.orderCode).toMatch(/^.+$/);
  return order;
}

async function createService(api: APIRequestContext): Promise<number> {
  const suffix = `${Date.now()}-${randomUUID().slice(0, 8)}`;
  const response = await api.post('/api/admin/services', {
    headers: await adminCsrf(api),
    data: {
      name: `Servicio ciclo M08 ${suffix}`,
      slug: `servicio-ciclo-m08-${suffix}`.toLowerCase(),
      shortDescription: 'Servicio real para [M08-CICLO]',
      description: null,
      price: 25,
      saleUnit: 'unidad',
      imageId: null,
      imageAltText: null,
    },
  });
  expect(response.status(), `crear servicio: ${await response.text()}`).toBe(201);
  const service = (await response.json()) as { id: number };

  const publish = await api.post(`/api/admin/services/${service.id}/publish`, {
    headers: await adminCsrf(api),
  });
  expect(publish.ok(), `publicar servicio: ${publish.status()} ${await publish.text()}`).toBe(true);
  return service.id;
}

async function createWork(
  api: APIRequestContext,
  serviceId: number,
  customerId: string | null,
  tag: string,
): Promise<CreatedWork> {
  const response = await api.post('/api/admin/service-orders', {
    headers: await adminCsrf(api),
    data: {
      idempotencyKey: randomUUID(),
      customerId,
      customerName: customerId === null ? `Visitante ${tag}` : null,
      customerPhone: customerId === null ? '999111222' : null,
      customerEmail: null,
      receivedNotes: `${PRIVATE_RECEIVED} ${tag}`,
      receivedAt: null,
      promisedAt: new Date(Date.now() + 48 * 60 * 60_000).toISOString(),
      assignToMe: true,
      items: [
        {
          serviceId,
          requestedDetails: `Trabajo ${tag} de [M08-CICLO]`,
          quantity: 1,
          agreedUnitPrice: 25,
        },
      ],
    },
  });
  expect(response.status(), `crear trabajo ${tag}: ${await response.text()}`).toBe(201);
  const work = (await response.json()) as CreatedWork;
  expect(work.customerId).toBe(customerId);
  expect(work.visibleCode).toMatch(/^.+$/);
  return work;
}

async function seedTracking(api: APIRequestContext, work: CreatedWork): Promise<string> {
  const priority = await api.put(`/api/admin/tracking/orders/${work.serviceOrderId}/priority`, {
    headers: await adminCsrf(api),
    data: { boardPriority: 73077, pinned: true, orderedPeerIds: null },
  });
  expect(priority.ok(), `prioridad: ${priority.status()} ${await priority.text()}`).toBe(true);

  const internalDueAt = new Date(Date.now() + 24 * 60 * 60_000).toISOString();
  const due = await api.put(`/api/admin/tracking/orders/${work.serviceOrderId}/due`, {
    headers: await adminCsrf(api),
    data: { internalDueAt },
  });
  expect(due.ok(), `plazo interno: ${due.status()} ${await due.text()}`).toBe(true);

  const note = await api.post(`/api/admin/tracking/orders/${work.serviceOrderId}/notes`, {
    headers: await adminCsrf(api),
    data: { body: PRIVATE_NOTE },
  });
  expect(note.status(), `nota interna: ${await note.text()}`).toBe(201);
  return internalDueAt;
}

async function overview(page: Page): Promise<{ parsed: PortalResponse; raw: string }> {
  const response = await page.request.get('/api/portal/overview?limit=20');
  const raw = await response.text();
  expect(response.status(), `overview: ${raw}`).toBe(200);
  return { parsed: JSON.parse(raw) as PortalResponse, raw };
}

function privateDataAbsent(raw: string, values: readonly string[] = []): void {
  for (const key of [
    'customerId',
    'serviceOrderId',
    'receivedNotes',
    'trackingNotes',
    'boardPriority',
    'internalDueAt',
    'adminUserId',
    'performedBy',
    'createdBy',
  ]) {
    expect(raw, `clave privada expuesta: ${key}`).not.toContain(key);
  }
  for (const value of values) {
    expect(raw, `valor privado expuesto: ${value}`).not.toContain(value);
  }
}

async function workDetail(page: Page, code: string, expected: number): Promise<string> {
  const response = await page.request.get(`/api/portal/work/${encodeURIComponent(code)}`);
  const raw = await response.text();
  expect(response.status(), `trabajo ${code}: ${raw}`).toBe(expected);
  return raw;
}

async function portalSchemaAbsent(): Promise<void> {
  expect(
    await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name='portal'"),
  ).toBe('0');
  expect(
    await psql("SELECT count(*) FROM information_schema.tables WHERE table_schema='portal'"),
  ).toBe('0');
  expect(
    await psql(`
      SELECT count(*)
        FROM pg_constraint c
        JOIN pg_class d ON d.oid=c.conrelid
        JOIN pg_namespace dn ON dn.oid=d.relnamespace
        JOIN pg_class r ON r.oid=c.confrelid
        JOIN pg_namespace rn ON rn.oid=r.relnamespace
       WHERE c.contype='f' AND (dn.nspname='portal' OR rn.nspname='portal')
    `),
  ).toBe('0');
}

function sqlIdentifier(value: string): string {
  return `"${value.replaceAll('"', '""')}"`;
}

async function ownerInventory(): Promise<string[]> {
  const schemas = ['catalog', 'crm', 'sales', 'services', 'service_orders', 'tracking'];
  const literals = schemas.map((schema) => `'${schema}'`).join(',');
  const raw = await psql(`
    SELECT table_schema || '.' || table_name
      FROM information_schema.tables
     WHERE table_type='BASE TABLE'
       AND table_schema IN (${literals})
     ORDER BY table_schema, table_name
  `);
  const tables = raw === '' ? [] : raw.split('\n').filter(Boolean);
  const result: string[] = [];

  for (const full of tables) {
    const dot = full.indexOf('.');
    const schema = full.slice(0, dot);
    const table = full.slice(dot + 1);
    const count = await psql(`SELECT count(*) FROM ${sqlIdentifier(schema)}.${sqlIdentifier(table)}`);
    result.push(`${full}=${count}`);
  }

  return result;
}

async function crossOwnerFks(): Promise<string[]> {
  const raw = await psql(`
    SELECT dn.nspname || '.' || d.relname || '->' || rn.nspname || '.' || r.relname
      FROM pg_constraint c
      JOIN pg_class d ON d.oid=c.conrelid
      JOIN pg_namespace dn ON dn.oid=d.relnamespace
      JOIN pg_class r ON r.oid=c.confrelid
      JOIN pg_namespace rn ON rn.oid=r.relnamespace
     WHERE c.contype='f'
       AND dn.nspname IN ('sales','tracking')
       AND rn.nspname <> dn.nspname
  `);
  return (raw === '' ? [] : raw.split('\n').filter(Boolean)).sort();
}

const REQUIRED_CROSS_FKS = [
  'sales.order_lines->catalog.product_items',
  'sales.orders->crm.customers',
  'tracking.order_tracking->service_orders.service_orders',
] as const;

function missingFks(required: readonly string[], actual: readonly string[]): string[] {
  const have = new Set(actual);
  return required.filter((fk) => !have.has(fk));
}

async function salesSentinel(orderCode: string): Promise<string> {
  const safe = orderCode.replaceAll("'", "''");
  return psql(`
    SELECT order_code || '|' || customer_id::text || '|' || status || '|' || total_amount::text
      FROM sales.orders
     WHERE order_code='${safe}' AND is_active
  `);
}

async function workSentinel(visibleCode: string): Promise<string> {
  const safe = visibleCode.replaceAll("'", "''");
  return psql(`
    SELECT visible_code || '|' || coalesce(customer_id::text,'NULL') || '|' || status
      FROM service_orders.service_orders
     WHERE visible_code='${safe}'
  `);
}

async function trackingSentinel(serviceOrderId: string): Promise<string> {
  return psql(`
    SELECT board_priority::text || '|' || pinned::text || '|' ||
           (internal_due_at IS NOT NULL)::text || '|' ||
           (SELECT count(*)::text
              FROM tracking.tracking_notes n
             WHERE n.order_tracking_id=t.order_tracking_id AND n.is_active)
      FROM tracking.order_tracking t
     WHERE t.service_order_id='${serviceOrderId}'::uuid AND t.is_active
  `);
}

async function routeAlive(page: Page, route = PORTAL_ROOT): Promise<boolean> {
  await page.goto(route);
  // Estas páginas nacen en contextos de navegador creados dentro de la spec,
  // no en el fixture `page` que envuelve goto. Esperamos la navegación de la
  // SPA para no confundir el URL inicial con una ruta realmente montada.
  await page.waitForLoadState('networkidle');
  const pathname = new URL(page.url()).pathname;
  return pathname === route || pathname.startsWith(`${route}/`);
}

async function portalHrefs(page: Page): Promise<string[]> {
  await page.goto(PORTAL_ROOT);
  await page.waitForLoadState('networkidle');

  // Si Portal está activo, esperamos a que su shell pinte al menos un enlace.
  // Si está apagado, la ruta debe redirigir fuera de /mi-portal y la espera
  // termina por esa condición. Así una ausencia real sigue devolviendo [] y
  // falla en la aserción que corresponde, sin convertirla en falso verde.
  await page.waitForFunction(
    (route) =>
      window.location.pathname !== route
      || document.querySelector('a[href^="/mi-portal"]') !== null,
    PORTAL_ROOT,
    { timeout: 5_000 },
  ).catch(() => undefined);

  const hrefs = await page.locator('a[href^="/mi-portal"]').evaluateAll((nodes) =>
    nodes.map((node) => (node as HTMLAnchorElement).getAttribute('href') ?? '').filter(Boolean),
  );
  return [...new Set(hrefs)].sort();
}

async function portalRemains(page: Page): Promise<string[]> {
  const remains: string[] = [];
  if ((await capacidadesActivas(page)).includes('portal')) remains.push('capability:portal');
  if ((await page.request.get('/api/portal/overview')).status() !== 404) remains.push('api:/api/portal/overview');
  if (await routeAlive(page)) remains.push('route:/mi-portal');
  if ((await portalHrefs(page)).length > 0) remains.push('links:/mi-portal');
  return remains;
}

async function restoreGraph(page: Page, initial: readonly string[]): Promise<void> {
  for (const code of ['portal', 'tracking', 'service_orders', 'services', 'sales'] as const) {
    const active = await capacidadesActivas(page);
    if (active.includes(code) && !initial.includes(code)) {
      await cambiarModulo(page, code, 'Desactivar');
    }
  }

  for (const code of ['sales', 'services', 'service_orders', 'tracking', 'portal'] as const) {
    const active = await capacidadesActivas(page);
    if (!active.includes(code) && initial.includes(code)) {
      await cambiarModulo(page, code, 'Activar');
    }
  }
}

test('[M08-CICLO] OFF/ON, Sales y Tracking reales, preservación, paridad y restauración', async ({
  page,
  browser,
}) => {
  test.setTimeout(1_200_000);

  const initial = [...(await capacidadesActivas(page))].sort();
  expect(initial, 'el arnés debe partir con CRM activo').toContain('crm');
  expect(initial, 'el arnés debe partir con Catálogo activo').toContain('catalog');
  for (const code of ['portal', 'sales', 'services', 'service_orders', 'tracking'] as const) {
    expect(initial, `el arnés debería partir con ${code} inactivo`).not.toContain(code);
  }

  await loginAsE2eAdmin(page);

  const contextA = await browser.newContext();
  const contextB = await browser.newContext();
  const pageA = await contextA.newPage();
  const pageB = await contextB.newPage();

  try {
    const a = await customer(
      pageA,
      `m08-ciclo-a-${randomUUID()}@sillar.test`,
      'Cliente ciclo A',
    );
    const b = await customer(
      pageB,
      `m08-ciclo-b-${randomUUID()}@sillar.test`,
      'Cliente ciclo B',
    );
    await verifyCustomer(a.customerId);
    await verifyCustomer(b.customerId);

    // 1 · Portal apagado: no endpoint, ruta ni navegación; CRM sigue operativo.
    expect((await pageA.request.get('/api/portal/overview')).status()).toBe(404);
    expect(await routeAlive(pageA), 'Portal apagado dejó viva /mi-portal').toBe(false);
    expect(await portalHrefs(pageA), 'Portal apagado dejó navegación propia').toEqual([]);
    expect((await pageA.request.get('/api/customer/profile')).status(), 'CRM dejó de responder').toBe(200);
    await portalSchemaAbsent();

    // 2 · Portal activo con solo CRM: perfil útil y proveedores honestamente ausentes.
    await cambiarModulo(page, 'portal', 'Activar');
    const crmOnly = await overview(pageA);
    expect(crmOnly.parsed.profile.email.toLowerCase()).toBe(a.email.toLowerCase());
    expect(crmOnly.parsed.orders.state).toBe('unavailable');
    expect(crmOnly.parsed.work.state).toBe('unavailable');
    expect(crmOnly.parsed.orders.items).toEqual([]);
    expect(crmOnly.parsed.work.items).toEqual([]);
    privateDataAbsent(crmOnly.raw);

    expect(await routeAlive(pageA), 'Portal activo no montó /mi-portal').toBe(true);
    const activeHrefs = await portalHrefs(pageA);
    expect(activeHrefs).toEqual(expect.arrayContaining([
      '/mi-portal',
      '/mi-portal/pedidos',
      '/mi-portal/trabajos',
    ]));
    expect(await portalRemains(pageA), 'los detectores de superficies Portal están mudos').toEqual([
      'capability:portal',
      'api:/api/portal/overview',
      'route:/mi-portal',
      'links:/mi-portal',
    ]);

    // Control negativo de ruta/enlace sin tocar producto: una superficie inventada
    // debe quedar detectada como ausente.
    const fictitiousPortalHref = '/mi-portal/__m08-control-negativo-inexistente';
    // React Router puede conservar el PortalShell para una URL hija desconocida.
    // El negativo contractual aquí es que ese destino ficticio no exista como
    // navegación ofrecida por Portal; los 404 reales se acreditan en API.
    expect(activeHrefs, 'Portal expuso un enlace ficticio').not.toContain(fictitiousPortalHref);

    // 3 · M03 Sales real: producto real + pedidos A/B creados por la API propia.
    await configureSalesSeries();
    const itemId = await createCatalogItem(page.request);
    await cambiarModulo(page, 'sales', 'Activar');

    const beforeOrders = await overview(pageA);
    expect(beforeOrders.parsed.orders.state).toBe('empty');
    expect(beforeOrders.parsed.work.state).toBe('unavailable');

    const orderA = await createOrder(pageA, itemId);
    const orderB = await createOrder(pageB, itemId);

    const afterOrderA = await overview(pageA);
    expect(afterOrderA.parsed.orders.state).toBe('available');
    expect(afterOrderA.raw).toContain(orderA.orderCode);
    expect(afterOrderA.raw).not.toContain(orderB.orderCode);
    privateDataAbsent(afterOrderA.raw);

    const afterOrderB = await overview(pageB);
    expect(afterOrderB.raw).toContain(orderB.orderCode);
    expect(afterOrderB.raw).not.toContain(orderA.orderCode);

    expect((await pageA.request.get(`/api/sales/my-orders/${encodeURIComponent(orderA.orderCode)}`)).status()).toBe(200);
    expect((await pageA.request.get(`/api/sales/my-orders/${encodeURIComponent(orderB.orderCode)}`)).status()).toBe(404);
    expect((await pageB.request.get(`/api/sales/my-orders/${encodeURIComponent(orderA.orderCode)}`)).status()).toBe(404);
    expect((await pageA.request.get(`/api/sales/my-orders/${encodeURIComponent(`NO-EXISTE-${randomUUID()}`)}`)).status()).toBe(404);

    await pageA.goto('/mi-portal/pedidos');
    await expect(pageA.locator('body')).toContainText(orderA.orderCode);
    await expect(pageA.locator('body')).not.toContainText(orderB.orderCode);

    const orderASentinel = await salesSentinel(orderA.orderCode);
    expect(orderASentinel).not.toBe('');

    await cambiarModulo(page, 'sales', 'Desactivar');
    const salesOff = await overview(pageA);
    expect(salesOff.parsed.orders.state).toBe('unavailable');
    expect(salesOff.raw).not.toContain(orderA.orderCode);
    expect(await salesSentinel(orderA.orderCode), 'apagar Sales borró el pedido A').toBe(orderASentinel);
    expect((await pageA.request.get(`/api/sales/my-orders/${encodeURIComponent(orderA.orderCode)}`)).status()).toBe(404);

    await cambiarModulo(page, 'sales', 'Activar');
    const salesBack = await overview(pageA);
    expect(salesBack.raw).toContain(orderA.orderCode);

    // 4 · M05a → M05b → M06 reales: A/B/NULL, privacidad y 404 uniforme.
    await configureSetting(page.request, 'service_orders.series_label', 'S');
    for (const code of ['services', 'service_orders', 'tracking'] as const) {
      await cambiarModulo(page, code, 'Activar');
    }

    const serviceId = await createService(page.request);
    const workA = await createWork(page.request, serviceId, a.customerId, 'A');
    const workB = await createWork(page.request, serviceId, b.customerId, 'B');
    const workNull = await createWork(page.request, serviceId, null, 'NULL');
    const internalDueAt = await seedTracking(page.request, workA);

    const afterWorkA = await overview(pageA);
    expect(afterWorkA.parsed.work.state).toBe('available');
    expect(afterWorkA.raw).toContain(workA.visibleCode);
    expect(afterWorkA.raw).not.toContain(workB.visibleCode);
    expect(afterWorkA.raw).not.toContain(workNull.visibleCode);
    privateDataAbsent(afterWorkA.raw, [PRIVATE_NOTE, PRIVATE_RECEIVED, internalDueAt, '73077']);

    const ownWorkRaw = await workDetail(pageA, workA.visibleCode, 200);
    privateDataAbsent(ownWorkRaw, [PRIVATE_NOTE, PRIVATE_RECEIVED, internalDueAt, '73077']);
    await workDetail(pageA, workB.visibleCode, 404);
    await workDetail(pageA, workNull.visibleCode, 404);
    await workDetail(pageB, workA.visibleCode, 404);
    await workDetail(pageB, workB.visibleCode, 200);
    await workDetail(pageA, `NO-EXISTE-${randomUUID()}`, 404);

    await pageA.goto('/mi-portal/trabajos');
    await expect(pageA.locator('body')).toContainText(workA.visibleCode);
    await expect(pageA.locator('body')).not.toContainText(workB.visibleCode);
    await expect(pageA.locator('body')).not.toContainText(workNull.visibleCode);

    const workASentinel = await workSentinel(workA.visibleCode);
    const trackingASentinel = await trackingSentinel(workA.serviceOrderId);
    expect(workASentinel).not.toBe('');
    expect(trackingASentinel).toBe('73077|true|true|1');

    const fksBefore = await crossOwnerFks();
    expect(fksBefore, 'faltan FK cruzadas propietarias antes del ciclo OFF').toEqual([...REQUIRED_CROSS_FKS]);

    // Control negativo de FK sin destruir datos: añadir una FK ficticia a la
    // lista requerida tiene que ser detectado exactamente como una ausencia.
    const fakeFk = 'tracking.__m08_falta_control->service_orders.service_orders';
    expect(missingFks([...REQUIRED_CROSS_FKS, fakeFk], fksBefore)).toEqual([fakeFk]);

    const inventoryBefore = await ownerInventory();
    await portalSchemaAbsent();

    // 5 · M08 OFF: desaparecen sus superficies, pero los dueños y sus datos quedan.
    await cambiarModulo(page, 'portal', 'Desactivar');
    expect(await portalRemains(pageA), 'M08 apagado dejó restos observables').toEqual([]);
    expect((await pageA.request.get('/api/customer/profile')).status(), 'M04 dejó de funcionar al apagar M08').toBe(200);
    expect((await pageA.request.get(`/api/sales/my-orders/${encodeURIComponent(orderA.orderCode)}`)).status(), 'M03 dejó de funcionar al apagar M08').toBe(200);
    expect((await page.request.get(`/api/admin/service-orders/${workA.serviceOrderId}`)).status(), 'M05b dejó de funcionar al apagar M08').toBe(200);
    expect(await salesSentinel(orderA.orderCode)).toBe(orderASentinel);
    expect(await workSentinel(workA.visibleCode)).toBe(workASentinel);
    expect(await trackingSentinel(workA.serviceOrderId)).toBe(trackingASentinel);
    expect(await ownerInventory(), 'apagar M08 alteró inventario/filas de módulos propietarios').toEqual(inventoryBefore);
    expect(await crossOwnerFks(), 'apagar M08 alteró FK propietarias').toEqual(fksBefore);
    await portalSchemaAbsent();

    // 6 · Portal v1 no tiene DROP. La vía migrate()+seed() debe ser idempotente,
    //     conservar los dueños y no activar Portal por accidente.
    const capabilitiesBeforeMigrate = [...(await capacidadesActivas(page))].sort();
    await migrate();
    await seed();
    expect([...(await capacidadesActivas(page))].sort(), 'migrate()+seed() cambió activaciones').toEqual(
      capabilitiesBeforeMigrate,
    );
    expect(await ownerInventory(), 'migrate()+seed() alteró filas ajenas a Portal').toEqual(inventoryBefore);
    expect(await crossOwnerFks(), 'migrate()+seed() alteró FK ajenas a Portal').toEqual(fksBefore);
    expect(await salesSentinel(orderA.orderCode)).toBe(orderASentinel);
    expect(await workSentinel(workA.visibleCode)).toBe(workASentinel);
    expect(await trackingSentinel(workA.serviceOrderId)).toBe(trackingASentinel);
    expect((await pageA.request.get('/api/portal/overview')).status()).toBe(404);
    await portalSchemaAbsent();

    // Reactivar M08 recupera composición e identidad sin DDL propio.
    await cambiarModulo(page, 'portal', 'Activar');
    const reactivatedA = await overview(pageA);
    expect(reactivatedA.parsed.profile.email.toLowerCase()).toBe(a.email.toLowerCase());
    expect(reactivatedA.raw).toContain(orderA.orderCode);
    expect(reactivatedA.raw).not.toContain(orderB.orderCode);
    expect(reactivatedA.raw).toContain(workA.visibleCode);
    expect(reactivatedA.raw).not.toContain(workB.visibleCode);
    expect(reactivatedA.raw).not.toContain(workNull.visibleCode);
    privateDataAbsent(reactivatedA.raw, [PRIVATE_NOTE, PRIVATE_RECEIVED, internalDueAt, '73077']);
    const reactivatedB = await overview(pageB);
    expect(reactivatedB.parsed.profile.email.toLowerCase()).toBe(b.email.toLowerCase());
    expect(reactivatedB.raw).toContain(orderB.orderCode);
    expect(reactivatedB.raw).not.toContain(orderA.orderCode);
    expect(reactivatedB.raw).toContain(workB.visibleCode);
    expect(reactivatedB.raw).not.toContain(workA.visibleCode);
    await portalSchemaAbsent();

    // 7 · Proveedores blandos ON/OFF después de reactivar Portal.
    await cambiarModulo(page, 'tracking', 'Desactivar');
    const trackingOff = await overview(pageA);
    expect(trackingOff.parsed.work.state).toBe('unavailable');
    expect(trackingOff.raw).toContain(orderA.orderCode);
    expect(await trackingSentinel(workA.serviceOrderId), 'desactivar M06 borró seguimiento').toBe(trackingASentinel);

    await cambiarModulo(page, 'tracking', 'Activar');
    expect((await overview(pageA)).raw).toContain(workA.visibleCode);

    await cambiarModulo(page, 'sales', 'Desactivar');
    const salesOffAgain = await overview(pageA);
    expect(salesOffAgain.parsed.orders.state).toBe('unavailable');
    expect(salesOffAgain.raw).toContain(workA.visibleCode);
    expect(await salesSentinel(orderA.orderCode), 'desactivar M03 borró pedido').toBe(orderASentinel);

    await cambiarModulo(page, 'sales', 'Activar');
    expect((await overview(pageA)).raw).toContain(orderA.orderCode);

    expect(await ownerInventory(), 'los toggles blandos alteraron filas propietarias').toEqual(inventoryBefore);
    expect(await crossOwnerFks(), 'los toggles blandos alteraron FK propietarias').toEqual(fksBefore);
  } finally {
    await contextA.close();
    await contextB.close();
    await restoreGraph(page, initial);
  }

  expect([...(await capacidadesActivas(page))].sort(), 'el ciclo no restauró el grafo inicial').toEqual(initial);
  await portalSchemaAbsent();
});
