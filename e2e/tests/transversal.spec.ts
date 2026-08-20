import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * Reglas que no son de una pantalla sino de todas.
 *
 * Las tres salen de `CLAUDE.md` y de `VERIFICACION-VISUAL-CORE.md` §9, y las
 * tres son afirmaciones sobre el DOM: ninguna necesita ojos. Recorrer todas
 * las pantallas buscándolas a mano era lo que más tiempo le costaba a JP y lo
 * que menos juicio humano pedía.
 *
 * El `uuid` se comprueba aquí y no en la spec de medios, aunque la guía lo
 * pusiera en su §7: la regla que protege es global —«los identificadores
 * nunca se muestran al usuario»— y un `uuid` escapándose en la ficha de un
 * producto es el mismo fallo que en la galería.
 */

/** Las pantallas del panel que hoy existen (`modules/core/routes.tsx:39-62`). */
const SCREENS = [
  { path: '/admin', name: 'Inicio' },
  { path: '/admin/modulos', name: 'Módulos' },
  { path: '/admin/configuracion', name: 'Configuración' },
  { path: '/admin/archivos', name: 'Archivos' },
  { path: '/admin/usuarios', name: 'Usuarios' },
  { path: '/admin/auditoria', name: 'Auditoría' },
  { path: '/admin/mi-contrasena', name: 'Mi contraseña' },
  // De M01. Solo existe con el módulo activo, que es como lo deja
  // `global-setup.ts`: si esta ruta redirige, el entorno está mal montado.
  { path: '/admin/catalogo/marcas', name: 'Marcas' },
  { path: '/admin/catalogo/categorias', name: 'Categorías' },
  { path: '/admin/catalogo/productos', name: 'Productos' },
] as const;

/**
 * `uuid` en cualquiera de sus formas visibles. Deliberadamente laxo en la
 * versión —`[0-9a-f]` en vez de exigir el 7— para que también cace un v4 que
 * alguien filtre por descuido.
 */
const UUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;

test('Ninguna pantalla dice «Ha ocurrido un error»', async ({ page }) => {
  await loginAsE2eAdmin(page);

  for (const screen of SCREENS) {
    const text = (await abrirPantalla(page, screen.path)).toLowerCase();
    expect(text, `«${screen.name}» (${screen.path}) muestra un error genérico`)
      .not.toContain('ha ocurrido un error');
  }
});

test('Ningún botón se llama «Aceptar»', async ({ page }) => {
  await loginAsE2eAdmin(page);

  for (const screen of SCREENS) {
    await abrirPantalla(page, screen.path);

    // Un botón nombra la acción que ejecuta. «Aceptar» no dice ninguna, y
    // `confirmLabel` sin valor por defecto lo hace imposible por descuido en
    // el ConfirmDialog — esta prueba cubre el resto de los botones.
    await expect(
      page.getByRole('button', { name: /^\s*aceptar\s*$/i }),
      `«${screen.name}» (${screen.path}) tiene un botón «Aceptar»`,
    ).toHaveCount(0);
  }
});

/**
 * Abre una pantalla y espera a que **haya terminado de cargar sus datos**.
 *
 * No basta con que `main` sea visible: se cumple en cuanto aparece el
 * armazón, con el spinner todavía puesto y la tabla vacía. Escanear ahí
 * dentro hace que estas pruebas pasen sin haber mirado nada — que es
 * exactamente lo que estaban haciendo, y solo se notó porque la de auditoría
 * pasaba unas veces sí y otras no.
 */
async function abrirPantalla(page: import('@playwright/test').Page, path: string): Promise<string> {
  await page.goto(path);
  await expect(page.locator('main')).toBeVisible();

  // Sin peticiones en vuelo: los datos de la pantalla ya llegaron.
  await page.waitForLoadState('networkidle');
  await expect(page.locator('main .ui-spinner')).toHaveCount(0);

  return page.locator('main').innerText();
}

/** Busca un `uuid` en el texto que una persona puede leer en la pantalla. */
async function identificadorVisible(
  page: import('@playwright/test').Page,
  path: string,
): Promise<string | null> {
  // El texto visible, no el HTML: un `uuid` en un id= o en un data- es
  // legítimo (las tarjetas de módulo usan id="modulo-<código>"). Lo que la
  // regla prohíbe es que lo lea una persona.
  const visible = await abrirPantalla(page, path);
  return visible.match(UUID)?.[0] ?? null;
}

test('Ninguna pantalla enseña un identificador al usuario', async ({ page }) => {
  await loginAsE2eAdmin(page);

  // Auditoría va aparte, abajo: tiene un defecto conocido y abierto.
  for (const screen of SCREENS.filter((s) => s.path !== '/admin/auditoria')) {
    const found = await identificadorVisible(page, screen.path);

    expect(
      found,
      `«${screen.name}» (${screen.path}) muestra un identificador: ${found}`,
    ).toBeNull();
  }
});

/**
 * DEFECTO ABIERTO, no una excepción a la regla.
 *
 * `AuditPage.tsx:71` pinta `entry.entityId` en crudo, y desde la ADR-018 el
 * identificador de un medio es un `uuid`: la columna «Entidad» acaba
 * mostrando `019fff83-a5d5-74b0-…` a la vista, contra la regla de
 * `CLAUDE.md` de que los identificadores nunca se muestran al usuario.
 *
 * Se marca `fail` en vez de exentar la pantalla del bucle de arriba porque
 * son cosas distintas: exentarla escondería el defecto y nadie volvería a
 * mirarlo. Así queda escrito en código, no cuesta un rojo permanente que
 * enseñe a ignorar el rojo, y **si alguien lo arregla esta prueba empieza a
 * fallar** y obliga a venir aquí a borrar la marca.
 *
 * Qué debería mostrar en su lugar es una decisión de producto sin tomar: la
 * auditoría necesita identificar la fila exacta y a la vez no puede enseñar
 * el identificador. Anotado en `BITACORA.md` §5.
 */
test('La auditoría no enseña identificadores', async ({ page }) => {
  test.fail(true, 'Defecto abierto: AuditPage.tsx:71 pinta entityId en crudo');

  await loginAsE2eAdmin(page);

  // La entrada con `uuid` se provoca aquí, no se espera a que esté: si esta
  // prueba dependiera de lo que otras dejaron en el registro, pasaría o
  // fallaría según el orden de ejecución. Una spec intermitente enseña a
  // ignorar el rojo. Subir un medio deja una entrada de auditoría cuya
  // entidad es un `uuid` desde la ADR-018.
  await page.goto('/admin/archivos');
  await page.setInputFiles('.gal-drop input[type="file"]', {
    name: 'rastro-auditoria.png',
    mimeType: 'image/png',
    buffer: Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
      'base64',
    ),
  });
  await expect(page.getByRole('status').first()).toBeVisible();

  const found = await identificadorVisible(page, '/admin/auditoria');

  expect(found, `Auditoría muestra un identificador: ${found}`).toBeNull();
});

/**
 * Las mismas pantallas, con `prefers-reduced-motion` activo.
 *
 * Hasta ahora la suite **nunca emulaba la preferencia**, así que la política
 * de movimiento estaba escrita y sin ejercitar: nada distinguía «la regla
 * existe» de «la regla hace algo». Esto es lo que las separa.
 */
test('Con movimiento reducido, el indicador se detiene y sigue diciendo qué espera', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await loginAsE2eAdmin(page);

  // Una pantalla real que carga de verdad, no una ruta de prueba: el defecto
  // era «SILLAR no tiene indicador accesible», no «el componente aislado no
  // lo tiene». Se retrasa la respuesta para que el indicador llegue a verse.
  await page.route('**/api/admin/catalog/categories', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.continue();
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, 2500));
    await route.continue();
  });

  await page.goto('/admin/catalogo/categorias');

  const indicador = page.locator('.ui-table__state .ui-spinner-wrap');

  // 1 · **Lo que ve quien ve.** Con movimiento reducido el anillo se queda
  //     quieto, así que si el texto fuera `sr-only` la pantalla enseñaría un
  //     círculo parado y nada más — el defecto original, entero. Esta
  //     afirmación es la que faltaba: pasaba estando mal.
  await expect(indicador).toBeVisible();
  await expect(indicador.getByText(/Cargando/)).toBeVisible();

  // 2 · Y la región persistente, que es la otra mitad: vive FUERA del nodo
  //     con `aria-busy` y existe desde el primer render, no aparece con el
  //     mensaje (F103). Se comprueba que el nodo ocupado no la contenga.
  const ocupado = page.locator('.ui-table-scroll[aria-busy="true"]');
  await expect(ocupado).toHaveCount(1);

  const dentro = await page.evaluate(() => {
    const busy = document.querySelector('.ui-table-scroll[aria-busy="true"]');
    const regiones = Array.from(document.querySelectorAll('.ui-table-wrap [role="status"]'));
    return regiones.some((r) => busy?.contains(r));
  });
  expect(dentro, 'la región de estado vive dentro del nodo con aria-busy').toBe(false);

  // 3 · Y el anillo NO se mueve. La prueba no puede «arreglar» accesibilidad
  //     dejándolo girar: se afirma la ausencia de movimiento y la presencia
  //     del texto a la vez.
  const duracion = await indicador
    .locator('.ui-spinner')
    .evaluate((el) => getComputedStyle(el).animationDuration);

  // En milisegundos, sea cual sea la unidad que devuelva el navegador.
  const ms = duracion.endsWith('ms') ? parseFloat(duracion) : parseFloat(duracion) * 1000;
  expect(ms, `el anillo sigue animándose con movimiento reducido: ${duracion}`).toBeLessThan(50);

  await page.unroute('**/api/admin/catalog/categories');
});

/**
 * Que el proyecto `chromium-movimiento-reducido` **de verdad** aplique la
 * preferencia, y no solo repita las pruebas.
 *
 * Sin esto, una configuración mal puesta daría cinco pruebas más en verde sin
 * ejercitar nada — el mismo tipo de verde vacío que ya salió cinco veces hoy.
 * Por eso no afirma «está activa»: afirma **cuál de los dos proyectos es** y
 * que la preferencia coincide con él.
 */
test('El proyecto dice qué preferencia de movimiento aplica, y coincide', async ({ page }, info) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin');

  const reducido = await page.evaluate(
    () => window.matchMedia('(prefers-reduced-motion: reduce)').matches,
  );

  const esperado = info.project.name === 'chromium-movimiento-reducido';

  expect(
    reducido,
    `el proyecto «${info.project.name}» ${esperado ? 'debía' : 'no debía'} tener la preferencia activa`,
  ).toBe(esperado);
});

/**
 * Lo que la tienda no heredó del armazón — y que el armazón tampoco tenía.
 *
 * `main` faltaba y lo cazó `axe` porque es de lo que sabe buscar. Estas tres
 * cosas no las mira: **el título por ruta, el foco al cambiar de página y el
 * enlace de salto**. Al enumerarlas resultó que no faltaban solo en la
 * tienda: no existían en ninguna de las dos mitades.
 */
test('Cada pantalla tiene su propio título de documento', async ({ page }) => {
  await loginAsE2eAdmin(page);

  const vistos = new Map<string, string>();

  for (const screen of SCREENS) {
    await abrirPantalla(page, screen.path);
    const titulo = await page.title();

    // Ni el título estático del index.html ni vacío: cada pantalla se llama
    // como es. Es lo que se ve en el historial y al compartir un enlace.
    expect(titulo, `«${screen.name}» no pone su título`).not.toBe('SILLAR');
    expect(titulo.length).toBeGreaterThan(0);

    const previa = vistos.get(titulo);
    expect(previa, `«${screen.name}» comparte título con «${previa}»: ${titulo}`).toBeUndefined();
    vistos.set(titulo, screen.name);
  }
});

test('Al cambiar de ruta el foco va al contenido, no se queda en el menú', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');
  await expect(page.locator('main')).toBeVisible();

  // Se navega **desde un enlace del menú**, que es el caso real: sin mover el
  // foco, quien usa teclado se queda en el menú de la pantalla anterior.
  await page.getByRole('navigation').getByRole('link', { name: 'Usuarios' }).click();
  await expect(page).toHaveURL(/\/admin\/usuarios$/);

  // **Se espera a que el foco llegue, no se mide una vez.** `toHaveURL` pasa
  // en cuanto cambia el historial, y `RouteFocus` mueve el foco en un efecto,
  // que corre después del commit siguiente: entre las dos cosas hay un hueco
  // que crece con la máquina cargada. Medir una sola vez ahí dentro daba «el
  // foco no está en el contenido» sin que nada estuviera mal — el `main` del
  // panel vive en `AdminShell.tsx:90` y no se remonta al navegar.
  //
  // Si el foco no llegara nunca, esto falla igual: lo que se quita es la
  // carrera de la medición, no la afirmación.
  await expect
    .poll(
      () =>
        page.evaluate(() => {
          const main = document.querySelector('main');
          return main === document.activeElement || main?.contains(document.activeElement) === true;
        }),
      { message: 'tras navegar, el foco no está en el contenido' },
    )
    .toBe(true);
});

test('El enlace de salto existe, está oculto y aparece al enfocarlo', async ({ page }) => {
  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');
  // A que termine de arrancar: durante el arranque la pantalla es un
  // indicador de carga y no hay nada que tabular todavía.
  await expect(page.locator('main')).toBeVisible();

  const salto = page.getByRole('link', { name: 'Saltar al contenido' });

  // Existe y es lo primero del recorrido: una pulsación de Tab lo alcanza.
  // **Sin hacer clic antes**: un clic fija el punto desde el que sigue el
  // recorrido, así que pulsar en la esquina mandaba el Tab a lo que hubiera
  // debajo. Recién cargada, la página empieza el recorrido por el principio.
  await page.keyboard.press('Tab');

  const enfocado = await page.evaluate(() => {
    const el = document.activeElement;
    return el ? `${el.tagName.toLowerCase()}.${el.className} «${el.textContent?.trim().slice(0, 30)}»` : 'ninguno';
  });

  expect(enfocado, 'el primer Tab no llega al enlace de salto').toContain('pf-skip');

  // Y lleva a un ancla que existe de verdad.
  await expect(page.locator('#contenido')).toHaveCount(1);
});
