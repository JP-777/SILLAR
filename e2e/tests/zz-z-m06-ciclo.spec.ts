/**
 * M06 Seguimiento — ciclo físico y funcional completo.
 *
 * Esta prueba se autocontiene: el arnés migra M05a/M05b/M06 pero los deja
 * inactivos. Aquí se activan en orden de dependencia, se crea una orden real,
 * se escriben prioridad/plazo/nota por la API de M06 y solo después empieza la
 * parte destructiva.
 *
 * El detector de la FK se ejecuta antes del primer DROP. Si devolviera vacío,
 * la prueba falla antes de destruir nada: no existe el falso verde "no falta
 * ninguna FK" cuando en realidad el detector es mudo.
 */
import { randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import type { APIRequestContext, Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import {
  cambiarModulo,
  capacidadesActivas,
} from '../fixtures/grafoDeModulos.js';
import { psql, psqlArchivo } from '../setup/docker.js';
import { API_URL, ROOT } from '../setup/env.js';
import { migrate, seed } from '../setup/migrate.js';

const RUTA_PANEL = '/admin/seguimiento';
const NOTA = `Seguimiento ciclo M06 ${Date.now()}`;
const CLIENTE = `Cliente ciclo M06 ${Date.now()}`;
const SERVICIO = `Servicio ciclo M06 ${Date.now()}`;
const SLUG = `servicio-ciclo-m06-${Date.now()}`;

interface OrdenCreada {
  serviceOrderId: string;
  visibleCode: string;
  currentStatus: string;
  receivedAt: string;
}

interface BoardCard {
  serviceOrderId: string;
  visibleCode: string;
  currentStatus: string;
  boardPriority: number | null;
  pinned: boolean;
  internalDueAt: string | null;
}

interface BoardColumn {
  status: string;
  displayName: string;
  cards: BoardCard[];
}

interface BoardResponse {
  columns: BoardColumn[];
}

async function csrf(api: APIRequestContext): Promise<Record<string, string>> {
  const response = await api.get('/api/admin/auth/csrf');
  expect(response.ok(), `CSRF: ${response.status()} ${await response.text()}`).toBe(true);
  const data = (await response.json()) as { csrfToken: string };
  return { 'X-CSRF-Token': data.csrfToken };
}

async function configurarSerieM05b(api: APIRequestContext): Promise<void> {
  const list = await api.get('/api/admin/settings');
  expect(list.ok(), `listar configuración: ${list.status()} ${await list.text()}`).toBe(true);

  const settings = (await list.json()) as {
    key: string;
    isPublic: boolean;
    needsSetup: boolean;
  }[];

  const series = settings.find((setting) => setting.key === 'service_orders.series_label');
  expect(series, 'el seed de M05b no registró service_orders.series_label').toBeDefined();
  expect(series?.isPublic, 'la etiqueta de serie de M05b no debe ser pública').toBe(false);
  expect(series?.needsSetup, 'una instalación nueva debe pedir la etiqueta del nodo').toBe(true);

  const update = await api.put('/api/admin/settings/service_orders.series_label', {
    headers: await csrf(api),
    data: { value: 'S', isPublic: null },
  });
  expect(
    update.ok(),
    `configurar serie M05b: ${update.status()} ${await update.text()}`,
  ).toBe(true);

  const configured = (await update.json()) as { needsSetup: boolean; value: string };
  expect(configured.needsSetup).toBe(false);
  expect(configured.value).toBe('S');
}

async function activarCadena(page: Page): Promise<void> {
  const activas = await capacidadesActivas(page);
  for (const code of ['services', 'service_orders', 'tracking'] as const) {
    if (!activas.includes(code)) {
      await cambiarModulo(page, code, 'Activar');
      activas.push(code);
    }
  }
}

async function restaurarActivacion(page: Page, iniciales: readonly string[]): Promise<void> {
  for (const code of ['tracking', 'service_orders', 'services'] as const) {
    const activas = await capacidadesActivas(page);
    if (activas.includes(code) && !iniciales.includes(code)) {
      await cambiarModulo(page, code, 'Desactivar');
    }
  }
}

async function crearServicio(api: APIRequestContext): Promise<number> {
  const alta = await api.post('/api/admin/services', {
    headers: await csrf(api),
    data: {
      name: SERVICIO,
      slug: SLUG,
      shortDescription: 'Servicio sembrado por el ciclo M06',
      description: null,
      price: 25,
      saleUnit: 'unidad',
      imageId: null,
      imageAltText: null,
    },
  });

  expect(alta.ok(), `crear servicio: ${alta.status()} ${await alta.text()}`).toBe(true);
  const service = (await alta.json()) as { id: number };

  const publish = await api.post(`/api/admin/services/${service.id}/publish`, {
    headers: await csrf(api),
  });
  expect(publish.ok(), `publicar servicio: ${publish.status()} ${await publish.text()}`).toBe(true);
  return service.id;
}

async function crearOrden(api: APIRequestContext, serviceId: number): Promise<OrdenCreada> {
  const promisedAt = new Date(Date.now() + 48 * 60 * 60 * 1000).toISOString();
  const response = await api.post('/api/admin/service-orders', {
    headers: await csrf(api),
    data: {
      idempotencyKey: randomUUID(),
      customerId: null,
      customerName: CLIENTE,
      customerPhone: '999111222',
      customerEmail: null,
      receivedNotes: 'Orden creada por [M06-CICLO]',
      receivedAt: null,
      promisedAt,
      assignToMe: true,
      items: [
        {
          serviceId,
          requestedDetails: 'Trabajo sembrado por el ciclo M06',
          quantity: 1,
          agreedUnitPrice: 25,
        },
      ],
    },
  });

  expect(response.status(), `crear orden: ${await response.text()}`).toBe(201);
  return (await response.json()) as OrdenCreada;
}

async function sembrarSeguimiento(api: APIRequestContext, order: OrdenCreada): Promise<void> {
  const priority = await api.put(`/api/admin/tracking/orders/${order.serviceOrderId}/priority`, {
    headers: await csrf(api),
    data: { boardPriority: 7, pinned: true, orderedPeerIds: null },
  });
  expect(priority.ok(), `prioridad: ${priority.status()} ${await priority.text()}`).toBe(true);

  const due = await api.put(`/api/admin/tracking/orders/${order.serviceOrderId}/due`, {
    headers: await csrf(api),
    data: { internalDueAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString() },
  });
  expect(due.ok(), `plazo: ${due.status()} ${await due.text()}`).toBe(true);

  const note = await api.post(`/api/admin/tracking/orders/${order.serviceOrderId}/notes`, {
    headers: await csrf(api),
    data: { body: NOTA },
  });
  expect(note.ok(), `nota: ${note.status()} ${await note.text()}`).toBe(true);
}

async function board(api: APIRequestContext): Promise<BoardResponse> {
  const response = await api.get('/api/admin/tracking/board?scope=open&page=1&pageSize=20');
  expect(response.ok(), `board: ${response.status()} ${await response.text()}`).toBe(true);
  return (await response.json()) as BoardResponse;
}

function cardOf(data: BoardResponse, orderId: string): BoardCard | undefined {
  return data.columns.flatMap((column) => column.cards).find((card) => card.serviceOrderId === orderId);
}

async function rutaSeguimientoViva(page: Page): Promise<boolean> {
  await page.goto(RUTA_PANEL);
  await page.waitForLoadState('networkidle');
  return new URL(page.url()).pathname === RUTA_PANEL;
}

async function menuTieneSeguimiento(page: Page): Promise<boolean> {
  await page.goto('/admin');
  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu).toBeVisible();
  return (await menu.locator(`a[href="${RUTA_PANEL}"]`).count()) === 1;
}

async function fkTrackingAServiceOrders(): Promise<string[]> {
  const raw = await psql(`
    SELECT dn.nspname || '.' || d.relname || '->' || rn.nspname || '.' || r.relname
      FROM pg_constraint c
      JOIN pg_class d ON d.oid = c.conrelid
      JOIN pg_namespace dn ON dn.oid = d.relnamespace
      JOIN pg_class r ON r.oid = c.confrelid
      JOIN pg_namespace rn ON rn.oid = r.relnamespace
     WHERE c.contype = 'f'
       AND dn.nspname = 'tracking'
       AND rn.nspname = 'service_orders'
     ORDER BY 1
  `);

  return raw === '' ? [] : raw.split('\n').filter(Boolean);
}

function sqlIdentifier(value: string): string {
  return `"${value.replaceAll('"', '""')}"`;
}

async function inventarioExacto(schemas: readonly string[]): Promise<string[]> {
  const literals = schemas.map((schema) => `'${schema.replaceAll("'", "''")}'`).join(', ');
  const raw = await psql(`
    SELECT table_schema || '.' || table_name
      FROM information_schema.tables
     WHERE table_type = 'BASE TABLE'
       AND table_schema IN (${literals})
       AND NOT (table_schema = 'core' AND table_name = 'audit_log')
     ORDER BY 1
  `);
  const tables = raw === '' ? [] : raw.split('\n').filter(Boolean);
  const counts: string[] = [];

  for (const fullName of tables) {
    const dot = fullName.indexOf('.');
    const schema = fullName.slice(0, dot);
    const table = fullName.slice(dot + 1);
    const count = await psql(
      `SELECT count(*) FROM ${sqlIdentifier(schema)}.${sqlIdentifier(table)}`,
    );
    counts.push(`${fullName}=${count}`);
  }

  return counts;
}

async function estadoTracking(orderId: string): Promise<string> {
  return psql(`
    SELECT board_priority::text || '|' || pinned::text || '|' ||
           (internal_due_at IS NOT NULL)::text || '|' ||
           (SELECT count(*)::text FROM tracking.tracking_notes n
             WHERE n.order_tracking_id = t.order_tracking_id AND n.is_active)
      FROM tracking.order_tracking t
     WHERE t.service_order_id = '${orderId}'::uuid AND t.is_active
  `);
}

async function swaggerTrackingPaths(api: APIRequestContext): Promise<string[]> {
  const response = await api.get(`${API_URL}/swagger/v1/swagger.json`);
  expect(response.ok(), `Swagger: ${response.status()} ${await response.text()}`).toBe(true);
  const doc = (await response.json()) as { paths: Record<string, unknown> };
  return Object.keys(doc.paths).filter((route) => route.startsWith('/api/admin/tracking')).sort();
}

test('[M06-CICLO] desactivar, desmontar, reinstalar y reactivar M06 sin tocar M05b ni módulos ajenos', async ({
  page,
}) => {
  test.setTimeout(900_000);
  await loginAsE2eAdmin(page);

  const iniciales = [...(await capacidadesActivas(page))].sort();
  expect(iniciales, 'el arnés debe dejar M05a inactivo antes del ciclo M06').not.toContain('services');
  expect(iniciales, 'el arnés debe dejar M05b inactivo antes del ciclo M06').not.toContain('service_orders');
  expect(iniciales, 'el arnés debe dejar M06 inactivo antes del ciclo M06').not.toContain('tracking');

  await configurarSerieM05b(page.request);

  try {
    // 1 · Construir el estado inicial exigido por la SPEC: M05b+M06 activos y
    //     M06 con prioridad, plazo y nota reales sembrados por API.
    await activarCadena(page);
    const activas = await capacidadesActivas(page);
    expect(activas).toEqual(expect.arrayContaining(['services', 'service_orders', 'tracking']));

    const serviceId = await crearServicio(page.request);
    const order = await crearOrden(page.request, serviceId);
    await sembrarSeguimiento(page.request, order);

    const boardInicial = await board(page.request);
    const cardInicial = cardOf(boardInicial, order.serviceOrderId);
    expect(cardInicial, 'M06 activo no pinta la orden recién creada').toBeDefined();
    expect(cardInicial?.visibleCode).toBe(order.visibleCode);
    expect(cardInicial?.boardPriority).toBe(7);
    expect(cardInicial?.pinned).toBe(true);
    expect(cardInicial?.internalDueAt).not.toBeNull();
    expect(await estadoTracking(order.serviceOrderId)).toBe('7|true|true|1');

    expect(await menuTieneSeguimiento(page), 'M06 activo no aporta su enlace de panel').toBe(true);
    expect(await rutaSeguimientoViva(page), 'M06 activo no monta /admin/seguimiento').toBe(true);
    await expect(page.getByRole('heading', { level: 1, name: 'Seguimiento' })).toBeVisible();

    const swaggerActivo = await swaggerTrackingPaths(page.request);
    expect(swaggerActivo.length, 'M06 activo no publica sus rutas Swagger').toBeGreaterThan(0);

    // El detector tiene que ver la FK ANTES del primer DROP. Si se sabotea para
    // devolver vacío, la prueba se pone roja aquí y no llega a destruir nada.
    expect(
      await fkTrackingAServiceOrders(),
      'el detector de FK no ve la dependencia física tracking -> service_orders',
    ).toEqual(['tracking.order_tracking->service_orders.service_orders']);

    const auditoriaAntes = Number(
      await psql('SELECT count(*) FROM core.audit_log'),
    );

    const inventarioAntes = await inventarioExacto([
      'core',
      'cms',
      'crm',
      'services',
      'service_orders',
    ]);

    // 2 · Desactivar M06: desaparecen superficies, pero ni M05b ni los datos de
    //     seguimiento se borran.
    await cambiarModulo(page, 'tracking', 'Desactivar');
    expect(await capacidadesActivas(page)).not.toContain('tracking');
    expect(await menuTieneSeguimiento(page), 'M06 apagado dejó un enlace de menú').toBe(false);
    expect(await rutaSeguimientoViva(page), 'M06 apagado dejó viva su ruta de panel').toBe(false);

    const apiApagada = await page.request.get('/api/admin/tracking/board');
    expect(apiApagada.status(), 'M06 apagado debería retirar sus endpoints').toBe(404);
    expect(await swaggerTrackingPaths(page.request), 'Swagger conserva M06 apagado').toEqual([]);

    const ordenSigue = await page.request.get(`/api/admin/service-orders/${order.serviceOrderId}`);
    expect(ordenSigue.status(), 'desactivar M06 rompió la orden autoritativa de M05b').toBe(200);
    expect(await estadoTracking(order.serviceOrderId), 'desactivar M06 borró sus datos').toBe('7|true|true|1');

    // 3 · Guarda C6 al revés: el schema de M06 sigue instalado y por su FK M05b
    //     NO puede desmontarse. El rechazo tiene que nombrar a tracking.
    const dropM05b = readFileSync(
      path.join(ROOT, 'database', 'modules', 'service_orders', '99_drop.sql'),
      'utf8',
    );
    let rechazoM05b = '';
    try {
      await psql(dropM05b);
    } catch (error) {
      rechazoM05b = String(error);
    }
    expect(rechazoM05b, 'service_orders se dejó desmontar con tracking instalado').not.toBe('');
    expect(rechazoM05b.toLowerCase(), 'la guarda de M05b no nombra a tracking').toContain('tracking');
    expect(
      await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'service_orders'"),
      'la guarda falló tarde: service_orders fue borrado parcialmente',
    ).toBe('1');

    // 4 · Desmontar M06 físicamente. No CASCADE y dos veces para acreditar
    //     idempotencia. Los recuentos exactos de los módulos ajenos no cambian.
    await psqlArchivo('/scripts/modules/tracking/99_drop.sql');
    await psqlArchivo('/scripts/modules/tracking/99_drop.sql');
    expect(
      await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'tracking'"),
    ).toBe('0');
    expect(
      await inventarioExacto(['core', 'cms', 'crm', 'services', 'service_orders']),
      'desmontar M06 cambió filas de CORE, M02, M04, M05a o M05b',
    ).toEqual(inventarioAntes);

    expect(
      Number(await psql('SELECT count(*) FROM core.audit_log')),
      'desactivar M06 debe registrar exactamente una auditoría administrativa',
    ).toBe(auditoriaAntes + 1);

    const ordenTrasDrop = await page.request.get(`/api/admin/service-orders/${order.serviceOrderId}`);
    expect(ordenTrasDrop.status(), 'desmontar M06 rompió M05b').toBe(200);

    // 5 · Reinstalar por la infraestructura canónica. M06 vuelve VACÍO: los
    //     datos propios se borraron con el schema; M05b conserva la orden.
    await migrate();
    await seed();
    expect(
      await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'tracking'"),
      'migrate() no devolvió el schema tracking',
    ).toBe('1');
    expect(await psql('SELECT count(*) FROM tracking.order_tracking')).toBe('0');
    expect(await psql('SELECT count(*) FROM tracking.tracking_notes')).toBe('0');
    expect(
      await inventarioExacto(['core', 'cms', 'crm', 'services', 'service_orders']),
      'migrate()+seed() alteró datos ajenos a M06',
    ).toEqual(inventarioAntes);

    expect(
      Number(await psql('SELECT count(*) FROM core.audit_log')),
      'migrate()+seed() de M06 no debe añadir auditorías administrativas',
    ).toBe(auditoriaAntes + 1);

    // 6 · Reactivar: la orden vuelve a aparecer por lectura de M05b, sin copiar
    //     el seguimiento viejo. La FK física debe estar de vuelta.
    await cambiarModulo(page, 'tracking', 'Activar');
    expect(await capacidadesActivas(page)).toContain('tracking');
    expect(await fkTrackingAServiceOrders()).toEqual([
      'tracking.order_tracking->service_orders.service_orders',
    ]);

    expect(await menuTieneSeguimiento(page), 'reactivar M06 no devolvió su menú').toBe(true);
    expect(await rutaSeguimientoViva(page), 'reactivar M06 no devolvió su ruta').toBe(true);

    const boardReinstalado = await board(page.request);
    const cardReinstalada = cardOf(boardReinstalado, order.serviceOrderId);
    expect(cardReinstalada, 'M06 reinstalado ya no lee la orden conservada en M05b').toBeDefined();
    expect(cardReinstalada?.visibleCode).toBe(order.visibleCode);
    expect(cardReinstalada?.boardPriority, 'M06 recreó una prioridad que debía haberse borrado').toBeNull();
    expect(cardReinstalada?.pinned, 'M06 recreó el pin que debía haberse borrado').toBe(false);
    expect(cardReinstalada?.internalDueAt, 'M06 recreó el plazo que debía haberse borrado').toBeNull();

    // Y funciona después de reinstalar: materializa de nuevo su fila lazy.
    const writeAfterReinstall = await page.request.put(
      `/api/admin/tracking/orders/${order.serviceOrderId}/priority`,
      {
        headers: await csrf(page.request),
        data: { boardPriority: 1, pinned: false, orderedPeerIds: null },
      },
    );
    expect(
      writeAfterReinstall.ok(),
      `M06 reinstalado no vuelve a escribir: ${writeAfterReinstall.status()} ${await writeAfterReinstall.text()}`,
    ).toBe(true);
    expect(await psql('SELECT count(*) FROM tracking.order_tracking')).toBe('1');
  } finally {
    // Se deja el grafo como lo dejó global-setup/M05a-CICLO: los tres módulos
    // migrados pero inactivos. La limpieza no borra datos ajenos.
    await restaurarActivacion(page, iniciales);
  }

  expect([...(await capacidadesActivas(page))].sort()).toEqual(iniciales);
});
