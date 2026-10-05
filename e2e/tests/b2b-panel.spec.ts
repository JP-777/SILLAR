import type { APIRequestContext, Browser, Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { psql } from '../setup/docker.js';
import { API_URL, FRONTEND_URL } from '../setup/env.js';

/**
 * **La tabla de permisos del panel de M07, aplicada de verdad.**
 *
 * El `SPEC.md` §6 dice: mínimo `editor` para todo, y **solo `admin`** para las
 * tres bajas lógicas y el registro de pago. En el código eso son
 * `BandejaAdminEndpoints.cs:25` (el grupo) y `:44`, `:61`, `:86`, `:90` (las
 * cuatro excepciones).
 *
 * Cubre 3.1–3.6 de `docs/modules/b2b/PLAN-DE-PRUEBAS-M07.md`.
 *
 * **Dos reglas que este archivo no se salta:**
 *
 * 1. **La mitad positiva es obligatoria.** Comprobar que un `editor` recibe 403
 *    en cuatro rutas pasaría igual si esas cuatro rutas estuvieran simplemente
 *    rotas. Cada 403 va acompañado de la misma ruta funcionando para `admin`.
 * 2. **Tras cada rechazo se mira la base.** Un 403 que ya hubiera escrito sería
 *    igual de malo y el código de estado no lo distingue (regla 1 del plan).
 *
 * **El inventario de rutas se contrasta con Swagger (3.5)**, no con la lista de
 * aquí: una lista escrita a mano envejece el día que alguien añade un endpoint
 * y se olvida de esta prueba, y entonces la prueba deja de cubrir lo que dice
 * cubrir sin ponerse roja.
 */

const CLAVE_EDITOR = 'Retama-Quilla-47xW';
const CLAVE_CLIENTE = 'bosque-cobalto-dieciseis-lunas';

/**
 * La señal de mismo origen que el navegador manda y `request` no.
 *
 * `AnonymousCsrfEndpointFilter` (M04) exige `Sec-Fetch-Site: same-origin` —o un
 * `Origin` que coincida con el `Host` que ve la API, que tras el proxy de Vite no
 * coincide— en las escrituras públicas anteriores a la sesión: registro y login.
 * `APIRequestContext` no manda Fetch Metadata, así que se pone a mano. Es la
 * misma razón por la que `crm-auth-isolation.spec.ts:6` lo hace con
 * `page.evaluate`: dentro de la página la cabecera la pone el navegador.
 */
const MISMO_ORIGEN = { 'Sec-Fetch-Site': 'same-origin' } as const;


interface Ids {
  personalizacion: number;
  volumen: number;
  cotizacion: number;
  numero: string;
}

async function csrf(api: APIRequestContext): Promise<Record<string, string>> {
  const { csrfToken } = (await (await api.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };
  return { 'X-CSRF-Token': csrfToken };
}

/**
 * Crea un producto activo y publicado, y devuelve su identificador.
 *
 * **Hace falta porque el catálogo del arnés arranca vacío a propósito**: el seed
 * de M01 no trae contenido de negocio (SPEC de M01 §6.9), y eso es justo lo que
 * hace observable el estado vacío en `aa-vacios.spec.ts`. Una personalización de
 * M07 necesita un producto activo y publicado, así que esta prueba se lo
 * siembra — misma lección que `zz-desmontaje.spec.ts:109`: una prueba que solo
 * funciona acompañada no dice qué falla cuando falla.
 *
 * `is_public` vale `true` por omisión (`Product.cs:86`), así que el producto
 * nace publicado sin pedirlo.
 *
 * Al crearlo se comprueba además que **el selector de M01 que usa el panel de
 * M07 lo encuentra**: si ese contrato se rompiera, el fallo lo diría aquí en vez
 * de más abajo con un uuid que parece bueno.
 */
async function productoActivo(page: Page): Promise<string> {
  const sello = `${Date.now()}`;
  const creado = await page.request.post('/api/admin/catalog/products', {
    headers: await csrf(page.request),
    data: {
      name: `Cordon para desfile M07 ${sello}`,
      slug: `cordon-desfile-m07-${sello}`,
      shortDescription: null,
      description: null,
      primaryCategoryId: null,
      categoryIds: [],
      brandId: null,
      listPrice: 4.5,
      saleUnit: 'unidad',
      variantLabel: null,
      code: null,
      barcode: null,
    },
  });

  expect(creado.status(), `crear el producto de M01: ${await creado.text()}`).toBe(201);
  const producto = (await creado.json()) as { id: string };

  const encontrados = (await (
    await page.request.get('/api/admin/b2b/catalog/products?q=cordon')
  ).json()) as { productId: string }[];

  expect(
    encontrados.map((p) => p.productId),
    'el selector de M01 que usa el panel de M07 no encuentra el producto recién creado',
  ).toContain(producto.id);

  return producto.id;
}

/** Las cuatro rutas que exigen `admin`, con su método y su cuerpo si lo necesita. */
function soloAdmin(ids: Ids) {
  return [
    { que: 'baja de personalización', metodo: 'delete' as const, ruta: `/api/admin/b2b/special-orders/${ids.personalizacion}` },
    { que: 'baja de volumen', metodo: 'delete' as const, ruta: `/api/admin/b2b/institution-requests/${ids.volumen}` },
    { que: 'baja de cotización', metodo: 'delete' as const, ruta: `/api/admin/b2b/quotes/${ids.cotizacion}` },
    {
      que: 'registro de pago',
      metodo: 'put' as const,
      ruta: `/api/admin/b2b/quotes/${ids.cotizacion}/payment`,
      cuerpo: { paymentMethod: 'efectivo', paymentReference: null },
    },
  ];
}

/**
 * Deja una solicitud de cada clase y una cotización, **todo por el producto**.
 *
 * El cliente se registra y pide de verdad (`/api/customer/auth/*` y
 * `/api/b2b/*`); el personal cotiza de verdad (`POST /api/admin/b2b/quotes`).
 * Nada entra por `INSERT`: lo que esta prueba afirma es sobre el panel, y el
 * panel es quien crea la cotización.
 */
async function sembrar(page: Page, browser: Browser): Promise<Ids> {
  const contexto = await browser.newContext({ baseURL: FRONTEND_URL });
  const cliente = contexto.request;
  const email = `panel-${Date.now()}-${Math.random().toString(16).slice(2)}@sillar.test`;

  const registro = await cliente.post('/api/customer/auth/register', {
    headers: MISMO_ORIGEN,
    data: { fullName: 'Rosa Mamani Panel', email, password: CLAVE_CLIENTE, phone: null },
  });
  expect(registro.ok(), `registrar cliente: ${await registro.text()}`).toBe(true);

  const entrada = await cliente.post('/api/customer/auth/login', {
    headers: MISMO_ORIGEN,
    data: { email, password: CLAVE_CLIENTE },
  });
  expect(entrada.ok(), `entrar como cliente: ${await entrada.text()}`).toBe(true);
  const sesion = (await entrada.json()) as { csrfToken: string };
  const csrfCliente = { 'X-CSRF-Token': sesion.csrfToken };

  const pedidaVolumen = await cliente.post('/api/b2b/institution-requests', {
    headers: csrfCliente,
    data: {
      institutionName: 'Colegio San Martín (panel)',
      institutionDocument: null,
      contactPerson: null,
      description: '200 cordones para el desfile',
      quantity: 200,
      eventDate: null,
    },
  });
  expect(pedidaVolumen.status(), `pedir volumen: ${await pedidaVolumen.text()}`).toBe(201);
  const volumen = (await pedidaVolumen.json()) as { requestId: number };

  const cabeceras = await csrf(page.request);
  const productoId = await productoActivo(page);

  const pedidaPersonalizacion = await cliente.post('/api/b2b/special-orders', {
    headers: csrfCliente,
    data: {
      productId: productoId,
      description: 'Igual pero con el escudo grabado',
      quantity: 10,
      neededBy: null,
    },
  });
  expect(
    pedidaPersonalizacion.status(),
    `pedir personalización: ${await pedidaPersonalizacion.text()}`,
  ).toBe(201);
  const personalizacion = (await pedidaPersonalizacion.json()) as { requestId: number };

  const cotizada = await page.request.post('/api/admin/b2b/quotes', {
    headers: cabeceras,
    data: {
      origen: 'volumen',
      solicitudId: volumen.requestId,
      lines: [{ itemId: null, description: 'Cordón para desfile', quantity: 200, unitPrice: 3.5 }],
    },
  });
  expect(cotizada.ok(), `cotizar: ${await cotizada.text()}`).toBe(true);
  const panel = (await cotizada.json()) as {
    detalle: { cotizacion: { id: number; quoteNumber: string } };
  };

  await contexto.close();

  return {
    personalizacion: personalizacion.requestId,
    volumen: volumen.requestId,
    cotizacion: panel.detalle.cotizacion.id,
    numero: panel.detalle.cotizacion.quoteNumber,
  };
}

/** Crea un `editor` nuevo y devuelve su contexto con la sesión abierta. */
async function entrarComoEditor(page: Page, browser: Browser): Promise<{
  api: APIRequestContext;
  cerrar: () => Promise<void>;
}> {
  const sello = `${Date.now()}`;
  const correo = `encargada.b2b.${sello}@sillar.test`;

  const alta = await page.request.post('/api/admin/users', {
    headers: await csrf(page.request),
    data: {
      fullName: `Encargada De Solicitudes ${sello}`,
      // Sin parecido con la contraseña: `PasswordPolicy.cs` la rechaza si el
      // nombre o el correo aparecen dentro.
      email: correo,
      password: CLAVE_EDITOR,
      role: 'editor',
      phone: null,
    },
  });
  expect(alta.ok(), `crear el editor: ${alta.status()} ${await alta.text()}`).toBe(true);

  // Contexto propio: si entrara con `page.request` se perdería la sesión de
  // `super_admin`, que esta misma prueba necesita para la mitad positiva.
  const contexto = await browser.newContext({ baseURL: FRONTEND_URL });
  const entrada = await contexto.request.post('/api/admin/auth/login', {
    data: { email: correo, password: CLAVE_EDITOR },
  });
  expect(entrada.ok(), `entrar como editor: ${await entrada.text()}`).toBe(true);

  return { api: contexto.request, cerrar: () => contexto.close() };
}

/** Lo que hay que mirar tras un rechazo: bajas lógicas y pagos. */
async function estado(): Promise<string> {
  return psql(`
    SELECT (SELECT count(*) FROM b2b.quotes WHERE is_active)
        || '/' || (SELECT count(*) FROM b2b.institution_requests WHERE is_active)
        || '/' || (SELECT count(*) FROM b2b.special_order_leads WHERE is_active)
        || '/' || (SELECT count(*) FROM b2b.quotes WHERE paid_at IS NOT NULL)
  `);
}

// ---------------------------------------------------------------------------
// 3.1 y 3.2 · La tabla de permisos
// ---------------------------------------------------------------------------

test('Un editor lee y escribe lo suyo, y las cuatro rutas de admin le dan 403 sin cambiar nada', async ({
  page,
  browser,
}) => {
  test.setTimeout(180_000);
  await loginAsE2eAdmin(page);
  const ids = await sembrar(page, browser);
  const editor = await entrarComoEditor(page, browser);

  // --- 1 · Lo que un editor sí puede --------------------------------------
  for (const ruta of [
    '/api/admin/b2b/special-orders',
    '/api/admin/b2b/institution-requests',
    '/api/admin/b2b/quotes',
  ]) {
    const lectura = await editor.api.get(ruta);
    expect(lectura.status(), `un editor no pudo leer ${ruta}`).toBe(200);
  }

  const escritura = await editor.api.put(
    `/api/admin/b2b/institution-requests/${ids.volumen}/notes`,
    { headers: await csrf(editor.api), data: { staffNotes: 'MARCADOR-EDITOR-4d1f' } },
  );
  expect(escritura.status(), `un editor no pudo escribir notas: ${await escritura.text()}`).toBe(200);

  // --- 2 · Lo que no puede, y la base lo confirma (3.2) -------------------
  const antes = await estado();
  const cabecerasEditor = await csrf(editor.api);

  for (const caso of soloAdmin(ids)) {
    const respuesta =
      caso.metodo === 'delete'
        ? await editor.api.delete(caso.ruta, { headers: cabecerasEditor })
        : await editor.api.put(caso.ruta, { headers: cabecerasEditor, data: caso.cuerpo });

    expect(
      respuesta.status(),
      `${caso.que} debería dar 403 a un editor y dio ${respuesta.status()}: ${await respuesta.text()}`,
    ).toBe(403);
  }

  expect(
    await estado(),
    'un 403 del editor cambió el estado: alguna baja o algún pago quedó escrito',
  ).toBe(antes);

  // --- 3 · La mitad positiva: el admin sí pasa la puerta del rol ----------
  //
  // La cotización está en borrador, así que el pago se rechaza por **estado**
  // —409— y no por permiso. Que la respuesta sea 409 y no 403 es justamente lo
  // que demuestra que el `admin` atravesó la autorización: son dos puertas
  // distintas y dan códigos distintos.
  const pago = await page.request.put(`/api/admin/b2b/quotes/${ids.cotizacion}/payment`, {
    headers: await csrf(page.request),
    data: { paymentMethod: 'efectivo', paymentReference: null },
  });
  expect(
    pago.status(),
    `el admin recibió ${pago.status()} en la ruta de pago: ${await pago.text()}`,
  ).toBe(409);

  // Y una baja que sí puede hacer, de verdad: la personalización.
  const baja = await page.request.delete(
    `/api/admin/b2b/special-orders/${ids.personalizacion}`,
    { headers: await csrf(page.request) },
  );
  expect(baja.ok(), `el admin no pudo dar de baja: ${baja.status()} ${await baja.text()}`).toBe(true);
  expect(
    await psql(
      `SELECT is_active FROM b2b.special_order_leads WHERE special_order_lead_id = ${ids.personalizacion}`,
    ),
    'la baja del admin no llegó a la fila',
  ).toBe('f');

  await editor.cerrar();
});

// ---------------------------------------------------------------------------
// 3.3 · CSRF en toda escritura del panel
// ---------------------------------------------------------------------------

test('Toda escritura del panel sin token CSRF da 403 y no cambia nada', async ({
  page,
  browser,
}) => {
  test.setTimeout(120_000);
  await loginAsE2eAdmin(page);
  const ids = await sembrar(page, browser);

  const antes = await psql(
    `SELECT coalesce(staff_notes, '') FROM b2b.institution_requests WHERE institution_request_id = ${ids.volumen}`,
  );

  const escrituras = [
    {
      que: 'notas',
      hacer: () =>
        page.request.put(`/api/admin/b2b/institution-requests/${ids.volumen}/notes`, {
          data: { staffNotes: 'SIN-CSRF-NO-DEBERIA-ENTRAR' },
        }),
    },
    {
      que: 'estado',
      hacer: () =>
        page.request.put(`/api/admin/b2b/institution-requests/${ids.volumen}/status`, {
          data: { status: 'en_revision' },
        }),
    },
    {
      que: 'enviar cotización',
      hacer: () => page.request.put(`/api/admin/b2b/quotes/${ids.cotizacion}/send`),
    },
    {
      que: 'baja de cotización',
      hacer: () => page.request.delete(`/api/admin/b2b/quotes/${ids.cotizacion}`),
    },
  ];

  for (const caso of escrituras) {
    const respuesta = await caso.hacer();
    expect(
      respuesta.status(),
      `${caso.que} sin CSRF debería ser 403 y fue ${respuesta.status()}`,
    ).toBe(403);
  }

  expect(
    await psql(
      `SELECT coalesce(staff_notes, '') FROM b2b.institution_requests WHERE institution_request_id = ${ids.volumen}`,
    ),
    'una escritura sin CSRF llegó a la fila',
  ).toBe(antes);

  // `is_active::int` y no el booleano a secas: `psql -tA` imprime `f` cuando la
  // columna va sola y `true` cuando se concatena con `||`, y depender de esa
  // diferencia es pedir un rojo que no significa nada.
  expect(
    await psql(
      `SELECT status || '/' || is_active::int FROM b2b.quotes WHERE quote_id = ${ids.cotizacion}`,
    ),
    'una escritura sin CSRF cambió la cotización',
  ).toBe('borrador/1');
});

// ---------------------------------------------------------------------------
// 3.4 · Auditoría que nombra la fila
// ---------------------------------------------------------------------------

test('Toda escritura del panel deja auditoría b2b, y el resumen nombra la cotización', async ({
  page,
  browser,
}) => {
  test.setTimeout(120_000);
  await loginAsE2eAdmin(page);
  const ids = await sembrar(page, browser);

  const enviada = await page.request.put(`/api/admin/b2b/quotes/${ids.cotizacion}/send`, {
    headers: await csrf(page.request),
  });
  expect(enviada.ok(), `enviar: ${await enviada.text()}`).toBe(true);

  const auditoria = (await (
    await page.request.get(
      `/api/admin/audit?moduleCode=b2b&entityType=quote&entityId=${ids.cotizacion}`,
    )
  ).json()) as { items: { moduleCode: string; summary: string | null }[] };

  expect(
    auditoria.items.length,
    'las escrituras del panel de M07 no dejaron auditoría',
  ).toBeGreaterThan(0);

  for (const fila of auditoria.items) {
    expect(fila.moduleCode, 'una fila de auditoría de M07 no lleva su módulo').toBe('b2b');
  }

  // **Nombra la fila, no la clase** (`ANTES-DE-EMPEZAR-UN-MODULO.md` §5). Y se
  // busca el número visible **que devolvió la API**, no un formato escrito
  // aquí: así la prueba no fija la decisión de E5.
  const resumenes = auditoria.items.map((f) => f.summary ?? '').join('\n');
  expect(
    resumenes,
    `ningún resumen de auditoría nombra la cotización ${ids.numero}`,
  ).toContain(ids.numero);
});

// ---------------------------------------------------------------------------
// 3.5 · El inventario de rutas, contra Swagger
// ---------------------------------------------------------------------------

test('Las rutas de M07 que esta suite ejerce y las de Swagger son el mismo conjunto', async ({
  page,
}) => {
  await loginAsE2eAdmin(page);

  // Contra la API directamente: el proxy de Vite solo reenvía `/api` y
  // `/media`, así que pedir Swagger por el frontend devuelve el index.html.
  const respuesta = await page.request.get(`${API_URL}/swagger/v1/swagger.json`);
  expect(respuesta.ok(), `Swagger no responde: ${respuesta.status()}`).toBe(true);

  const doc = (await respuesta.json()) as { paths: Record<string, Record<string, unknown>> };

  const deM07 = Object.keys(doc.paths)
    .filter((ruta) => ruta.startsWith('/api/b2b') || ruta.startsWith('/api/admin/b2b'))
    .sort();

  // Los 20 *paths* de M07: 4 de cliente y 16 de panel —`path`, no operación:
  // `/quotes/{id}` es uno solo y lo comparten GET, PUT y DELETE—. **Se escriben
  // aquí a propósito y Swagger es el contraste**: si alguien añade un endpoint y no lo
  // añade a esta lista, la comparación de abajo falla y hay que venir a
  // decidir qué permiso le toca en la prueba 3.1. Ese es el punto — que no se
  // pueda añadir una ruta sin pasar por aquí.
  const esperadas = [
    '/api/admin/b2b/catalog/items',
    '/api/admin/b2b/catalog/products',
    '/api/admin/b2b/institution-requests',
    '/api/admin/b2b/institution-requests/{id}',
    '/api/admin/b2b/institution-requests/{id}/notes',
    '/api/admin/b2b/institution-requests/{id}/status',
    '/api/admin/b2b/quotes',
    '/api/admin/b2b/quotes/{id}',
    '/api/admin/b2b/quotes/{id}/approve',
    '/api/admin/b2b/quotes/{id}/payment',
    '/api/admin/b2b/quotes/{id}/send',
    '/api/admin/b2b/special-orders',
    '/api/admin/b2b/special-orders/{id}',
    '/api/admin/b2b/special-orders/{id}/notes',
    '/api/admin/b2b/special-orders/{id}/relink',
    '/api/admin/b2b/special-orders/{id}/status',
    '/api/b2b/institution-requests',
    '/api/b2b/my-requests',
    '/api/b2b/quotes/{quoteNumber}',
    '/api/b2b/special-orders',
  ].sort();

  expect(
    deM07,
    'el conjunto de rutas de M07 en Swagger cambió: si has añadido o quitado un endpoint, decide su permiso en la prueba 3.1 y actualiza esta lista',
  ).toEqual(esperadas);

  // Y cada operación lleva resumen: una ruta sin describir no es visible en
  // Swagger aunque aparezca (regla 6 de `CLAUDE.md`).
  const sinResumen: string[] = [];

  for (const ruta of deM07) {
    for (const [metodo, operacion] of Object.entries(doc.paths[ruta])) {
      const descrita = operacion as { summary?: string };
      if (!descrita.summary) {
        sinResumen.push(`${metodo.toUpperCase()} ${ruta}`);
      }
    }
  }

  expect(sinResumen, `operaciones de M07 sin resumen en Swagger:\n${sinResumen.join('\n')}`).toEqual([]);
});

// ---------------------------------------------------------------------------
// 3.6 · Una cotización enviada no admite edición de líneas
// ---------------------------------------------------------------------------

test('Una cotización enviada no admite edición de líneas, y su total no cambia', async ({
  page,
  browser,
}) => {
  test.setTimeout(120_000);
  await loginAsE2eAdmin(page);
  const ids = await sembrar(page, browser);
  const cabeceras = await csrf(page.request);

  // En borrador sí: la mitad positiva, otra vez, para que el 409 de después
  // signifique algo.
  const enBorrador = await page.request.put(`/api/admin/b2b/quotes/${ids.cotizacion}`, {
    headers: cabeceras,
    data: { lines: [{ itemId: null, description: 'Cordón, precio ajustado', quantity: 200, unitPrice: 3 }] },
  });
  expect(enBorrador.ok(), `editar en borrador: ${await enBorrador.text()}`).toBe(true);

  const enviada = await page.request.put(`/api/admin/b2b/quotes/${ids.cotizacion}/send`, {
    headers: cabeceras,
  });
  expect(enviada.ok(), `enviar: ${await enviada.text()}`).toBe(true);

  const totalAntes = await psql(
    `SELECT total_amount FROM b2b.quotes WHERE quote_id = ${ids.cotizacion}`,
  );

  const intento = await page.request.put(`/api/admin/b2b/quotes/${ids.cotizacion}`, {
    headers: cabeceras,
    data: { lines: [{ itemId: null, description: 'Otra cosa', quantity: 1, unitPrice: 1 }] },
  });

  expect(
    intento.status(),
    `editar una enviada debería dar 409 y dio ${intento.status()}: ${await intento.text()}`,
  ).toBe(409);

  // La frase dice qué lo impide, no «ha ocurrido un error».
  const texto = await intento.text();
  expect(texto, 'el 409 de edición no explica qué lo impide').toContain('solo se editan en borrador');

  expect(
    await psql(`SELECT total_amount FROM b2b.quotes WHERE quote_id = ${ids.cotizacion}`),
    'el 409 de edición cambió el total de la cotización',
  ).toBe(totalAntes);
});
