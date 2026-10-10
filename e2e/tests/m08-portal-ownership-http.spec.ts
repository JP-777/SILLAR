/**
 * SILLAR M08 — [M08-HTTP-07B]. Prueba REAL de propiedad A/B/NULL.
 * No es la compuerta [M08-CICLO] ni QA independiente del colíder.
 * Solo actúa sobre el PostgreSQL efímero y el host del arnés Playwright.
 */
import { randomUUID } from 'node:crypto';
import type { APIRequestContext, Page } from '@playwright/test';
import { expect, test } from '../fixtures/base.js';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { cambiarModulo, capacidadesActivas } from '../fixtures/grafoDeModulos.js';

const PASSWORD = 'bosque-cobalto-dieciseis-lunas';
const SAME_ORIGIN = { 'Sec-Fetch-Site': 'same-origin' };

interface AuthenticatedCustomer { customerId: string; email: string; }
interface CreatedWork { serviceOrderId: string; visibleCode: string; customerId: string | null; }
interface CustomerWork { visibleCode: string; currentStatus: string; }
interface PortalResponse { profile: { email: string }; work: { state: string; items: CustomerWork[] }; }

async function customer(page: Page, email: string, fullName: string): Promise<AuthenticatedCustomer> {
  const registration = await page.request.post('/api/customer/auth/register', {
    headers: SAME_ORIGIN,
    data: { email, fullName, password: PASSWORD, phone: null },
  });
  expect(registration.status(), `registro: ${await registration.text()}`).toBe(200);
  const login = await page.request.post('/api/customer/auth/login', {
    headers: SAME_ORIGIN,
    data: { email, password: PASSWORD },
  });
  expect(login.status(), `login: ${await login.text()}`).toBe(200);
  const me = await page.request.get('/api/customer/auth/me');
  expect(me.status(), `identidad: ${await me.text()}`).toBe(200);
  const own = (await me.json()) as AuthenticatedCustomer;
  expect(own.email).toBe(email);
  expect(own.customerId).toMatch(/^[0-9a-f-]{36}$/i);
  return own;
}

async function csrf(api: APIRequestContext): Promise<Record<string, string>> {
  const response = await api.get('/api/admin/auth/csrf');
  expect(response.status(), `csrf: ${await response.text()}`).toBe(200);
  const token = (await response.json()) as { csrfToken: string };
  expect(token.csrfToken.length).toBeGreaterThan(0);
  return { 'X-CSRF-Token': token.csrfToken };
}

async function configureSeries(api: APIRequestContext): Promise<void> {
  const response = await api.get('/api/admin/settings');
  expect(response.status(), `config: ${await response.text()}`).toBe(200);
  const settings = (await response.json()) as { key: string; isPublic: boolean; needsSetup: boolean }[];
  const series = settings.find((s) => s.key === 'service_orders.series_label');
  expect(series, 'Falta el ajuste inicial M05b').toBeDefined();
  expect(series!.isPublic).toBe(false);
  const put = await api.put('/api/admin/settings/service_orders.series_label', {
    headers: await csrf(api),
    data: { value: 'S', isPublic: null },
  });
  expect(put.status(), `serie: ${await put.text()}`).toBe(200);
  expect(((await put.json()) as { needsSetup: boolean }).needsSetup).toBe(false);
}

async function createService(api: APIRequestContext, name: string): Promise<number> {
  const response = await api.post('/api/admin/services', {
    headers: await csrf(api),
    data: {
      name, slug: `portal-07b-${randomUUID()}`,
      shortDescription: 'Descripcion publica de la vitrina', description: null,
      price: 25, saleUnit: 'unidad', imageId: null, imageAltText: null,
    },
  });
  expect(response.status(), `servicio: ${await response.text()}`).toBe(201);
  const { id } = (await response.json()) as { id: number };
  const pub = await api.post(`/api/admin/services/${id}/publish`, { headers: await csrf(api) });
  expect(pub.status(), `publicar servicio: ${await pub.text()}`).toBe(200);
  return id;
}

async function createWork(
  api: APIRequestContext, serviceId: number, customerId: string | null, tag: string,
): Promise<CreatedWork> {
  const response = await api.post('/api/admin/service-orders', {
    headers: await csrf(api),
    data: {
      idempotencyKey: randomUUID(), customerId,
      customerName: customerId === null ? 'Visitante sin cuenta' : null,
      customerPhone: customerId === null ? '999111222' : null,
      customerEmail: null,
      receivedNotes: `M08_07B_NOTA_PRIVADA_${tag}`,
      receivedAt: null, promisedAt: new Date(Date.now() + 48 * 60 * 60_000).toISOString(),
      assignToMe: true,
      items: [{
        serviceId, requestedDetails: `M08_07B_SOLICITUD_INTERNA_${tag}`,
        quantity: 1, agreedUnitPrice: 25,
      }],
    },
  });
  expect(response.status(), `crear orden ${tag}: ${await response.text()}`).toBe(201);
  const work = (await response.json()) as CreatedWork;
  expect(work.serviceOrderId).toMatch(/^[0-9a-f-]{36}$/i);
  expect(work.visibleCode.length).toBeGreaterThan(0);
  expect(work.customerId).toBe(customerId);
  return work;
}

async function overview(page: Page): Promise<{ parsed: PortalResponse; raw: string }> {
  const response = await page.request.get('/api/portal/overview?limit=20');
  const raw = await response.text();
  expect(response.status(), `overview: ${raw}`).toBe(200);
  return { parsed: JSON.parse(raw) as PortalResponse, raw };
}

function privateDataAbsent(raw: string): void {
  for (const name of [
    'customerId', 'serviceOrderId', 'receivedNotes', 'trackingNotes',
    'requestedDetails', 'agreedUnitPrice', 'boardPriority', 'pinned',
    'internalDueAt', 'adminUserId', 'performedBy', 'createdBy',
    'assignmentHistory', 'statusHistory',
  ]) {
    expect(raw.toLowerCase(), `clave administrativa expuesta: ${name}`)
      .not.toContain(`"${name.toLowerCase()}"`);
  }
  for (const marker of [
    'M08_07B_NOTA_PRIVADA_A', 'M08_07B_NOTA_PRIVADA_B', 'M08_07B_NOTA_PRIVADA_NULL',
    'M08_07B_SOLICITUD_INTERNA_A', 'M08_07B_SOLICITUD_INTERNA_B',
    'M08_07B_SOLICITUD_INTERNA_NULL', 'M08_07B_SEGUIMIENTO_SOLO_ADMIN',
  ]) {
    expect(raw, `valor privado expuesto: ${marker}`).not.toContain(marker);
  }
}

test('[M08-HTTP-07B] órdenes A/B/NULL reales, propiedad, 404 y datos privados', async ({ page, browser }) => {
  test.setTimeout(18 * 60_000);
  const before = await capacidadesActivas(page);
  expect(before).toContain('crm');
  expect(before).not.toContain('portal');
  expect(before).not.toContain('tracking');
  expect(before).not.toContain('service_orders');

  await loginAsE2eAdmin(page);
  for (const code of ['services', 'service_orders', 'tracking', 'portal'] as const) {
    await cambiarModulo(page, code, 'Activar');
  }
  for (const code of ['services', 'service_orders', 'tracking', 'portal'] as const) {
    expect(await capacidadesActivas(page)).toContain(code);
  }
  await configureSeries(page.request);

  const baseURL = new URL(page.url()).origin;
  const ctxA = await browser.newContext({ baseURL });
  const ctxB = await browser.newContext({ baseURL });
  try {
    const pageA = await ctxA.newPage();
    const pageB = await ctxB.newPage();
    const aEmail = `m08-07b-a-${randomUUID()}@sillar.test`;
    const bEmail = `m08-07b-b-${randomUUID()}@sillar.test`;
    const a = await customer(pageA, aEmail, 'Propietario A');
    const b = await customer(pageB, bEmail, 'Propietario B');
    expect(a.customerId).not.toBe(b.customerId);

    const service = await createService(page.request, `Servicio 07B ${randomUUID()}`);
    const ownA = await createWork(page.request, service, a.customerId, 'A');
    const ownB = await createWork(page.request, service, b.customerId, 'B');
    const anonymous = await createWork(page.request, service, null, 'NULL');
    expect(new Set([ownA.visibleCode, ownB.visibleCode, anonymous.visibleCode]).size).toBe(3);

    // Sembrar atributos internos genuinos de M06, no solo afirmar ausencia con tablas vacías.
    const priority = await page.request.put(`/api/admin/tracking/orders/${ownA.serviceOrderId}/priority`, {
      headers: await csrf(page.request),
      data: { boardPriority: 7, pinned: true, orderedPeerIds: null },
    });
    expect(priority.status(), `prioridad: ${await priority.text()}`).toBe(200);
    const due = await page.request.put(`/api/admin/tracking/orders/${ownA.serviceOrderId}/due`, {
      headers: await csrf(page.request),
      data: { internalDueAt: new Date(Date.now() + 24 * 60 * 60_000).toISOString() },
    });
    expect(due.status(), `plazo: ${await due.text()}`).toBe(200);
    const note = await page.request.post(`/api/admin/tracking/orders/${ownA.serviceOrderId}/notes`, {
      headers: await csrf(page.request),
      data: { body: 'M08_07B_SEGUIMIENTO_SOLO_ADMIN' },
    });
    expect(note.status(), `nota: ${await note.text()}`).toBe(201);

    const overA = await overview(pageA);
    const overB = await overview(pageB);
    expect(overA.parsed.profile.email).toBe(aEmail);
    expect(overB.parsed.profile.email).toBe(bEmail);
    expect(overA.parsed.work.state).toBe('available');
    expect(overB.parsed.work.state).toBe('available');
    expect(overA.parsed.work.items.map((x) => x.visibleCode)).toEqual([ownA.visibleCode]);
    expect(overB.parsed.work.items.map((x) => x.visibleCode)).toEqual([ownB.visibleCode]);
    privateDataAbsent(overA.raw);
    privateDataAbsent(overB.raw);
    expect(overA.raw).not.toContain(ownB.visibleCode);
    expect(overA.raw).not.toContain(anonymous.visibleCode);
    expect(overB.raw).not.toContain(ownA.visibleCode);
    expect(overB.raw).not.toContain(anonymous.visibleCode);

    // A no puede cambiar de sujeto por query, ni abrir el trabajo B o NULL.
    const forgedOverview = await pageA.request.get(`/api/portal/overview?customerId=${b.customerId}`);
    expect(forgedOverview.status()).toBe(200);
    const forgedRaw = await forgedOverview.text();
    expect((JSON.parse(forgedRaw) as PortalResponse).profile.email).toBe(aEmail);
    expect(forgedRaw).not.toContain(ownB.visibleCode);
    privateDataAbsent(forgedRaw);

    const detailA = await pageA.request.get(`/api/portal/work/${encodeURIComponent(ownA.visibleCode)}`);
    const detailB = await pageB.request.get(`/api/portal/work/${encodeURIComponent(ownB.visibleCode)}`);
    expect(detailA.status(), `detalle A: ${await detailA.text()}`).toBe(200);
    expect(detailB.status(), `detalle B: ${await detailB.text()}`).toBe(200);
    const ownDetailARaw = await detailA.text();
    const ownDetailBRaw = await detailB.text();
    expect(ownDetailARaw).toContain(ownA.visibleCode);
    expect(ownDetailBRaw).toContain(ownB.visibleCode);
    privateDataAbsent(ownDetailARaw);
    privateDataAbsent(ownDetailBRaw);
    expect(ownDetailARaw).not.toContain(ownB.visibleCode);
    expect(ownDetailBRaw).not.toContain(ownA.visibleCode);

    const absentCode = `S-2026-NOEXISTE-${randomUUID()}`;
    for (const ownPage of [pageA, pageB]) {
      const alienCode = ownPage === pageA ? ownB.visibleCode : ownA.visibleCode;
      for (const code of [alienCode, anonymous.visibleCode, absentCode]) {
        const response = await ownPage.request.get(`/api/portal/work/${encodeURIComponent(code)}`);
        expect(response.status(), `fuga de propiedad ${code}: ${await response.text()}`).toBe(404);
      }
      const noAdminAccess = await ownPage.request.get(`/api/admin/service-orders/${ownA.serviceOrderId}`);
      expect(noAdminAccess.status()).toBe(401);
    }
    const bypass = await pageA.request.get(
      `/api/portal/work/${encodeURIComponent(ownB.visibleCode)}?customerId=${b.customerId}`,
    );
    expect(bypass.status()).toBe(404);
    const unauthorizedAdmin = await page.request.get(`/api/portal/work/${encodeURIComponent(ownA.visibleCode)}`);
    expect(unauthorizedAdmin.status()).toBe(401);

    console.log('[M08-HTTP-07B] PASS: propiedad A/B/NULL, notas y plazos internos ausentes, 404 no enumerable');
  } finally {
    await ctxA.close();
    await ctxB.close();
  }
});
