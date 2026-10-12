/**
 * SILLAR M08 — Sales M03 poblado, HTTP + PostgreSQL reales.
 * Escenario focal de integración. No sustituye [M08-CICLO] ni QA independiente.
 * El arnés crea y destruye su propia base efímera (worktree aislada).
 *
 * La verificación de correo se establece con SQL SOLO como precondición del
 * fixture: esta prueba NO acredita el flujo SMTP/verificación de M04.
 */
import { randomUUID } from 'node:crypto';
import type { APIRequestContext, Browser, Page } from '@playwright/test';
import { expect, test } from '../fixtures/base.js';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { capacidadesActivas, cambiarModulo } from '../fixtures/grafoDeModulos.js';
import { psql } from '../setup/docker.js';

const PASSWORD = 'bosque-cobalto-dieciseis-lunas';
const SAME_ORIGIN = { 'Sec-Fetch-Site': 'same-origin' };
const PRIVATE_KEYS = [
  'customerId', 'orderId', 'customerAccountId', 'passwordHash', 'csrfToken',
  'adminUserId', 'internalNotes', 'originNode', 'rowVersion',
] as const;

type Customer = { customerId: string; email: string; emailVerified: boolean };
type OrderCreated = { orderCode: string; status: string; totalAmount: number; paymentDueAt: string };
type OrderSummary = { orderCode: string; status: string; totalAmount: number; lineCount: number };
type Overview = {
  profile: { email: string };
  orders: { state: 'available' | 'empty' | 'unavailable' | 'error'; items: OrderSummary[] };
  work: { state: string; items: unknown[] };
};

function q(value: string): string {
  return `'${value.replaceAll("'", "''")}'`;
}

function noPrivateKeys(raw: string, context: string): void {
  for (const key of PRIVATE_KEYS) {
    expect(raw.toLowerCase(), `${context}: clave interna '${key}'`)
      .not.toContain(`"${key.toLowerCase()}"`);
  }
}

async function registerAndLogin(page: Page, email: string, fullName: string): Promise<Customer> {
  const registration = await page.request.post('/api/customer/auth/register', {
    headers: SAME_ORIGIN,
    data: { fullName, email, password: PASSWORD, phone: null },
  });
  expect(registration.status(), `registro A/B: ${await registration.text()}`).toBe(200);

  const login = await page.request.post('/api/customer/auth/login', {
    headers: SAME_ORIGIN,
    data: { email, password: PASSWORD },
  });
  expect(login.status(), `login A/B: ${await login.text()}`).toBe(200);

  const me = await page.request.get('/api/customer/auth/me');
  expect(me.status(), `me A/B: ${await me.text()}`).toBe(200);
  const customer = (await me.json()) as Customer;
  expect(customer.email).toBe(email);
  expect(customer.customerId).toMatch(/^[0-9a-f-]{36}$/i);
  expect(customer.emailVerified).toBe(false);
  return customer;
}

async function setVerifiedFixture(page: Page, customer: Customer): Promise<void> {
  // Fixture SQL efímero, no un mecanismo de producto ni evidencia SMTP de M04.
  const result = await psql(`
    UPDATE crm.customer_accounts
       SET email_verified_at = now()
     WHERE customer_id = ${q(customer.customerId)}::uuid
       AND email_verified_at IS NULL;
    SELECT coalesce(bool_and(email_verified_at IS NOT NULL), false)
      FROM crm.customer_accounts
     WHERE customer_id = ${q(customer.customerId)}::uuid;
  `);
  expect(result.split('\n').at(-1), 'el fixture no verificó exactamente la cuenta').toBe('t');
  const me = await page.request.get('/api/customer/auth/me');
  expect(me.status()).toBe(200);
  expect((await me.json()) as Customer).toMatchObject({
    customerId: customer.customerId, email: customer.email, emailVerified: true,
  });
}

async function adminCsrf(api: APIRequestContext): Promise<Record<string, string>> {
  const response = await api.get('/api/admin/auth/csrf');
  expect(response.status(), `admin csrf: ${await response.text()}`).toBe(200);
  const body = (await response.json()) as { csrfToken: string };
  expect(body.csrfToken).toBeTruthy();
  return { 'X-CSRF-Token': body.csrfToken };
}

async function createProduct(api: APIRequestContext): Promise<{ itemId: string; price: number }> {
  const unique = randomUUID();
  const price = 12.5;
  const response = await api.post('/api/admin/catalog/products', {
    headers: await adminCsrf(api),
    data: {
      name: `Producto M08 Sales ${unique}`,
      slug: `m08-sales-${unique}`,
      shortDescription: 'Escenario de integración de pedidos reales',
      description: null,
      primaryCategoryId: null,
      categoryIds: [],
      brandId: null,
      listPrice: price,
      saleUnit: 'unidad',
      variantLabel: null,
      code: null,
      barcode: null,
    },
  });
  expect(response.status(), `crear producto: ${await response.text()}`).toBe(201);
  const product = (await response.json()) as { items: { id: string }[]; isPublic: boolean; isActive: boolean };
  expect(product.isActive).toBe(true);
  expect(product.isPublic).toBe(true);
  expect(product.items).toHaveLength(1);
  expect(product.items[0].id).toMatch(/^[0-9a-f-]{36}$/i);
  return { itemId: product.items[0].id, price };
}

async function createOrder(page: Page, itemId: string, quantity: number): Promise<OrderCreated> {
  const csrfCookie = (await page.context().cookies()).find(c => c.name === 'sillar_tienda_csrf');
  expect(csrfCookie?.value, 'cookie CSRF del cliente ausente').toBeTruthy();
  const response = await page.request.post('/api/sales/orders', {
    headers: { ...SAME_ORIGIN, 'X-CSRF-Token': csrfCookie!.value },
    data: { lines: [{ itemId, quantity }] },
  });
  expect(response.status(), `POST pedido: ${await response.text()}`).toBe(201);
  const created = (await response.json()) as OrderCreated;
  expect(created.orderCode).toMatch(/^P-\d{4}-/);
  expect(created.status).toBe('pending_payment');
  expect(created.totalAmount).toBe(12.5 * quantity);
  expect(created.paymentDueAt).toBeTruthy();
  return created;
}

async function orderCodes(page: Page): Promise<string[]> {
  const response = await page.request.get('/api/sales/my-orders');
  const raw = await response.text();
  expect(response.status(), `Sales own list: ${raw}`).toBe(200);
  noPrivateKeys(raw, 'sales/my-orders');
  const rows = JSON.parse(raw) as OrderSummary[];
  return rows.map(x => x.orderCode);
}

async function portal(page: Page): Promise<{ value: Overview; raw: string }> {
  const response = await page.request.get('/api/portal/overview?limit=20');
  const raw = await response.text();
  expect(response.status(), `portal overview: ${raw}`).toBe(200);
  noPrivateKeys(raw, 'portal overview');
  return { value: JSON.parse(raw) as Overview, raw };
}

async function setSalesSeriesFixture(): Promise<void> {
  // El seed de M03 NO define una etiqueta universal. Para ESTE nodo E2E
  // ('principal') se configura P antes de activar Sales, para que el reinicio
  // cargue el ajuste en SettingsCache. Solo en core.site_settings efimero.
  const value = await psql(`
    INSERT INTO core.site_settings
      (setting_key, setting_value, value_type, description, is_public, is_active)
    VALUES
      ('sales.order_series_label', 'P', 'text',
       'Serie del nodo principal, fixture E2E M08', false, true)
    ON CONFLICT (setting_key) DO NOTHING;
    SELECT setting_value FROM core.site_settings
     WHERE setting_key = 'sales.order_series_label' AND is_active;
  `);
  expect(value.split('\n').at(-1), 'etiqueta de serie no configurada para E2E').toBe('P');
}

async function verifyOrderOwnershipInPg(customer: Customer, orderCode: string): Promise<void> {
  const count = await psql(`
    SELECT count(*) FROM sales.orders
     WHERE order_code = ${q(orderCode)}
       AND customer_id = ${q(customer.customerId)}::uuid AND is_active;
  `);
  expect(count, `pedido real asociado al cliente ${customer.email}`).toBe('1');
}

async function ownOr404(page: Page, own: string, alien: string): Promise<void> {
  const ok = await page.request.get(`/api/sales/my-orders/${encodeURIComponent(own)}`);
  const raw = await ok.text();
  expect(ok.status(), `detalle propio: ${raw}`).toBe(200);
  expect(raw).toContain(own);
  noPrivateKeys(raw, 'sales/my-orders detail');
  const foreign = await page.request.get(`/api/sales/my-orders/${encodeURIComponent(alien)}`);
  const absent = await page.request.get(`/api/sales/my-orders/${encodeURIComponent('P-2099-999999')}`);
  expect(foreign.status(), 'pedido ajeno debe ocultarse con 404').toBe(404);
  expect(absent.status(), 'pedido inexistente debe responder 404').toBe(404);
  // El traceId de ProblemDetails es distinto por cada petición HTTP.
  // El 404 debe ser equivalente sin usar un identificador diagnóstico aleatorio.
  const foreignRaw = await foreign.text();
  const absentRaw = await absent.text();
  noPrivateKeys(foreignRaw, 'respuesta 404 de pedido ajeno');
  noPrivateKeys(absentRaw, 'respuesta 404 de pedido inexistente');
  const stable404 = (rawBody: string): Record<string, unknown> => {
    const problem = JSON.parse(rawBody) as Record<string, unknown>;
    expect(problem).toMatchObject({ status: 404, title: 'Not Found' });
    delete problem.traceId;
    return problem;
  };
  expect(stable404(foreignRaw), 'ajeno e inexistente deben ser indistinguibles, excepto traceId')
    .toEqual(stable404(absentRaw));
}

test('[M08-SALES-POBLADO] A/B reales por API y PostgreSQL; propiedad y privacidad', async ({ page, browser }: { page: Page; browser: Browser }) => {
  test.setTimeout(18 * 60_000);
  const before = await capacidadesActivas(page);
  expect(before).toContain('crm');
  expect(before).toContain('catalog');
  expect(before).not.toContain('portal');
  expect(before).not.toContain('sales');

  await loginAsE2eAdmin(page);
  let ctxA: Awaited<ReturnType<Browser['newContext']>> | undefined;
  let ctxB: Awaited<ReturnType<Browser['newContext']>> | undefined;
  let ctxN: Awaited<ReturnType<Browser['newContext']>> | undefined;
  try {
    await setSalesSeriesFixture();
    await cambiarModulo(page, 'sales', 'Activar');
    await cambiarModulo(page, 'portal', 'Activar');
    const origin = new URL(page.url()).origin;
    ctxA = await browser.newContext({ baseURL: origin });
    ctxB = await browser.newContext({ baseURL: origin });
    ctxN = await browser.newContext({ baseURL: origin });
    const aPage = await ctxA.newPage();
    const bPage = await ctxB.newPage();
    const noSession = await ctxN.newPage();

    expect((await noSession.request.get('/api/portal/overview')).status()).toBe(401);
    expect((await noSession.request.get('/api/sales/my-orders')).status()).toBe(401);

    const aEmail = `m08-sales-a-${randomUUID()}@sillar.test`;
    const bEmail = `m08-sales-b-${randomUUID()}@sillar.test`;
    const a = await registerAndLogin(aPage, aEmail, 'Cliente M08 Sales A');
    const b = await registerAndLogin(bPage, bEmail, 'Cliente M08 Sales B');
    expect(a.customerId).not.toBe(b.customerId);
    await setVerifiedFixture(aPage, a);
    await setVerifiedFixture(bPage, b);

    const emptyA = await portal(aPage);
    const emptyB = await portal(bPage);
    expect(emptyA.value.orders.state).toBe('empty');
    expect(emptyB.value.orders.state).toBe('empty');
    expect(emptyA.value.orders.items).toEqual([]);
    expect(emptyB.value.orders.items).toEqual([]);

    const { itemId } = await createProduct(page.request);
    const orderA = await createOrder(aPage, itemId, 2);
    const orderB = await createOrder(bPage, itemId, 3);
    expect(orderA.orderCode).not.toBe(orderB.orderCode);
    await verifyOrderOwnershipInPg(a, orderA.orderCode);
    await verifyOrderOwnershipInPg(b, orderB.orderCode);

    expect(await orderCodes(aPage)).toEqual([orderA.orderCode]);
    expect(await orderCodes(bPage)).toEqual([orderB.orderCode]);
    await ownOr404(aPage, orderA.orderCode, orderB.orderCode);
    await ownOr404(bPage, orderB.orderCode, orderA.orderCode);

    const overviewA = await portal(aPage);
    const overviewB = await portal(bPage);
    expect(overviewA.value.profile.email).toBe(aEmail);
    expect(overviewB.value.profile.email).toBe(bEmail);
    expect(overviewA.value.orders.state).toBe('available');
    expect(overviewB.value.orders.state).toBe('available');
    expect(overviewA.value.orders.items.map(x => x.orderCode)).toEqual([orderA.orderCode]);
    expect(overviewB.value.orders.items.map(x => x.orderCode)).toEqual([orderB.orderCode]);
    expect(overviewA.value.orders.items[0]).toMatchObject({ totalAmount: 25, lineCount: 1 });
    expect(overviewB.value.orders.items[0]).toMatchObject({ totalAmount: 37.5, lineCount: 1 });
    expect(overviewA.raw).not.toContain(orderB.orderCode);
    expect(overviewB.raw).not.toContain(orderA.orderCode);

    const forged = await aPage.request.get(`/api/portal/overview?customerId=${b.customerId}`);
    expect(forged.status()).toBe(200);
    const forgedRaw = await forged.text();
    noPrivateKeys(forgedRaw, 'portal forged owner');
    expect((JSON.parse(forgedRaw) as Overview).profile.email).toBe(aEmail);
    expect(forgedRaw).not.toContain(orderB.orderCode);
    console.log('[M08-SALES-POBLADO] PASS: 2 pedidos HTTP/PG, A/B aislados, 404 uniforme, privacidad');
  } finally {
    await ctxA?.close();
    await ctxB?.close();
    await ctxN?.close();
    // Devolver el grafo al estado inicial, aunque Playwright falle.
    const active = await capacidadesActivas(page);
    for (const moduleCode of ['portal', 'sales'] as const) {
      if (active.includes(moduleCode) && !before.includes(moduleCode)) {
        await cambiarModulo(page, moduleCode, 'Desactivar');
      }
    }
    expect([...(await capacidadesActivas(page))].sort()).toEqual([...before].sort());
  }
});
