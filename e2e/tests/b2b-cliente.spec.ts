import type { APIRequestContext, Browser } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { psql } from '../setup/docker.js';
import { FRONTEND_URL } from '../setup/env.js';

/**
 * **Las cuatro rutas de cliente de M07, por API directa.**
 *
 * Esconder un botón no cierra un endpoint, así que nada de esto pasa por
 * pantalla: se ejercen las rutas (`SolicitudesClienteEndpoints.cs:21-48`) con
 * sesión y sin ella, con token CSRF y sin él, y con la cuenta de otro.
 *
 * Cubre los criterios 1.1–1.10 y 2.6–2.7 de
 * `docs/modules/b2b/PLAN-DE-PRUEBAS-M07.md`.
 *
 * **Por qué este archivo no existía.** Hasta el 5 de octubre de 2026 el arnés
 * no migraba `Sillar.Modules.B2B` ni activaba `b2b`, así que la etapa e2e podía
 * estar entera en verde **sin que M07 existiera en el escenario**: ninguna de
 * estas rutas estaba montada y nadie lo notaba. Es la C9 de
 * `ESCALADAS-M07.md:202`.
 *
 * **Regla 1 del plan, aplicada en cada rechazo:** «toda afirmación de "no se
 * creó nada" se comprueba contra la base, contando filas antes y después». Un
 * 401 no demuestra que no se haya escrito.
 *
 * **Una cuenta nueva por grupo de casos, y no es higiene:** el cupo es de 5
 * solicitudes por hora **y por cuenta** (`LimitePorCuenta.cs:16`), así que
 * reutilizar una cuenta haría que una prueba fallara por el cupo gastado de
 * otra.
 */

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


interface Cliente {
  api: APIRequestContext;
  csrf: Record<string, string>;
  customerId: string;
  cerrar: () => Promise<void>;
}

/** Cuántas filas hay en las dos tablas de solicitudes. */
async function filas(): Promise<string> {
  return psql(`
    SELECT (SELECT count(*) FROM b2b.special_order_leads)
         + (SELECT count(*) FROM b2b.institution_requests)
  `);
}

/**
 * Registra y abre sesión de una cuenta nueva, en su propio contexto.
 *
 * Se devuelve también el `customer_id` que dice el propio API: es lo que
 * permite comprobar (1.10) que la fila queda con el de la **sesión** y no con
 * el que mande el cuerpo.
 */
async function nuevoCliente(browser: Browser, quien: string): Promise<Cliente> {
  const contexto = await browser.newContext({ baseURL: FRONTEND_URL });
  const api = contexto.request;
  const email = `${quien}-${Date.now()}-${Math.random().toString(16).slice(2)}@sillar.test`;

  const registro = await api.post('/api/customer/auth/register', {
    headers: MISMO_ORIGEN,
    data: { fullName: 'Cliente De Solicitudes', email, password: CLAVE_CLIENTE, phone: null },
  });
  expect(registro.ok(), `registrar cliente: ${registro.status()} ${await registro.text()}`).toBe(true);

  const entrada = await api.post('/api/customer/auth/login', {
    headers: MISMO_ORIGEN,
    data: { email, password: CLAVE_CLIENTE },
  });
  expect(entrada.ok(), `entrar como cliente: ${entrada.status()} ${await entrada.text()}`).toBe(true);

  const cuerpo = (await entrada.json()) as {
    customer: { customerId: string };
    csrfToken: string;
  };

  return {
    api,
    csrf: { 'X-CSRF-Token': cuerpo.csrfToken },
    customerId: cuerpo.customer.customerId,
    cerrar: () => contexto.close(),
  };
}

/** Un cuerpo válido de solicitud de volumen, con la descripción que se le pase. */
function volumen(descripcion: string) {
  return {
    institutionName: 'Colegio San Martín',
    institutionDocument: null,
    contactPerson: null,
    description: descripcion,
    quantity: 120,
    eventDate: null,
  };
}

// ---------------------------------------------------------------------------
// 1.1 – 1.4 · Sin sesión no se entra, y no se escribe
// ---------------------------------------------------------------------------

test('Sin sesión de cliente, las cuatro rutas dan 401 y no dejan ninguna fila', async ({
  browser,
}) => {
  const anonimo = await browser.newContext({ baseURL: FRONTEND_URL });
  const antes = await filas();

  // Las dos escrituras (1.1 y 1.2) y las dos lecturas (1.3 y 1.4).
  const casos = [
    {
      nombre: '1.1 personalización',
      ejecutar: () =>
        anonimo.request.post('/api/b2b/special-orders', {
          data: {
            productId: '00000000-0000-7000-8000-0000000000a1',
            description: 'Sin sesión',
            quantity: 1,
            neededBy: null,
          },
        }),
    },
    {
      nombre: '1.2 volumen',
      ejecutar: () =>
        anonimo.request.post('/api/b2b/institution-requests', {
          data: volumen('Sin sesión'),
        }),
    },
    { nombre: '1.3 solicitudes propias', ejecutar: () => anonimo.request.get('/api/b2b/my-requests') },
    { nombre: '1.4 cotización', ejecutar: () => anonimo.request.get('/api/b2b/quotes/C-2026-0001') },
  ];

  for (const caso of casos) {
    const respuesta = await caso.ejecutar();
    expect(
      respuesta.status(),
      `${caso.nombre} sin sesión debería ser 401 y fue ${respuesta.status()}: ${await respuesta.text()}`,
    ).toBe(401);
  }

  expect(await filas(), 'un 401 dejó escrita una solicitud').toBe(antes);

  await anonimo.close();
});

// ---------------------------------------------------------------------------
// 1.5 y 1.6 · Las dos poblaciones no se mezclan
// ---------------------------------------------------------------------------

test('La cookie del panel no sirve como cliente, y la de cliente no abre el panel', async ({
  page,
  browser,
}) => {
  // 1.5 · Personal contra las rutas de cliente.
  await loginAsE2eAdmin(page);
  const cookiesPanel = await page.context().cookies();
  expect(
    cookiesPanel.some((c) => c.name === 'sillar_panel'),
    'la sesión de panel no dejó su cookie',
  ).toBe(true);

  for (const ruta of ['/api/b2b/my-requests', '/api/b2b/quotes/C-2026-0001']) {
    const respuesta = await page.request.get(ruta);
    expect(
      respuesta.status(),
      `${ruta} con cookie de panel debería ser 401`,
    ).toBe(401);
  }

  // 1.6 · Cliente contra las rutas del panel. Se recorre la tabla entera, no
  //       una ruta de muestra.
  const cliente = await nuevoCliente(browser, 'aislamiento');

  for (const ruta of [
    '/api/admin/b2b/special-orders',
    '/api/admin/b2b/institution-requests',
    '/api/admin/b2b/quotes',
    '/api/admin/b2b/catalog/products?q=cord',
    '/api/admin/b2b/catalog/items?q=cord',
  ]) {
    const respuesta = await cliente.api.get(ruta);
    expect(
      respuesta.status(),
      `${ruta} con cookie de cliente debería ser 401 y fue ${respuesta.status()}`,
    ).toBe(401);
  }

  await cliente.cerrar();
});

// ---------------------------------------------------------------------------
// 1.7 · CSRF
// ---------------------------------------------------------------------------

test('Con sesión y sin token CSRF, crear da 403 y no deja fila', async ({ browser }) => {
  const cliente = await nuevoCliente(browser, 'csrf');
  const antes = await filas();

  const sinToken = await cliente.api.post('/api/b2b/institution-requests', {
    data: volumen('Sin token CSRF'),
  });

  expect(
    sinToken.status(),
    `sin CSRF debería ser 403 y fue ${sinToken.status()}: ${await sinToken.text()}`,
  ).toBe(403);
  expect(await filas(), 'un 403 de CSRF dejó escrita la solicitud').toBe(antes);

  // La mitad positiva, y es obligatoria: sin ella el 403 de arriba se
  // cumpliría igual con la ruta simplemente rota.
  const conToken = await cliente.api.post('/api/b2b/institution-requests', {
    headers: cliente.csrf,
    data: volumen('Con token CSRF'),
  });

  expect(
    conToken.status(),
    `con CSRF debería crearse y fue ${conToken.status()}: ${await conToken.text()}`,
  ).toBe(201);

  await cliente.cerrar();
});

// ---------------------------------------------------------------------------
// 1.8, 1.9 y 1.10 · Lo que una respuesta de cliente no puede contener
// ---------------------------------------------------------------------------

test('La cotización de otro cliente responde igual que una inexistente, y sin notas internas', async ({
  page,
  browser,
}) => {
  test.setTimeout(120_000);

  const dueno = await nuevoCliente(browser, 'dueno');
  const ajeno = await nuevoCliente(browser, 'ajeno');

  // El dueño pide, y **el cuerpo manda otro `customerId` a propósito** (1.10):
  // la fila tiene que quedar con el de la sesión.
  const creada = await dueno.api.post('/api/b2b/institution-requests', {
    headers: dueno.csrf,
    data: { ...volumen('Cordones para el desfile'), customerId: ajeno.customerId },
  });
  expect(creada.status(), `crear: ${await creada.text()}`).toBe(201);
  const solicitud = (await creada.json()) as { requestId: number };

  expect(
    await psql(
      `SELECT customer_id FROM b2b.institution_requests WHERE institution_request_id = ${solicitud.requestId}`,
    ),
    'la fila quedó con el customerId del cuerpo en vez del de la sesión',
  ).toBe(dueno.customerId);

  // El personal cotiza y **escribe una nota interna con un marcador único**
  // (1.9): lo que se busca después es ese marcador, en el cuerpo crudo.
  await loginAsE2eAdmin(page);
  const { csrfToken } = (await (await page.request.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };
  const cabeceras = { 'X-CSRF-Token': csrfToken };

  const nota = `MARCADOR-NOTA-${Date.now().toString(16)}`;
  const notas = await page.request.put(
    `/api/admin/b2b/institution-requests/${solicitud.requestId}/notes`,
    { headers: cabeceras, data: { staffNotes: nota } },
  );
  expect(notas.ok(), `escribir notas: ${await notas.text()}`).toBe(true);

  const cotizada = await page.request.post('/api/admin/b2b/quotes', {
    headers: cabeceras,
    data: {
      origen: 'volumen',
      solicitudId: solicitud.requestId,
      lines: [{ itemId: null, description: 'Cordón para desfile', quantity: 120, unitPrice: 3.5 }],
    },
  });
  expect(cotizada.ok(), `cotizar: ${await cotizada.text()}`).toBe(true);
  const panel = (await cotizada.json()) as {
    detalle: { cotizacion: { quoteNumber: string } };
  };
  const numero = panel.detalle.cotizacion.quoteNumber;

  // 1.8 · Mismo estado **y mismo cuerpo** para la ajena y la inexistente: la
  //       ruta no puede revelar qué números existen.
  const ajena = await ajeno.api.get(`/api/b2b/quotes/${numero}`);
  const inexistente = await ajeno.api.get('/api/b2b/quotes/C-1999-9999');

  expect(ajena.status(), 'la cotización de otro cliente no dio 404').toBe(404);
  expect(inexistente.status(), 'una cotización inexistente no dio 404').toBe(404);

  // **Se compara el cuerpo sin el `traceId`.** Ese campo es distinto en cada
  // petición por diseño —es el identificador de la traza—, así que incluirlo
  // hacía que la prueba fallara por algo que no tiene nada que ver con lo que
  // afirma. Lo que no puede diferir es todo lo demás: si una ruta revelara que
  // el número existe, la diferencia estaría en el título, el estado o el
  // detalle.
  const sinTraza = (cuerpo: string) => cuerpo.replace(/"traceId":"[^"]*"/, '"traceId":"—"');

  expect(
    sinTraza(await ajena.text()),
    'la respuesta de una cotización ajena se distingue de la de una inexistente',
  ).toBe(sinTraza(await inexistente.text()));

  // 1.9 · El marcador no aparece en ninguna de las respuestas públicas.
  const publicas = [
    await dueno.api.get('/api/b2b/my-requests'),
    await dueno.api.get(`/api/b2b/quotes/${numero}`),
  ];

  for (const respuesta of publicas) {
    expect(
      await respuesta.text(),
      'una respuesta de cliente trae las notas internas del personal',
    ).not.toContain(nota);
  }

  await dueno.cerrar();
  await ajeno.cerrar();
});

// ---------------------------------------------------------------------------
// 2.6 y 2.7 · El cupo por cuenta
// ---------------------------------------------------------------------------

test('Pasado el cupo, la cuenta recibe 429 con Retry-After, una frase sin «error», y no deja fila', async ({
  browser,
}) => {
  test.setTimeout(120_000);

  const cliente = await nuevoCliente(browser, 'cupo');

  // El máximo por defecto son 5 por hora y por cuenta
  // (`LimitePorCuenta.cs:16`). **No se escribe el 5 aquí**: se gasta cupo
  // hasta que la API diga que no, con un techo generoso que solo existe para
  // que la prueba no sea infinita si la barrera desapareciera.
  let agotado: Awaited<ReturnType<typeof cliente.api.post>> | null = null;
  let aceptadas = 0;

  for (let intento = 1; intento <= 12 && agotado === null; intento += 1) {
    const respuesta = await cliente.api.post('/api/b2b/institution-requests', {
      headers: cliente.csrf,
      data: volumen(`Cordones, intento ${intento}`),
    });

    if (respuesta.status() === 429) {
      agotado = respuesta;
    } else {
      expect(
        respuesta.status(),
        `el intento ${intento} debería crearse o agotar el cupo, y dio ${respuesta.status()}: ${await respuesta.text()}`,
      ).toBe(201);
      aceptadas += 1;
    }
  }

  expect(
    agotado,
    `tras ${aceptadas} solicitudes seguidas de la misma cuenta, el cupo nunca dijo no`,
  ).not.toBeNull();

  // **El rechazo no escribe.** Se cuenta la fila por fila de esta cuenta, no
  // el total: otra prueba puede estar creando las suyas.
  const suyas = await psql(
    `SELECT count(*) FROM b2b.institution_requests WHERE customer_id = '${cliente.customerId}'`,
  );
  expect(Number(suyas), 'el 429 dejó escrita la solicitud rechazada').toBe(aceptadas);

  // Dice cuándo volver, y lo dice en la cabecera estándar.
  const retry = agotado!.headers()['retry-after'];
  expect(retry, 'el 429 no trae Retry-After').toBeTruthy();
  expect(Number(retry), 'Retry-After no es un número de segundos').toBeGreaterThan(0);

  // 2.7 · Y la frase es de persona: no dice «error» ni ofrece «Aceptar»
  //       (regla de `CLAUDE.md`, Frontend).
  const texto = await agotado!.text();
  expect(texto.toLowerCase(), 'la frase del cupo dice «error»').not.toContain('error');
  expect(texto, 'la frase del cupo ofrece «Aceptar»').not.toContain('Aceptar');
  expect(texto.length, 'el 429 llegó sin explicación').toBeGreaterThan(20);

  await cliente.cerrar();
});
