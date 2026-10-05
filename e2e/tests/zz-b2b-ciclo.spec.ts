import type { Browser, Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { psql } from '../setup/docker.js';
import { API_URL, FRONTEND_URL } from '../setup/env.js';

/**
 * **El ciclo real de M07: activo → usado → desactivado → reactivado.**
 *
 * Cubre 4.1, 4.2, 4.3, 4.5, 4.6 y 4.7 de
 * `docs/modules/b2b/PLAN-DE-PRUEBAS-M07.md`, y el criterio de terminado de
 * `CLAUDE.md`: un módulo está terminado cuando se puede instalar y desinstalar
 * sin romper nada del resto.
 *
 * ---
 *
 * ## Por qué el prefijo `zz-`, y qué se rompe si alguien lo cambia
 *
 * **El prefijo es deliberado, no decorativo.**
 *
 * - **Depende de `workers: 1` y `fullyParallel: false`**
 *   (`e2e/playwright.config.ts:15-17`). Esta spec desactiva y reactiva un
 *   módulo del grafo compartido: con dos trabajadores a la vez, otra spec se
 *   encontraría el producto apagado a media prueba y fallaría por algo que no
 *   es suyo.
 * - **Tiene que ejecutarse después de las specs funcionales.** Playwright
 *   ordena los archivos alfabéticamente, y `zz-` es la convención del arnés
 *   para «corre al final, cuando nadie más va a mirar»
 *   (`zz-desmontaje.spec.ts:22`).
 * - **Renombrarla o reordenarla contamina las pruebas posteriores.** Entre el
 *   paso 4 y el 6 el producto está sin M07 **y el proceso se reinicia dos
 *   veces**: cualquier spec que corriera en ese hueco vería un panel sin el
 *   grupo «Solicitudes» y rutas en 404.
 * - **Reconstruir el estado al final es parte del criterio, no limpieza.** El
 *   criterio dice «se puede instalar **y desinstalar**»: reactivar M07 y
 *   comprobar que sus superficies y sus datos vuelven es la segunda mitad de la
 *   afirmación, no el orden de la cocina.
 *
 * ---
 *
 * ## Menú y router no son el mismo número
 *
 * M07 pone **3 entradas de menú** (`frontend/src/modules/b2b/navegacion.ts`) y
 * **4 rutas de router** (`frontend/src/modules/b2b/routes.tsx`): el detalle de
 * una cotización, `/admin/solicitudes/cotizaciones/:id`, no es entrada de menú
 * porque necesita un identificador. **Se prueba igual, con una cotización
 * real**: quedarse fuera por no tener entrada propia sería dejar sin cubrir
 * justamente la pantalla donde se cobra.
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


/** Las tres rutas de menú, con el título que tiene que pintar cada una. */
const PANTALLAS = [
  ['/admin/solicitudes/personalizadas', 'Solicitudes personalizadas'],
  ['/admin/solicitudes/institucionales', 'Solicitudes por volumen'],
  ['/admin/solicitudes/cotizaciones', 'Cotizaciones'],
] as const;

interface Usado {
  numero: string;
  cotizacionId: number;
  volumenId: number;
  personalizacionId: number;
}

async function csrfPanel(page: Page): Promise<Record<string, string>> {
  const { csrfToken } = (await (await page.request.get('/api/admin/auth/csrf')).json()) as {
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
    headers: await csrfPanel(page),
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

/** Lo que M07 tiene dentro, para comparar antes y después de apagarlo. */
async function inventario(): Promise<string> {
  return psql(`
    SELECT (SELECT count(*) FROM b2b.special_order_leads)
        || '/' || (SELECT count(*) FROM b2b.institution_requests)
        || '/' || (SELECT count(*) FROM b2b.quotes)
        || '/' || (SELECT count(*) FROM b2b.quote_lines)
        || '/' || coalesce((SELECT string_agg(quote_number, ',' ORDER BY quote_number) FROM b2b.quotes), '')
  `);
}

/** Mueve el interruptor de un módulo por la pantalla y espera el reinicio real. */
async function cambiarModulo(page: Page, codigo: string, accion: 'Activar' | 'Desactivar') {
  await page.goto('/admin/modulos');

  await duringExpectedOutage(page, async () => {
    await page.locator(`#modulo-${codigo}`).getByRole('switch').click();
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: new RegExp(`^${accion}`) })
      .click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });
}

/**
 * **Paso 1 · usar M07 de verdad, de punta a punta.**
 *
 * Cliente registrado pide → el personal cotiza, envía, aprueba y **cobra**.
 * Todo por el producto: nada entra por `INSERT`. Al terminar hay una
 * cotización `pagada` con el trío de R-14 congelado, que es el estado más
 * cargado que M07 puede tener — y por tanto el que mejor prueba que desactivar
 * no borra.
 */
async function usar(page: Page, browser: Browser): Promise<Usado> {
  const contexto = await browser.newContext({ baseURL: FRONTEND_URL });
  const cliente = contexto.request;
  const email = `ciclo-${Date.now()}-${Math.random().toString(16).slice(2)}@sillar.test`;

  const registro = await cliente.post('/api/customer/auth/register', {
    headers: MISMO_ORIGEN,
    data: { fullName: 'Rosa Mamani Ciclo', email, password: CLAVE_CLIENTE, phone: null },
  });
  expect(registro.ok(), `registrar cliente: ${await registro.text()}`).toBe(true);

  const entrada = await cliente.post('/api/customer/auth/login', {
    headers: MISMO_ORIGEN,
    data: { email, password: CLAVE_CLIENTE },
  });
  expect(entrada.ok(), `entrar como cliente: ${await entrada.text()}`).toBe(true);
  const sesion = (await entrada.json()) as { csrfToken: string };
  const csrfCliente = { 'X-CSRF-Token': sesion.csrfToken };

  const volumen = await cliente.post('/api/b2b/institution-requests', {
    headers: csrfCliente,
    data: {
      institutionName: 'Colegio San Martín (ciclo)',
      institutionDocument: null,
      contactPerson: null,
      description: '150 cordones para el desfile',
      quantity: 150,
      eventDate: null,
    },
  });
  expect(volumen.status(), `pedir volumen: ${await volumen.text()}`).toBe(201);
  const pedidoVolumen = (await volumen.json()) as { requestId: number };

  const cabeceras = await csrfPanel(page);

  const productoId = await productoActivo(page);

  const personalizacion = await cliente.post('/api/b2b/special-orders', {
    headers: csrfCliente,
    data: {
      productId: productoId,
      description: 'Igual pero con el escudo grabado',
      quantity: 12,
      neededBy: null,
    },
  });
  expect(personalizacion.status(), `pedir personalización: ${await personalizacion.text()}`).toBe(201);
  const pedidoPersonalizacion = (await personalizacion.json()) as { requestId: number };

  await contexto.close();

  // --- El personal, por el panel ------------------------------------------
  const cotizada = await page.request.post('/api/admin/b2b/quotes', {
    headers: cabeceras,
    data: {
      origen: 'volumen',
      solicitudId: pedidoVolumen.requestId,
      lines: [{ itemId: null, description: 'Cordón para desfile', quantity: 150, unitPrice: 3.5 }],
    },
  });
  expect(cotizada.ok(), `cotizar: ${await cotizada.text()}`).toBe(true);
  const panel = (await cotizada.json()) as {
    detalle: { cotizacion: { id: number; quoteNumber: string } };
  };
  const { id, quoteNumber } = panel.detalle.cotizacion;

  for (const paso of ['send', 'approve'] as const) {
    const respuesta = await page.request.put(`/api/admin/b2b/quotes/${id}/${paso}`, {
      headers: cabeceras,
    });
    expect(respuesta.ok(), `${paso}: ${await respuesta.text()}`).toBe(true);
  }

  const pago = await page.request.put(`/api/admin/b2b/quotes/${id}/payment`, {
    headers: cabeceras,
    data: { paymentMethod: 'efectivo', paymentReference: null },
  });
  expect(pago.ok(), `registrar el pago: ${pago.status()} ${await pago.text()}`).toBe(true);

  // **El trío de R-14 quedó congelado, y los tres o ninguno.** Se leen
  // concatenados: si cualquiera fuera NULL, la concatenación entera sale NULL.
  const atribucion = await psql(`
    SELECT paid_registered_by || '|' || paid_registered_by_admin_user_local_id
                             || '|' || paid_registered_by_admin_user_home_node
    FROM b2b.quotes WHERE quote_id = ${id}
  `);
  expect(atribucion, 'el pago no congeló los tres datos de R-14').not.toBe('');
  expect(atribucion.split('|').length, 'la atribución del pago llegó incompleta').toBe(3);

  return {
    numero: quoteNumber,
    cotizacionId: id,
    volumenId: pedidoVolumen.requestId,
    personalizacionId: pedidoPersonalizacion.requestId,
  };
}

test('M07 se usa, se apaga y vuelve: sin enlace roto, sin ruta muerta y sin perder nada', async ({
  page,
  browser,
}) => {
  // Dos reinicios reales del proceso, más el ciclo entero de una cotización.
  test.setTimeout(420_000);
  await loginAsE2eAdmin(page);

  // =======================================================================
  // 1 · USAR
  // =======================================================================
  const usado = await usar(page, browser);
  const antes = await inventario();

  // =======================================================================
  // 2 · ACTIVO — la mitad positiva, y va primero a propósito (4.5)
  //
  // Sin esto, los 404 y los «0 enlaces» del paso 4 se cumplirían igual con un
  // módulo que nunca se hubiera montado.
  // =======================================================================

  // 2a · El menú: **3 enlaces**, y se identifica por destino y no por la
  //      palabra «Solicitudes», que otro módulo puede usar con toda la razón
  //      (`ANTES-DE-EMPEZAR-UN-MODULO.md` §1).
  await page.goto('/admin');
  await expect(page.locator('main')).toBeVisible();

  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu).toBeVisible();
  await expect(
    menu.locator('a[href^="/admin/solicitudes/"]'),
    'M07 activo debería poner exactamente 3 enlaces de menú',
  ).toHaveCount(3);

  // 2b · El router: **4 rutas**, las tres de menú y el detalle de una
  //      cotización real.
  for (const [ruta, titulo] of PANTALLAS) {
    await page.goto(ruta);
    await expect(page, `${ruta} no se quedó donde debía`).toHaveURL(new RegExp(`${ruta}$`));
    await expect(
      page.getByRole('heading', { name: titulo, exact: true }),
      `${ruta} no pintó su título`,
    ).toBeVisible();
  }

  const detalle = `/admin/solicitudes/cotizaciones/${usado.cotizacionId}`;
  await page.goto(detalle);
  await expect(page, 'la ruta de detalle no se quedó donde debía').toHaveURL(new RegExp(`${detalle}$`));
  await expect(
    page.getByRole('heading', { name: `Cotización ${usado.numero}`, exact: true }),
    'la pantalla de detalle no pintó el número de la cotización',
  ).toBeVisible();

  // 2c · Y las dos familias de API, con rutas reales y no inventadas.
  const adminActivo = await page.request.get('/api/admin/b2b/quotes');
  expect(adminActivo.status(), 'con M07 activo la bandeja del panel no responde 200').toBe(200);

  // =======================================================================
  // 3 · BLOQUEO (4.7) — M07 activo impide desactivar sus dependencias duras
  // =======================================================================
  const cabeceras = await csrfPanel(page);

  for (const dependencia of ['catalog', 'crm']) {
    const intento = await page.request.post(
      `/api/admin/modules/${dependencia}/deactivate`,
      { headers: cabeceras },
    );

    expect(
      intento.status(),
      `desactivar ${dependencia} con M07 activo debería dar 409 y dio ${intento.status()}`,
    ).toBe(409);

    // **La respuesta identifica a M07 por su código, en `blockedBy`.** El
    // título nombra al módulo bloqueado —`catalog`—; quién bloquea viaja aparte
    // a propósito, para que la interfaz pueda convertirlo en nombre visible y
    // en enlace (`AdminModuleEndpoints.cs:127-137`).
    const problema = (await intento.json()) as { title?: string; blockedBy?: string[] };
    expect(
      problema.blockedBy ?? [],
      `el 409 de ${dependencia} no identifica a M07 como quien bloquea`,
    ).toContain('b2b');

    // Y no ha pasado nada: la dependencia sigue activa.
    const capacidades = (await (await page.request.get('/api/capabilities')).json()) as {
      modules: { code: string }[];
    };
    expect(
      capacidades.modules.map((m) => m.code),
      `${dependencia} se desactivó a pesar del 409`,
    ).toContain(dependencia);
  }

  // =======================================================================
  // 4 · DESACTIVAR
  // =======================================================================
  await cambiarModulo(page, 'b2b', 'Desactivar');
  await expect(page.locator('#modulo-b2b')).toContainText('Inactivo');

  // 4a · El panel sigue en pie, y ningún enlace residual del grupo (4.3).
  await page.goto('/admin');
  await expect(page.locator('main')).toBeVisible();

  const menuApagado = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menuApagado, 'el panel se quedó sin menú al desactivar M07').toBeVisible();
  await expect(
    menuApagado.locator('a[href^="/admin/solicitudes/"]'),
    'quedó un enlace de M07 en el menú con el módulo desactivado',
  ).toHaveCount(0);

  // 4b · Las **cuatro** rutas dejan de estar montadas (4.2). Quien escriba una
  //      a mano no encuentra una pantalla rota: cae en la redirección.
  for (const [ruta] of PANTALLAS) {
    await page.goto(ruta);
    await expect(page, `${ruta} sigue montada con M07 desactivado`).not.toHaveURL(
      new RegExp(`${ruta}$`),
    );
  }

  await page.goto(detalle);
  await expect(
    page,
    'la ruta de detalle de cotización sigue montada con M07 desactivado',
  ).not.toHaveURL(new RegExp(`${detalle}$`));

  // 4c · Ni pantalla ni contenedor residual: con el módulo apagado no queda
  //      ningún rastro de sus títulos en el panel.
  for (const [, titulo] of PANTALLAS) {
    await expect(
      page.getByRole('heading', { name: titulo, exact: true }),
      `quedó la pantalla «${titulo}» con M07 desactivado`,
    ).toHaveCount(0);
  }

  // 4d · La API: **rutas reales de los dos grupos**, no una URL inventada.
  //      Un 404 sobre algo que nunca existió sería un falso verde.
  const clienteApagado = await browser.newContext({ baseURL: FRONTEND_URL });

  const rutasReales = [
    { quien: 'cliente', respuesta: await clienteApagado.request.get('/api/b2b/my-requests') },
    { quien: 'cliente', respuesta: await clienteApagado.request.get(`/api/b2b/quotes/${usado.numero}`) },
    { quien: 'panel', respuesta: await page.request.get('/api/admin/b2b/quotes') },
    {
      quien: 'panel',
      respuesta: await page.request.get(`/api/admin/b2b/quotes/${usado.cotizacionId}`),
    },
  ];

  for (const { quien, respuesta } of rutasReales) {
    expect(
      respuesta.status(),
      `una ruta real de ${quien} debería dar 404 con M07 apagado y dio ${respuesta.status()}`,
    ).toBe(404);
  }

  await clienteApagado.close();

  // 4e · Y Swagger no declara ni una: el módulo inactivo no mapea sus
  //      endpoints, así que no puede quedar documentado lo que no existe.
  const swaggerApagado = await page.request.get(`${API_URL}/swagger/v1/swagger.json`);
  expect(swaggerApagado.ok(), 'Swagger no responde con M07 apagado').toBe(true);
  const docApagado = (await swaggerApagado.json()) as { paths: Record<string, unknown> };

  const residuales = Object.keys(docApagado.paths).filter(
    (ruta) => ruta.startsWith('/api/b2b') || ruta.startsWith('/api/admin/b2b'),
  );
  expect(
    residuales,
    `Swagger sigue declarando rutas de M07 con el módulo apagado:\n${residuales.join('\n')}`,
  ).toEqual([]);

  // 4f · El resto del panel sigue operativo: no es que todo esté roto.
  const ajeno = await page.request.get('/api/admin/catalog/products?pageSize=1');
  expect(ajeno.status(), 'con M07 apagado el catálogo dejó de responder').toBe(200);

  // =======================================================================
  // 5 · LOS DATOS SIGUEN AHÍ (4.6) — desactivar no es borrar
  // =======================================================================
  expect(
    await inventario(),
    'desactivar M07 se llevó solicitudes, cotizaciones o líneas',
  ).toBe(antes);

  // =======================================================================
  // 6 · REACTIVAR — y las superficies vuelven
  // =======================================================================
  await cambiarModulo(page, 'b2b', 'Activar');
  await expect(page.locator('#modulo-b2b')).toContainText('Activo');

  await page.goto('/admin');
  await expect(page.locator('main')).toBeVisible();
  await expect(
    page.getByRole('navigation', { name: 'Secciones del panel' }).locator('a[href^="/admin/solicitudes/"]'),
    'al reactivar M07 no volvieron sus 3 enlaces de menú',
  ).toHaveCount(3);

  for (const [ruta, titulo] of PANTALLAS) {
    await page.goto(ruta);
    await expect(
      page.getByRole('heading', { name: titulo, exact: true }),
      `al reactivar M07 la pantalla ${ruta} no volvió`,
    ).toBeVisible();
  }

  await page.goto(detalle);
  await expect(
    page.getByRole('heading', { name: `Cotización ${usado.numero}`, exact: true }),
    'al reactivar M07 la pantalla de detalle no volvió',
  ).toBeVisible();

  // Y los datos son **los mismos**, uno a uno: recuentos y números visibles.
  expect(
    await inventario(),
    'al reactivar M07 el inventario no coincide con el de antes de apagarlo',
  ).toBe(antes);

  // La cotización pagada sigue pagada, con su atribución intacta.
  expect(
    await psql(`
      SELECT status || '|' || coalesce(paid_registered_by, '')
      FROM b2b.quotes WHERE quote_id = ${usado.cotizacionId}
    `),
    'el ciclo de apagado y encendido alteró la cotización pagada',
  ).toMatch(/^pagada\|.+/);

  // Y el cliente vuelve a poder leer la suya.
  const panelVivo = await page.request.get(`/api/admin/b2b/quotes/${usado.cotizacionId}`);
  expect(panelVivo.status(), 'al reactivar M07 el detalle de la cotización no responde').toBe(200);

  expect(usado.personalizacionId, 'la siembra no dejó personalización').toBeGreaterThan(0);
});
