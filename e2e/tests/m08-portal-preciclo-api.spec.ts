/**
 * SILLAR M08 — PRECICLO API. Helpers derivados de la prueba 07B publicada.
 * No es la compuerta [M08-CICLO] ni QA independiente del colíder.
 * Solo actúa sobre el PostgreSQL efímero y el host del arnés Playwright.
 */
import { randomUUID } from 'node:crypto';
import type { APIRequestContext, Page } from '@playwright/test';
import { expect, test } from '../fixtures/base.js';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { cambiarModulo, capacidadesActivas } from '../fixtures/grafoDeModulos.js';
import { psql } from '../setup/docker.js';
import { migrate, seed } from '../setup/migrate.js';


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


/**
 * [M08-PRECICLO-API] Acredita SOLO restaurabilidad de composición y datos.
 * No es [M08-CICLO] integral: frontend, sabotajes y puerta canónica pendientes.
 */

async function portalSchemaAusente(): Promise<void> {
  const result = await psql(`
    SELECT count(*) FROM information_schema.schemata WHERE schema_name='portal'
  `);
  expect(result, 'M08 v1 tiene prohibido crear schema portal').toBe('0');
  const tables = await psql(`
    SELECT count(*) FROM information_schema.tables WHERE table_schema='portal'
  `);
  expect(tables).toBe('0');
  const fks = await psql(`
    SELECT count(*) FROM pg_constraint c
    JOIN pg_class d ON d.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = d.relnamespace
    WHERE c.contype='f' AND n.nspname='portal'
  `);
  expect(fks).toBe('0');
}

async function crossFK(): Promise<string> {
  return psql(`
    SELECT count(*) FROM pg_constraint c
    JOIN pg_class d ON d.oid=c.conrelid
    JOIN pg_namespace n ON n.oid=d.relnamespace
    JOIN pg_class r ON r.oid=c.confrelid
    JOIN pg_namespace rn ON rn.oid=r.relnamespace
    WHERE c.contype='f' AND n.nspname='tracking' AND rn.nspname='service_orders'
  `);
}

function quoteId(s: string): string { return `"${s.replaceAll('"','""')}"`; }

async function snapshotDuenos(): Promise<string[]> {
  const tables=await psql(`
    SELECT table_schema || '.' || table_name FROM information_schema.tables
    WHERE table_type='BASE TABLE'
      AND table_schema IN ('crm','services','service_orders','tracking','sales')
    ORDER BY table_schema, table_name
  `);
  const names=tables.split('\n').filter(Boolean);
  expect(names.length, 'no se hallaron tablas físicas de los dueños').toBeGreaterThanOrEqual(5);
  const result: string[]=[];
  for (const full of names){
    const dot=full.indexOf('.');
    const sql=`SELECT count(*) FROM ${quoteId(full.slice(0,dot))}.${quoteId(full.slice(dot+1))}`;
    result.push(`${full}=${await psql(sql)}`);
  }
  return result;
}

async function portalOverview(page: Page): Promise<{profile:{email:string}; orders:{state:string;items:unknown[]};work:{state:string;items:{visibleCode:string}[]}}> {
  const res=await page.request.get('/api/portal/overview');
  const raw=await res.text();
  expect(res.status(),`overview: ${raw}`).toBe(200);
  privateDataAbsent(raw);
  return JSON.parse(raw);
}

async function workStatus(page: Page, code: string, expected: number): Promise<void> {
  const response=await page.request.get(`/api/portal/work/${encodeURIComponent(code)}`);
  const body=await response.text();
  expect(response.status(),`work ${code}: ${body}`).toBe(expected);
  if(expected===200) privateDataAbsent(body);
}

test('[M08-PRECICLO-API] composición OFF/ON, migrate+seed y preservación de CRM/M05b/M06', async ({ page, browser }) => {
  test.setTimeout(25 * 60_000);
  const before=[...(await capacidadesActivas(page))].sort();
  expect(before).toContain('crm');
  for(const code of ['portal','sales','tracking','service_orders','services']) expect(before).not.toContain(code);
  await portalSchemaAusente();
  expect((await page.request.get('/api/portal/overview')).status()).toBe(404);
  await loginAsE2eAdmin(page);
  await cambiarModulo(page,'portal','Activar');
  const baseURL=new URL(page.url()).origin;
  const ctxA=await browser.newContext({baseURL});
  const ctxB=await browser.newContext({baseURL});
  try {
    const pageA=await ctxA.newPage();
    const pageB=await ctxB.newPage();
    const a=await customer(pageA,`m08-ciclo-a-${randomUUID()}@sillar.test`,'Cliente ciclo A');
    const b=await customer(pageB,`m08-ciclo-b-${randomUUID()}@sillar.test`,'Cliente ciclo B');
    const soloCrm=await portalOverview(pageA);
    expect(soloCrm.profile.email).toBe(a.email);
    expect(soloCrm.orders.state).toBe('unavailable');
    expect(soloCrm.work.state).toBe('unavailable');

    await cambiarModulo(page,'sales','Activar');
    const salesActiva=await portalOverview(pageA);
    expect(salesActiva.orders.state).toBe('empty');
    expect(salesActiva.work.state).toBe('unavailable');

    for(const code of ['services','service_orders','tracking'] as const) await cambiarModulo(page,code,'Activar');
    await configureSeries(page.request);
    const service=await createService(page.request,`Servicio ciclo M08 ${randomUUID()}`);
    const ownA=await createWork(page.request,service,a.customerId,'A');
    const ownB=await createWork(page.request,service,b.customerId,'B');
    const anon=await createWork(page.request,service,null,'NULL');
    const note=await page.request.post(`/api/admin/tracking/orders/${ownA.serviceOrderId}/notes`,{
      headers:await csrf(page.request),data:{body:'M08_07B_SEGUIMIENTO_SOLO_ADMIN'},
    });
    expect(note.status(),`nota: ${await note.text()}`).toBe(201);
    const overviewA=await portalOverview(pageA);
    expect(overviewA.work.state).toBe('available');
    expect(overviewA.work.items.map(x=>x.visibleCode)).toEqual([ownA.visibleCode]);
    await workStatus(pageA,ownA.visibleCode,200);
    await workStatus(pageA,ownB.visibleCode,404);
    await workStatus(pageA,anon.visibleCode,404);
    await workStatus(pageB,ownA.visibleCode,404);
    expect(await crossFK(),'detector FK tracking → M05b mudo').toBe('1');
    const baseline=await snapshotDuenos();
    console.log('[M08-PRECICLO] SNAPSHOT_OWNER_TABLES=',baseline.join(','));

    // Desmontar M08 sin tocar módulos dueños ni su persistencia.
    await cambiarModulo(page,'portal','Desactivar');
    expect((await pageA.request.get('/api/portal/overview')).status()).toBe(404);
    expect((await pageA.request.get('/api/customer/profile')).status()).toBe(200);
    expect((await page.request.get(`/api/admin/service-orders/${ownA.serviceOrderId}`)).status()).toBe(200);
    expect(await crossFK()).toBe('1');
    expect(await snapshotDuenos()).toEqual(baseline);
    await portalSchemaAusente();

    // En v1 sin schema no existe DROP de portal. migrate()+seed() deben ser
    // idempotentes sobre los módulos verdaderos y no alterar sus datos.
    await migrate();
    await seed();
    await portalSchemaAusente();
    expect(await crossFK()).toBe('1');
    expect(await snapshotDuenos(),'migrate+seed alteró filas ajenas a Portal').toEqual(baseline);

    await cambiarModulo(page,'portal','Activar');
    const after=await portalOverview(pageA);
    expect(after.profile.email).toBe(a.email);
    expect(after.work.items.map(x=>x.visibleCode)).toEqual([ownA.visibleCode]);
    await workStatus(pageA,ownA.visibleCode,200);
    await workStatus(pageB,ownA.visibleCode,404);

    // Dependencias blandas: su retirada no convierte al portal en 500.
    await cambiarModulo(page,'tracking','Desactivar');
    expect((await portalOverview(pageA)).work.state).toBe('unavailable');
    await workStatus(pageA,ownA.visibleCode,503);
    await cambiarModulo(page,'tracking','Activar');
    expect((await portalOverview(pageA)).work.state).toBe('available');
    await workStatus(pageA,ownA.visibleCode,200);

    await cambiarModulo(page,'sales','Desactivar');
    expect((await portalOverview(pageA)).orders.state).toBe('unavailable');
    expect((await portalOverview(pageA)).work.state).toBe('available');
    await cambiarModulo(page,'sales','Activar');
    expect((await portalOverview(pageA)).orders.state).toBe('empty');

    await cambiarModulo(page,'portal','Desactivar');
    expect((await pageA.request.get('/api/portal/overview')).status()).toBe(404);
    expect(await crossFK()).toBe('1');
    expect(await snapshotDuenos()).toEqual(baseline);
    await portalSchemaAusente();
    console.log('[M08-PRECICLO] PASS: montaje y degradación, DB intacta, sin schema portal');
  } finally {
    await ctxA.close();
    await ctxB.close();
    // Restaurar solo módulos activados por ESTA prueba y en orden inverso.
    const active=await capacidadesActivas(page);
    for(const code of ['portal','tracking','service_orders','services','sales'] as const){
      if(active.includes(code) && !before.includes(code)){
        await cambiarModulo(page,code,'Desactivar');
        const at=active.indexOf(code);if(at>=0) active.splice(at,1);
      }
    }
  }
  expect([...(await capacidadesActivas(page))].sort()).toEqual(before);
});
