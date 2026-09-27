/**
 * M04: ciclo REAL del módulo con el stack E2E efímero.
 *
 * **No es lo mismo que borrar el esquema.** `Test12` y `Test13`
 * (`CrmPersistenceTests`) acreditan que el esquema `crm` se borra y se vuelve a
 * crear sin tocar `core` ni `catalog`, pero ahí no hay aplicación: nadie mira el
 * menú, las rutas ni la portada. Esto recorre el procedimiento de la plataforma
 * —desactivar desde el panel, desinstalar, reinstalar, activar— y mira lo que
 * ve una persona.
 *
 * **Los detectores devuelven la lista de problemas en vez de afirmar.** Así se
 * puede comprobar, en la misma corrida y sobre la aplicación real, que dicen que
 * no cuando deben:
 *
 *   - con M04 activo, `restosDeCrm` **tiene que** encontrar restos;
 *   - con M04 apagado, `faltasDeCrm` **tiene que** encontrar faltas;
 *   - un enlace roto inyectado a propósito **tiene que** salir en
 *     `enlacesACrm`.
 *
 * Un detector que nunca encuentra nada es indistinguible de uno que funciona
 * (`ANTES-DE-EMPEZAR-UN-MODULO.md` §2).
 *
 * Todo ocurre sobre el stack efímero del arnés: el `99_drop.sql` se ejecuta
 * dentro de su contenedor `db`, nunca contra una base compartida.
 */
import path from 'node:path';
import type { APIRequestContext, Page, TestInfo } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { psql, psqlArchivo } from '../setup/docker.js';
import { CONNECTION_STRING, ROOT } from '../setup/env.js';
import { run } from '../setup/shell.js';

const sello = Date.now();
const producto = `Producto ciclo M04 ${sello}`;
const slug = `producto-ciclo-m04-${sello}`;

/** Rutas que M04 aporta: públicas y del panel (`frontend/src/modules/crm/routes.tsx`). */
const RUTAS_PUBLICAS_CRM = [
  '/entrar',
  '/crear-cuenta',
  '/recuperar-contrasena',
  '/restablecer-contrasena',
  '/verificar-correo',
  '/activar-cuenta',
  '/contacto',
  '/mi-cuenta',
];
const RUTAS_PANEL_CRM = ['/admin/clientes', '/admin/mensajes'];

/** ¿Es un href que apunta a una superficie de M04? */
function esRutaDeCrm(href: string): boolean {
  const ruta = href.split(/[?#]/)[0].replace(/^https?:\/\/[^/]+/, '');
  return [...RUTAS_PUBLICAS_CRM, ...RUTAS_PANEL_CRM].some(
    (r) => ruta === r || ruta.startsWith(`${r}/`),
  );
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

async function cambiarModulo(page: Page, accion: 'Activar' | 'Desactivar') {
  await page.goto('/admin/modulos');

  await duringExpectedOutage(page, async () => {
    await page.locator('#modulo-crm').getByRole('switch').click();
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: new RegExp(`^${accion}`) })
      .click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });
}

async function crearProducto(api: APIRequestContext) {
  const response = await api.post('/api/admin/catalog/products', {
    headers: await csrf(api),
    data: {
      name: producto,
      slug,
      shortDescription: null,
      description: null,
      primaryCategoryId: null,
      categoryIds: [],
      brandId: null,
      listPrice: 15,
      saleUnit: null,
      variantLabel: null,
      code: null,
      barcode: null,
    },
  });
  expect(response.ok(), `crear producto: ${response.status()} ${await response.text()}`).toBe(true);
}

async function crearFicha(api: APIRequestContext, nombre: string, correo: string) {
  return api.post('/api/admin/crm/customers', {
    headers: await csrf(api),
    data: {
      fullName: nombre,
      email: correo,
      phone: null,
      documentType: null,
      documentNumber: null,
      internalNotes: null,
    },
  });
}

/**
 * ¿Aparece el elemento? Espera hasta 15 s y devuelve un booleano en vez de
 * fallar, para que los detectores acumulen problemas. `isVisible()` no espera.
 */
async function seVe(locator: import('@playwright/test').Locator): Promise<boolean> {
  return locator.first().waitFor({ state: 'visible', timeout: 15_000 }).then(
    () => true,
    () => false,
  );
}

/** Los href de todos los enlaces de la página actual. */
async function hrefs(page: Page): Promise<string[]> {
  return page.locator('a[href]').evaluateAll((nodos) =>
    nodos.map((n) => (n as HTMLAnchorElement).getAttribute('href') ?? ''),
  );
}

/** Enlaces de la página actual que apuntan a una superficie de M04. */
async function enlacesACrm(page: Page): Promise<string[]> {
  return (await hrefs(page)).filter(esRutaDeCrm);
}

/**
 * ¿Una ruta sigue viva? `false` si el enrutador la redirige a otra parte.
 *
 * La redirección de una ruta sin módulo ocurre en el cliente, cuando ya han
 * llegado las capacidades: se espera a que la URL cambie, con plazo, en vez de
 * mirarla una sola vez y tomar por viva una ruta que iba a redirigir.
 */
async function rutaViva(page: Page, ruta: string): Promise<boolean> {
  await page.goto(ruta);
  await page.waitForLoadState('networkidle');
  const redirigida = await page
    .waitForURL((url) => url.pathname !== ruta, { timeout: 5_000 })
    .then(() => true, () => false);
  return !redirigida;
}

/**
 * Restos de M04 cuando debería estar apagado. Vacío = no queda nada.
 * Con M04 activo **tiene que** devolver restos: es el control negativo.
 */
async function restosDeCrm(page: Page): Promise<string[]> {
  const restos: string[] = [];

  if ((await capacidades(page.request)).includes('crm')) restos.push('capacidades: crm activo');

  const me = await page.request.get('/api/customer/auth/me');
  if (me.status() !== 404) restos.push(`API de cliente responde ${me.status()} en /api/customer/auth/me`);

  await page.goto('/admin');
  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu).toBeVisible();
  const enMenu = (await menu.locator('a[href]').evaluateAll((n) =>
    n.map((a) => (a as HTMLAnchorElement).getAttribute('href') ?? ''),
  )).filter(esRutaDeCrm);
  for (const href of enMenu) restos.push(`menú del panel enlaza ${href}`);
  for (const href of await enlacesACrm(page)) {
    if (!enMenu.includes(href)) restos.push(`inicio del panel enlaza ${href}`);
  }

  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await page.waitForLoadState('networkidle');
  if (await page.getByText('Cuenta de cliente', { exact: true }).count()) {
    restos.push('portada pinta la sección de cuenta de cliente');
  }
  for (const href of await enlacesACrm(page)) restos.push(`portada enlaza ${href}`);

  for (const ruta of [...RUTAS_PUBLICAS_CRM, ...RUTAS_PANEL_CRM]) {
    if (await rutaViva(page, ruta)) restos.push(`ruta ${ruta} sigue montada`);
  }

  return restos;
}

/**
 * Faltas de M04 cuando debería estar activo. Vacío = todo está.
 * Con M04 apagado **tiene que** devolver faltas: es el control negativo.
 */
async function faltasDeCrm(page: Page): Promise<string[]> {
  const faltas: string[] = [];

  if (!(await capacidades(page.request)).includes('crm')) faltas.push('capacidades: crm no está');

  const me = await page.request.get('/api/customer/auth/me');
  if (!me.ok()) faltas.push(`/api/customer/auth/me responde ${me.status()}`);

  await page.goto('/admin');
  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu).toBeVisible();
  for (const ruta of RUTAS_PANEL_CRM) {
    if ((await menu.locator(`a[href="${ruta}"]`).count()) === 0) faltas.push(`menú sin ${ruta}`);
  }

  if (!(await rutaViva(page, '/admin/clientes'))
    || !(await seVe(page.getByRole('heading', { level: 1, name: 'Clientes' })))) {
    faltas.push('/admin/clientes no muestra la lista de clientes');
  }

  if (!(await rutaViva(page, '/entrar'))
    || !(await seVe(page.getByRole('heading', { name: 'Entrar a mi cuenta' })))) {
    faltas.push('/entrar no muestra el acceso de clientes');
  }

  if (!(await rutaViva(page, '/contacto'))
    || !(await seVe(page.getByRole('heading', { level: 1, name: 'Contacto' })))) {
    faltas.push('/contacto no muestra el formulario');
  }

  await page.goto('/');
  if (!(await seVe(page.getByText('Cuenta de cliente', { exact: true })))) {
    faltas.push('portada sin la sección de cuenta de cliente');
  }

  return faltas;
}

/** CORE, M01 y M02 siguen en pie. Vacío = todo bien. */
async function problemasAjenos(page: Page): Promise<string[]> {
  const problemas: string[] = [];
  const activas = await capacidades(page.request);
  // CORE no se busca en capacidades: se comprueba por su panel, más abajo.
  for (const codigo of ['catalog', 'cms']) {
    if (!activas.includes(codigo)) problemas.push(`capacidades: falta ${codigo}`);
  }

  // CORE: el panel y su gestión de módulos.
  await page.goto('/admin/modulos');
  if (!(await seVe(page.locator('#modulo-crm')))) problemas.push('CORE: /admin/modulos no lista M04');
  await page.goto('/admin');
  if (!(await seVe(page.getByRole('navigation', { name: 'Secciones del panel' })))) {
    problemas.push('CORE: el panel no pinta su menú');
  }

  // M01: API pública y ficha del producto.
  const ficha = await page.request.get(`/api/catalog/products/${slug}`);
  if (!ficha.ok()) problemas.push(`M01: /api/catalog/products/${slug} responde ${ficha.status()}`);
  await page.goto(`/producto/${slug}`);
  if (!(await seVe(page.getByRole('heading', { name: producto })))) {
    problemas.push('M01: la ficha pública del producto no se pinta');
  }

  // M02: API pública y panel de contenido.
  const banners = await page.request.get('/api/cms/banners');
  if (!banners.ok()) problemas.push(`M02: /api/cms/banners responde ${banners.status()}`);
  await page.goto('/admin/contenido/banners');
  if (!(await seVe(page.getByRole('heading', { name: 'Banners' })))) {
    problemas.push('M02: /admin/contenido/banners no se pinta');
  }

  return problemas;
}

async function estadoAjeno() {
  return {
    usuarios: await psql('SELECT count(*) FROM core.admin_users'),
    ajustes: await psql('SELECT count(*) FROM core.site_settings'),
    modulos: await psql('SELECT count(*) FROM core.modules'),
    productos: await psql('SELECT count(*) FROM catalog.products'),
    tablasCore: await psql("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'core'"),
    tablasCatalogo: await psql("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'catalog'"),
    tablasCms: await psql("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'cms'"),
  };
}

async function tablasCrm() {
  return psql("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'crm'");
}

async function foto(page: Page, testInfo: TestInfo, nombre: string) {
  await testInfo.attach(`m04-${nombre}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png',
  });
}

test('[M04-CICLO] desactivar, desinstalar, reinstalar y activar M04 sin romper CORE, M01 ni M02', async ({
  page,
}, testInfo) => {
  test.setTimeout(480_000);
  await loginAsE2eAdmin(page);
  await crearProducto(page.request);

  // 1 · Punto de partida: M04 activo y completo.
  const iniciales = await capacidades(page.request);
  expect(iniciales, 'la prueba necesita M04 activo al empezar').toContain('crm');
  const otras = iniciales.filter((c) => c !== 'crm');
  expect(await faltasDeCrm(page), 'M04 activo debería estar completo').toEqual([]);
  expect(await problemasAjenos(page)).toEqual([]);

  // Control negativo: con M04 activo, el detector de restos TIENE que verlos.
  const restosConCrm = await restosDeCrm(page);
  expect(restosConCrm, 'el detector de restos no ve M04 aunque está activo: es mudo').toEqual(
    expect.arrayContaining([
      'capacidades: crm activo',
      'menú del panel enlaza /admin/clientes',
      'portada pinta la sección de cuenta de cliente',
      // /mi-cuenta no: sin sesión de cliente redirige a /entrar (session/guards.tsx).
      'ruta /entrar sigue montada',
      'ruta /admin/clientes sigue montada',
    ]),
  );

  const inicio = await estadoAjeno();
  const crmInicial = await tablasCrm();
  expect(Number(crmInicial)).toBeGreaterThan(0);
  await foto(page, testInfo, 'activo');

  // 2 · Desactivar desde el panel.
  await cambiarModulo(page, 'Desactivar');
  expect(await capacidades(page.request)).toEqual(otras);
  expect(await restosDeCrm(page), 'M04 desactivado deja restos').toEqual([]);
  expect(await problemasAjenos(page), 'desactivar M04 rompió otro módulo').toEqual([]);

  // Control negativo: con M04 apagado, el detector de faltas TIENE que verlas.
  expect(await faltasDeCrm(page), 'el detector de faltas no ve que M04 falta: es mudo').toEqual(
    expect.arrayContaining(['capacidades: crm no está', 'menú sin /admin/clientes']),
  );

  // Control negativo: un enlace roto inyectado TIENE que salir.
  await page.goto('/');
  expect(await enlacesACrm(page)).toEqual([]);
  await page.evaluate(() => {
    const a = document.createElement('a');
    a.href = '/mi-cuenta';
    a.textContent = 'Enlace roto de control';
    document.querySelector('main')?.appendChild(a);
  });
  expect(await enlacesACrm(page), 'el detector de enlaces no ve un enlace roto inyectado').toEqual(['/mi-cuenta']);
  await foto(page, testInfo, 'desactivado');

  // 3 · Desinstalar: el esquema, dos veces (idempotente), con el módulo ya apagado.
  await psqlArchivo('/scripts/modules/crm/99_drop.sql');
  await psqlArchivo('/scripts/modules/crm/99_drop.sql');
  expect(await tablasCrm()).toBe('0');
  expect(
    await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'crm'"),
  ).toBe('0');
  expect(await estadoAjeno(), 'desinstalar M04 tocó CORE, M01 o M02').toEqual(inicio);
  expect(await restosDeCrm(page), 'M04 desinstalado deja restos').toEqual([]);
  expect(await problemasAjenos(page), 'desinstalar M04 rompió otro módulo').toEqual([]);

  // 4 · Reinstalar: migraciones de CRM y su semilla, dos veces.
  await run(
    'dotnet',
    ['ef', 'database', 'update', '--project', 'Sillar.Modules.Crm', '--startup-project', 'Sillar.Api'],
    { cwd: path.join(ROOT, 'backend'), env: { ConnectionStrings__Default: CONNECTION_STRING } },
  );
  await psqlArchivo('/scripts/modules/crm/02_seed.sql');
  await psqlArchivo('/scripts/modules/crm/02_seed.sql');
  expect(await tablasCrm()).toBe(crmInicial);
  expect(await estadoAjeno()).toEqual(inicio);

  // 5 · Activar: el proceso se reinicia y tiene que arrancar limpio.
  await cambiarModulo(page, 'Activar');
  await expect(page.locator('#modulo-crm')).toContainText('Activo');
  expect(await capacidades(page.request)).toEqual(iniciales);
  expect(await faltasDeCrm(page), 'M04 reactivado no recupera sus superficies').toEqual([]);
  expect(await problemasAjenos(page), 'reactivar M04 rompió otro módulo').toEqual([]);
  expect(await restosDeCrm(page), 'el detector de restos no ve M04 tras reactivarlo').toContain(
    'capacidades: crm activo',
  );

  // Y funciona de verdad sobre la instalación nueva: crea una ficha y la lista.
  const correo = `ciclo-m04-${sello}@ejemplo.pe`;
  const alta = await crearFicha(page.request, `Cliente ciclo M04 ${sello}`, correo);
  expect(alta.ok(), `crear ficha tras reinstalar: ${alta.status()} ${await alta.text()}`).toBe(true);
  await page.goto('/admin/clientes');
  await expect(page.getByText(correo)).toBeVisible();

  expect(await estadoAjeno()).toEqual(inicio);
  await foto(page, testInfo, 'reinstalado');
});
