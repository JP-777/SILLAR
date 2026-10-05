/**
 * M05a Servicios — Vitrina: ciclo REAL del módulo con el producto ejecutándose.
 *
 * activo → desactivado → reactivado, desde el panel de módulos, con el proceso
 * reiniciándose en cada cambio igual que en una instalación. Se mira lo que ve
 * una persona: rutas públicas y del panel, navegación, portada y enlaces.
 * Leer el código para ver que existe un `has('services')` no acredita nada.
 *
 * **Punto de partida y de llegada.** El arnés (`setup/global-setup.ts`) no
 * activa `services`: M05a empieza inactivo. La prueba lo activa, siembra un
 * servicio publicado —sin él la portada no tiene nada que enseñar—, recorre el
 * ciclo y **lo deja inactivo al terminar**, pase lo que pase (`finally`), para
 * no cambiarle el entorno a lo que corre después.
 *
 * **Los detectores devuelven listas en vez de afirmar**, como en
 * `zz-z-m04-ciclo.spec.ts`. En la misma corrida se comprueba que dicen que no
 * cuando deben:
 *
 *   - con M05a activo, `restosDeServicios` **tiene que** encontrar restos;
 *   - con M05a apagado, `faltasDeServicios` **tiene que** encontrar faltas;
 *   - un enlace muerto inyectado **tiene que** salir en `enlacesAServicios`;
 *   - un hueco inyectado en la portada **tiene que** salir en `huecosDePortada`.
 *
 * Un detector que nunca encuentra nada es indistinguible de uno que funciona
 * (`ANTES-DE-EMPEZAR-UN-MODULO.md` §2).
 *
 * **Por qué el nombre empieza por `zz-z-`, y no es un accidente.**
 *
 *   - El prefijo es deliberado: coloca esta prueba al final del orden
 *     alfabético en que Playwright recorre los archivos.
 *   - La prueba cambia el estado real de un módulo (activa y desactiva M05a
 *     desde el panel) y reinicia el host varias veces.
 *   - El arnés ejecuta con `workers: 1` y `fullyParallel: false`
 *     (`playwright.config.ts`): los archivos corren de uno en uno y en orden,
 *     así que lo que esta prueba hace lo ven las que vienen detrás.
 *   - Por eso tiene que correr después de todas las pruebas que puedan depender
 *     del estado inicial del arnés —M05a inactivo y un host recién arrancado—.
 *   - Renombrarla o reordenarla sin revisar esa dependencia puede contaminar
 *     las pruebas posteriores.
 *   - El `finally` que devuelve M05a a inactivo no hace irrelevante el orden:
 *     sigue siendo una prueba de ciclo destructiva respecto del estado en
 *     ejecución —reinicios del host y datos sembrados—, y el `finally` solo
 *     limita el daño si algo falla a mitad.
 */
import type { APIRequestContext, Browser, Page, TestInfo } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';

const sello = Date.now();
const servicio = `Anillado espiral ciclo M05a ${sello}`;
const slug = `anillado-espiral-ciclo-m05a-${sello}`;

/** Rutas que M05a aporta (`frontend/src/modules/services/routes.tsx`). */
const RUTA_LISTA = '/servicios';
const RUTA_FICHA = `/servicios/${slug}`;
const RUTA_PANEL = '/admin/servicios';
const TITULO_PORTADA = 'Nuestros servicios';

/** ¿Es un href que apunta a una superficie de M05a? */
function esRutaDeServicios(href: string): boolean {
  const ruta = href.split(/[?#]/)[0].replace(/^https?:\/\/[^/]+/, '');
  return [RUTA_LISTA, RUTA_PANEL].some((r) => ruta === r || ruta.startsWith(`${r}/`));
}

async function csrf(api: APIRequestContext) {
  const response = await api.get('/api/admin/auth/csrf');
  expect(response.ok()).toBe(true);
  const data = (await response.json()) as { csrfToken: string };
  return { 'X-CSRF-Token': data.csrfToken };
}

async function capacidades(api: APIRequestContext) {
  const response = await api.get('/api/capabilities');
  expect(response.ok()).toBe(true);
  const data = (await response.json()) as { modules: { code: string }[] };
  return data.modules.map((item) => item.code).sort();
}

/** Activa o desactiva M05a desde el panel; el proceso se reinicia. */
async function cambiarModulo(page: Page, accion: 'Activar' | 'Desactivar') {
  await page.goto('/admin/modulos');

  await duringExpectedOutage(page, async () => {
    await page.locator('#modulo-services').getByRole('switch').click();
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: new RegExp(`^${accion}`) })
      .click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });
}

/** Un servicio publicado, por la API del panel: sin él la portada no pinta nada. */
async function crearServicioPublicado(api: APIRequestContext) {
  const alta = await api.post('/api/admin/services', {
    headers: await csrf(api),
    data: {
      name: servicio,
      slug,
      shortDescription: 'Tapa transparente y contratapa',
      description: null,
      price: null,
      saleUnit: 'Por documento',
      imageId: null,
      imageAltText: null,
    },
  });
  expect(alta.ok(), `crear servicio: ${alta.status()} ${await alta.text()}`).toBe(true);
  const { id } = (await alta.json()) as { id: number };

  const publicar = await api.post(`/api/admin/services/${id}/publish`, { headers: await csrf(api) });
  expect(publicar.ok(), `publicar servicio: ${publicar.status()} ${await publicar.text()}`).toBe(true);
}

/** ¿Aparece el elemento? Espera hasta 15 s y devuelve un booleano. */
async function seVe(locator: import('@playwright/test').Locator): Promise<boolean> {
  return locator.first().waitFor({ state: 'visible', timeout: 15_000 }).then(
    () => true,
    () => false,
  );
}

async function hrefs(page: Page): Promise<string[]> {
  return page.locator('a[href]').evaluateAll((nodos) =>
    nodos.map((n) => (n as HTMLAnchorElement).getAttribute('href') ?? ''),
  );
}

/** Enlaces de la página actual que apuntan a una superficie de M05a. */
async function enlacesAServicios(page: Page): Promise<string[]> {
  return (await hrefs(page)).filter(esRutaDeServicios);
}

/**
 * ¿Una ruta sigue viva? `false` si el enrutador la redirige a otra parte
 * (`app/routes.tsx`: lo no montado cae en `*` y vuelve a `/`).
 */
async function rutaViva(page: Page, ruta: string): Promise<boolean> {
  await page.goto(ruta);
  await page.waitForLoadState('networkidle');
  const redirigida = await page
    .waitForURL((url) => url.pathname !== ruta, { timeout: 5_000 })
    .then(() => true, () => false);
  return !redirigida;
}

/** La portada, ya asentada. */
async function irAPortada(page: Page) {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await expect(page.locator('main#contenido')).toBeVisible();
}

/**
 * Hijos directos de la portada que ocupan sitio sin enseñar nada: un hueco.
 * `PublicSite.tsx` monta cada sección sin envoltorio, así que una sección que
 * quedara montada vacía se vería aquí.
 */
async function huecosDePortada(page: Page): Promise<string[]> {
  return page.locator('main#contenido > *').evaluateAll((nodos) =>
    nodos
      .filter((n) => {
        const caja = (n as HTMLElement).getBoundingClientRect();
        const texto = (n as HTMLElement).innerText.trim();
        const medios = n.querySelectorAll('img, svg, video, picture').length;
        return caja.height > 0 && texto === '' && medios === 0;
      })
      .map((n) => `<${n.tagName.toLowerCase()} class="${n.getAttribute('class') ?? ''}">`),
  );
}

/**
 * Los hijos directos de la portada, para comparar antes y después. El de M05a
 * se etiqueta por su título; el resto, por su primera línea de texto.
 */
async function bloquesDePortada(page: Page): Promise<string[]> {
  return page.locator('main#contenido > *').evaluateAll(
    (nodos, titulo) =>
      nodos.map((n) => {
        const texto = (n as HTMLElement).innerText.trim();
        return texto.includes(titulo) ? titulo : (texto.split('\n')[0] ?? '');
      }),
    TITULO_PORTADA,
  );
}

/**
 * Restos de M05a cuando debería estar apagado. Vacío = no queda nada.
 * Con M05a activo **tiene que** devolver restos: es el control negativo.
 */
async function restosDeServicios(page: Page): Promise<string[]> {
  const restos: string[] = [];

  if ((await capacidades(page.request)).includes('services')) restos.push('capacidades: services activo');

  const lista = await page.request.get('/api/services');
  if (lista.status() !== 404) restos.push(`API pública responde ${lista.status()} en /api/services`);

  await page.goto('/admin');
  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu).toBeVisible();
  const enMenu = (await menu.locator('a[href]').evaluateAll((n) =>
    n.map((a) => (a as HTMLAnchorElement).getAttribute('href') ?? ''),
  )).filter(esRutaDeServicios);
  for (const href of enMenu) restos.push(`menú del panel enlaza ${href}`);
  for (const href of await enlacesAServicios(page)) {
    if (!enMenu.includes(href)) restos.push(`inicio del panel enlaza ${href}`);
  }

  await irAPortada(page);
  if (await page.getByText(TITULO_PORTADA, { exact: true }).count()) {
    restos.push('portada pinta la sección de servicios');
  }
  for (const href of await enlacesAServicios(page)) restos.push(`portada enlaza ${href}`);
  for (const hueco of await huecosDePortada(page)) restos.push(`portada con hueco ${hueco}`);

  for (const ruta of [RUTA_LISTA, RUTA_FICHA, RUTA_PANEL]) {
    if (await rutaViva(page, ruta)) restos.push(`ruta ${ruta} sigue montada`);
  }

  return restos;
}

/**
 * Faltas de M05a cuando debería estar activo. Vacío = todo está.
 * Con M05a apagado **tiene que** devolver faltas: es el control negativo.
 */
async function faltasDeServicios(page: Page): Promise<string[]> {
  const faltas: string[] = [];

  if (!(await capacidades(page.request)).includes('services')) faltas.push('capacidades: services no está');

  const ficha = await page.request.get(`/api/services/${slug}`);
  if (!ficha.ok()) faltas.push(`/api/services/${slug} responde ${ficha.status()}`);

  await page.goto('/admin');
  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu).toBeVisible();
  if ((await menu.locator(`a[href="${RUTA_PANEL}"]`).count()) === 0) faltas.push(`menú sin ${RUTA_PANEL}`);

  if (!(await rutaViva(page, RUTA_PANEL))
    || !(await seVe(page.getByRole('heading', { level: 1, name: 'Servicios' })))
    || !(await seVe(page.getByText(servicio)))) {
    faltas.push(`${RUTA_PANEL} no muestra la administración con el servicio`);
  }

  if (!(await rutaViva(page, RUTA_LISTA))
    || !(await seVe(page.getByRole('heading', { level: 1, name: 'Servicios' })))
    || !(await seVe(page.getByRole('link', { name: new RegExp(servicio) })))) {
    faltas.push(`${RUTA_LISTA} no muestra la vitrina con el servicio`);
  }

  if (!(await rutaViva(page, RUTA_FICHA))
    || !(await seVe(page.getByRole('heading', { level: 1, name: servicio })))) {
    faltas.push(`${RUTA_FICHA} no muestra la ficha del servicio`);
  }

  await irAPortada(page);
  if (!(await seVe(page.getByText(TITULO_PORTADA, { exact: true })))) {
    faltas.push('portada sin la sección de servicios');
  }
  if (!(await enlacesAServicios(page)).includes(RUTA_LISTA)) faltas.push(`portada no enlaza ${RUTA_LISTA}`);

  return faltas;
}

/** CORE, M01, M02 y M04 siguen en pie. Vacío = todo bien. */
async function problemasAjenos(page: Page): Promise<string[]> {
  const problemas: string[] = [];
  const activas = await capacidades(page.request);
  for (const codigo of ['catalog', 'cms', 'crm']) {
    if (!activas.includes(codigo)) problemas.push(`capacidades: falta ${codigo}`);
  }

  await page.goto('/admin/modulos');
  if (!(await seVe(page.locator('#modulo-services')))) problemas.push('CORE: /admin/modulos no lista M05a');
  await page.goto('/admin');
  if (!(await seVe(page.getByRole('navigation', { name: 'Secciones del panel' })))) {
    problemas.push('CORE: el panel no pinta su menú');
  }

  const catalogo = await page.request.get('/api/catalog/products');
  if (!catalogo.ok()) problemas.push(`M01: /api/catalog/products responde ${catalogo.status()}`);
  if (!(await rutaViva(page, '/admin/catalogo/productos'))
    || !(await seVe(page.getByRole('heading', { level: 1, name: 'Productos' })))) {
    problemas.push('M01: /admin/catalogo/productos no se pinta');
  }

  const banners = await page.request.get('/api/cms/banners');
  if (!banners.ok()) problemas.push(`M02: /api/cms/banners responde ${banners.status()}`);

  if (!(await rutaViva(page, '/contacto'))) problemas.push('M04: /contacto no está montada');

  await irAPortada(page);
  if (await page.getByText('No se pudo cargar el sistema').count()) problemas.push('portada en página de error');

  return problemas;
}

async function servicioEsta(page: Page, nombre: string) {
  await expect(page.getByText(nombre)).toBeVisible();
}

async function foto(page: Page, testInfo: TestInfo, nombre: string) {
  await testInfo.attach(`m05a-${nombre}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png',
  });
}

/** Sin sesión, el panel de M05a no se abre: lleva al acceso. */
async function panelSinSesion(browser: Browser): Promise<string> {
  const contexto = await browser.newContext();
  try {
    const anonima = await contexto.newPage();
    await anonima.goto(RUTA_PANEL);
    await anonima.waitForURL((url) => url.pathname !== RUTA_PANEL, { timeout: 15_000 });
    return new URL(anonima.url()).pathname;
  } finally {
    await contexto.close();
  }
}

test('[M05A-CICLO] activar, desactivar y reactivar M05a sin romper CORE, M01, M02 ni M04', async ({
  page,
  browser,
}, testInfo) => {
  test.setTimeout(600_000);
  await loginAsE2eAdmin(page);

  // 0 · Punto de partida del arnés: M05a inactivo, y nada suyo a la vista.
  const iniciales = await capacidades(page.request);
  expect(iniciales, 'el arnés no activa M05a: la prueba parte de inactivo').not.toContain('services');
  expect(await restosDeServicios(page), 'M05a inactivo desde la instalación deja restos').toEqual([]);

  try {
    // 1 · Activar desde el panel y sembrar un servicio publicado.
    await cambiarModulo(page, 'Activar');
    await expect(page.locator('#modulo-services')).toContainText('Activo');
    const conServicios = await capacidades(page.request);
    expect(conServicios).toEqual([...iniciales, 'services'].sort());
    await crearServicioPublicado(page.request);

    // M05a activo: las cinco superficies.
    expect(await faltasDeServicios(page), 'M05a activo debería estar completo').toEqual([]);
    expect(await problemasAjenos(page)).toEqual([]);
    expect(await panelSinSesion(browser), 'sin sesión, /admin/servicios no debe abrirse').toBe('/login');

    // Control negativo: con M05a activo, el detector de restos TIENE que verlos.
    expect(await restosDeServicios(page), 'el detector de restos no ve M05a aunque está activo: es mudo').toEqual(
      expect.arrayContaining([
        'capacidades: services activo',
        `menú del panel enlaza ${RUTA_PANEL}`,
        'portada pinta la sección de servicios',
        `portada enlaza ${RUTA_LISTA}`,
        `ruta ${RUTA_LISTA} sigue montada`,
        `ruta ${RUTA_FICHA} sigue montada`,
        `ruta ${RUTA_PANEL} sigue montada`,
      ]),
    );

    await irAPortada(page);
    const portadaActiva = await bloquesDePortada(page);
    expect(portadaActiva).toContain(TITULO_PORTADA);
    await foto(page, testInfo, 'activo');

    // 2 · Desactivar desde el panel.
    await cambiarModulo(page, 'Desactivar');
    expect(await capacidades(page.request)).toEqual(iniciales);
    expect(await restosDeServicios(page), 'M05a desactivado deja restos').toEqual([]);
    expect(await problemasAjenos(page), 'desactivar M05a rompió otro módulo').toEqual([]);

    // Sin hueco: la portada es la de antes menos exactamente el bloque de M05a.
    await irAPortada(page);
    expect(await bloquesDePortada(page), 'desactivar M05a dejó un bloque de más o de menos en la portada').toEqual(
      portadaActiva.filter((bloque) => bloque !== TITULO_PORTADA),
    );

    // Control negativo: con M05a apagado, el detector de faltas TIENE que verlas.
    expect(await faltasDeServicios(page), 'el detector de faltas no ve que M05a falta: es mudo').toEqual(
      expect.arrayContaining(['capacidades: services no está', `menú sin ${RUTA_PANEL}`, 'portada sin la sección de servicios']),
    );

    // Control negativo: un enlace muerto y un hueco inyectados TIENEN que salir.
    await irAPortada(page);
    expect(await enlacesAServicios(page)).toEqual([]);
    expect(await huecosDePortada(page)).toEqual([]);
    await page.evaluate((ficha) => {
      const a = document.createElement('a');
      a.href = ficha;
      a.textContent = 'Enlace muerto de control';
      document.querySelector('main#contenido')?.appendChild(a);
      const hueco = document.createElement('section');
      hueco.className = 'hueco-de-control';
      hueco.style.height = '120px';
      document.querySelector('main#contenido')?.appendChild(hueco);
    }, RUTA_FICHA);
    expect(await enlacesAServicios(page), 'el detector de enlaces no ve un enlace muerto inyectado').toEqual([RUTA_FICHA]);
    expect(await huecosDePortada(page), 'el detector de huecos no ve un hueco inyectado').toEqual([
      '<section class="hueco-de-control">',
    ]);
    await foto(page, testInfo, 'desactivado');

    // 3 · Reactivar: vuelve todo, con los datos de antes.
    await cambiarModulo(page, 'Activar');
    await expect(page.locator('#modulo-services')).toContainText('Activo');
    expect(await capacidades(page.request)).toEqual(conServicios);
    expect(await faltasDeServicios(page), 'M05a reactivado no recupera sus superficies').toEqual([]);
    expect(await problemasAjenos(page), 'reactivar M05a rompió otro módulo').toEqual([]);
    await irAPortada(page);
    expect(await bloquesDePortada(page), 'la portada reactivada no es la de antes').toEqual(portadaActiva);

    // Y funciona de verdad, no solo se pinta: la administración sigue operativa.
    const otro = await page.request.post('/api/admin/services', {
      headers: await csrf(page.request),
      data: {
        name: `Plastificado ciclo M05a ${sello}`,
        slug: null,
        shortDescription: 'Tras reactivar',
        description: null,
        price: 2.5,
        saleUnit: 'Por hoja',
        imageId: null,
        imageAltText: null,
      },
    });
    expect(otro.ok(), `crear servicio tras reactivar: ${otro.status()} ${await otro.text()}`).toBe(true);
    await page.goto(RUTA_PANEL);
    await servicioEsta(page, `Plastificado ciclo M05a ${sello}`);
    await foto(page, testInfo, 'reactivado');
  } finally {
    // 4 · Se deja como se encontró: inactivo, que es como lo deja el arnés.
    if ((await capacidades(page.request)).includes('services')) {
      await cambiarModulo(page, 'Desactivar');
    }
  }

  expect(await capacidades(page.request)).toEqual(iniciales);
});
