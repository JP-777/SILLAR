import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * Los estados vacíos, que **solo existen antes de que nadie cree nada**.
 *
 * Estaban repartidos por sus ficheros y pasaban por accidente: `catalogo`,
 * `categorias` y `productos` van antes alfabéticamente que casi todo lo que
 * siembra datos. En cuanto aparecieron `medios-compartidos` y
 * `presentaciones`, la de productos empezó a fallar en la suite entera
 * —sola seguía pasando— y las otras dos quedaron a una letra de hacerlo.
 *
 * Se juntan aquí con el prefijo `aa-` para que el orden **sea una decisión y
 * no un accidente del abecedario**. Es lo único de la suite que depende de
 * correr primero, y por eso se dice en el nombre del fichero.
 */

/**
 * Las filas **con datos**, que no son todas las del `tbody`.
 *
 * `Table` pinta el estado vacío como una fila más —un `<tr>` con un `<td
 * class="ui-table__state">` (`patterns.tsx:279-281`)—, así que contar
 * `tbody tr` da **1** con la tabla vacía, no 0.
 *
 * Estas tres pruebas exigían `toBe(0)` y pasaban igualmente: contaban antes
 * de que la página pintara, cuando no había ninguna fila de nada. **Nunca
 * habrían pasado sobre una tabla pintada, ni vacía ni llena**, y solo se vio
 * al hacer que navegar esperara al armazón.
 */
function filasConDatos(page: import('@playwright/test').Page) {
  return page.locator('tbody tr').filter({ hasNot: page.locator('.ui-table__state') });
}

const MARCAS = '/admin/catalogo/marcas';
const CATEGORIAS = '/admin/catalogo/categorias';
const PRODUCTOS = '/admin/catalogo/productos';

test('Sin marcas, la pantalla invita a crear la primera', async ({ page }) => {
  const record = themeRecorder(page, 'catalogo');
  await loginAsE2eAdmin(page);
  await page.goto(MARCAS);

  const filas = filasConDatos(page);

  // Si esto falla, no es que el estado vacío esté roto: es que otra prueba
  // creó marcas antes. Se dice explícitamente en vez de dejar que la
  // aserción de abajo pase en vacío o falle sin explicar por qué.
  expect(
    await filas.count(),
    'esta prueba necesita empezar sin marcas — ¿otra las creó antes?',
  ).toBe(0);

  // Vacía, no rota: dice qué falta y ofrece la acción, sin parecer un error.
  await expect(page.getByText('Todavía no hay marcas')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Crear la primera marca' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('marcas-sin-ninguna-todavia');
});

test('Sin categorías, la pantalla invita a crear la primera', async ({ page }) => {
  const record = themeRecorder(page, 'categorias');
  await loginAsE2eAdmin(page);
  await page.goto(CATEGORIAS);

  expect(
    await filasConDatos(page).count(),
    'esta prueba necesita empezar sin categorías — ¿otra las creó antes?',
  ).toBe(0);

  await expect(page.getByText('Todavía no hay categorías')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Crear la primera categoría' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('categorias-sin-ninguna-todavia');
});

test('Sin productos, la pantalla invita a crear el primero', async ({ page }) => {
  const record = themeRecorder(page, 'productos');
  await loginAsE2eAdmin(page);
  await page.goto(PRODUCTOS);

  expect(
    await filasConDatos(page).count(),
    'esta prueba necesita empezar sin productos — ¿otra los creó antes?',
  ).toBe(0);

  await expect(page.getByText('Todavía no hay productos')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Crear el primer producto' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('productos-sin-ninguno-todavia');
});

test('El sitio recién instalado tiene nombre, sin que nadie lo escriba dos veces', async ({
  page,
}) => {
  // **La instalación obliga a poner el nombre del negocio, así que la portada
  // no puede salir sin él.** Hasta hoy sí podía: el nombre iba a la fila de la
  // instalación y el ajuste público se quedaba en `PENDIENTE_DEFINIR` hasta
  // que alguien lo editara en Configuración — el mismo dato en dos sitios, y
  // el que se quedaba atrás era el que ve el público.
  //
  // Esta prueba existe para que esa rama vuelva a ser inalcanzable: si alguien
  // separa otra vez los dos caminos, la portada se queda sin encabezado y esto
  // se pone rojo.
  const ajuste = await (await page.request.get('/api/settings/public')).json();

  expect(
    (ajuste as Record<string, string>).business_name,
    'el ajuste público business_name se quedó en el marcador del seed',
  ).not.toBe('PENDIENTE_DEFINIR');

  // **Sin sesión y sin válvula.** Visitar la tienda como visitante anónimo ya
  // no deja ningún error de consola: «quién soy» responde 200 con nulo, y el
  // token CSRF solo se pide cuando hay sesión.
  await page.goto('/');
  await expect(
    page.getByRole('heading', { level: 1 }),
    'la portada de un sitio instalado no enseña el nombre del negocio',
  ).toBeVisible();
});

/**
 * **La mitad vacía de la portada de M02.** Su mitad con datos vive en
 * `contenido.spec.ts`, y la separación no es de estilo:
 *
 * Los cuatro bloques de `cmsHome.tsx` devuelven `null` cuando su lista llega
 * vacía (`cmsHome.tsx:56`, `:120`, `:186`, `:249`). Afirmar eso **después** de
 * que alguien cree un banner no lo comprueba: lo hace imposible. Y afirmarlo
 * en el mismo archivo que crea los datos lo dejaría pasando en solitario y
 * fallando en la suite, que es exactamente el fallo que dio nombre a este
 * archivo.
 */
test('Sin contenido publicado, la portada no enseña ningún bloque de M02', async ({ page }) => {
  // **Primero se ancla en algo positivo.** Sin esto la prueba entera es la
  // clase de aserción de ausencia que este arnés ya cazó una vez: cuatro
  // `toHaveCount(0)` sobre un `body` a medio pintar pasan solas.
  await page.goto('/');
  await expect(
    page.getByRole('heading', { level: 1 }),
    'la portada no llegó a pintar, así que lo de abajo no afirma nada',
  ).toBeVisible();

  // Y segundo: que M02 **esté activo**. Un bloque ausente porque el módulo
  // está apagado no dice nada sobre el estado vacío. Esta es la comprobación
  // que convierte las cuatro de abajo en una afirmación.
  const capacidades = (await (await page.request.get('/api/capabilities')).json()) as {
    modules: { code: string }[];
  };
  expect(
    capacidades.modules.map((modulo) => modulo.code),
    'M02 no está activo: los bloques faltarían por eso y no por estar vacíos',
  ).toContain('cms');

  for (const titulo of ['Novedades', 'Promociones', 'Productos destacados', 'Trabajos destacados']) {
    await expect(
      page.getByRole('heading', { name: titulo, level: 2 }),
      `sin contenido, «${titulo}» no debería aparecer en la portada`,
    ).toHaveCount(0);
  }

  // **Y ningún contenedor residual**, que es la otra mitad del criterio: un
  // bloque puede desaparecer y dejar su `<section>` vacía ocupando su hueco
  // vertical. Se cuentan las cuatro por su `aria-labelledby`, que es lo único
  // que las identifica sin depender del texto.
  for (const id of ['cms-banners', 'cms-promotions', 'cms-featured-products', 'cms-featured-projects']) {
    await expect(
      page.locator(`section[aria-labelledby="${id}-title"]`),
      `«${id}» dejó su sección pintada sin contenido dentro`,
    ).toHaveCount(0);
  }
});

/**
 * **El catálogo vacío no promete un catálogo.**
 *
 * `catalogHome` pintaba siempre «Nuestra tienda — Mira todo lo que tenemos
 * publicado» y declaraba siempre `'con-contenido'`, sin consultar nada. Con
 * M01 activo y cero productos públicos eso era falso dos veces: invitaba a una
 * lista vacía, y de paso impedía que la portada llegara nunca a su estado
 * vacío, porque el resumen ya tenía un aporte.
 *
 * Vive aquí por lo mismo que el resto del archivo: **es el único momento de la
 * suite en que el catálogo está de verdad vacío**. Afirmarlo después de que
 * alguien publique un producto no lo comprueba, lo hace imposible.
 */
test('Con el catálogo vacío, la portada no invita a verlo', async ({ page }) => {
  // El ancla positiva primero: cuatro aserciones de ausencia sobre una página
  // a medio pintar pasan solas.
  await page.goto('/');
  await expect(
    page.getByRole('heading', { level: 1 }),
    'la portada no llegó a pintar, así que lo de abajo no afirma nada',
  ).toBeVisible();

  // Y que M01 **esté activo**: una sección ausente porque el módulo está
  // apagado no dice nada sobre el catálogo vacío. Es lo que convierte las dos
  // de abajo en una afirmación, y es justo lo que separa este caso del de
  // `zz-desmontaje.spec.ts`.
  const capacidades = (await (await page.request.get('/api/capabilities')).json()) as {
    modules: { code: string }[];
  };
  expect(
    capacidades.modules.map((modulo) => modulo.code),
    'M01 no está activo: la sección faltaría por eso y no por estar vacío el catálogo',
  ).toContain('catalog');

  await expect(
    page.getByText('Nuestra tienda', { exact: true }),
    'la portada anuncia la tienda sin un solo producto publicado',
  ).toHaveCount(0);
  await expect(
    page.getByRole('link', { name: 'Ver el catálogo', exact: true }),
    'la portada enlaza a un catálogo vacío',
  ).toHaveCount(0);

  // **Y aun así la portada no se declara vacía**, porque M04 sí aporta. Es la
  // otra mitad: que M01 deje de aportar no puede arrastrar a la portada al
  // estado vacío mientras otro módulo tenga algo que enseñar.
  await expect(
    page.getByText('Cuenta de cliente', { exact: true }),
    'M04 no está aportando: el caso de abajo no probaría nada',
  ).toBeVisible();
  await expect(
    page.getByText('Todavía no hay contenido publicado.'),
    'la portada se declara vacía mientras M04 pinta su sección',
  ).toHaveCount(0);
});

/**
 * Y el caso que solo existe desde que M01 puede decir «no tengo nada»: la
 * portada llega a su estado vacío **con el catálogo instalado y activo**.
 *
 * Antes era inalcanzable sin desactivar M01, que es lo que hace el canario de
 * `contenido.spec.ts`. La diferencia importa: con el módulo apagado la sección
 * ni se monta, así que aquello no comprueba lo que `catalogHome` declara.
 * Aquí sí, y es la única prueba que cazaría un `catalogHome` que dijera
 * `'con-contenido'` sin pintar nada.
 *
 * Se apaga M04 y no M02: los cuatro bloques de M02 ya declaran vacío solos
 * mientras no haya contenido publicado, y a esta altura de la suite no lo hay.
 * El único que aporta sin depender de datos es M04.
 */
test('Con el catálogo vacío y nadie más aportando, la portada lo dice', async ({ page }) => {
  test.setTimeout(180_000);

  await loginAsE2eAdmin(page);
  await cambiarModulo(page, 'crm', 'Desactivar');

  try {
    await page.goto('/');

    await expect(
      page.getByText('Todavía no hay contenido publicado.'),
      'la portada se quedó muda: ni contenido ni aviso',
    ).toBeVisible();

    // **Con M01 activo mientras lo dice.** Sin esto la prueba pasaría igual
    // con el catálogo desactivado y no probaría nada nuevo.
    const capacidades = (await (await page.request.get('/api/capabilities')).json()) as {
      modules: { code: string }[];
    };
    expect(
      capacidades.modules.map((modulo) => modulo.code),
      'M01 no está activo: el aviso saldría por eso y no por el catálogo vacío',
    ).toContain('catalog');
  } finally {
    await cambiarModulo(page, 'crm', 'Activar');
  }

  await expect(page.locator('#modulo-crm')).toContainText('Activo');
});

async function cambiarModulo(
  page: import('@playwright/test').Page,
  codigo: string,
  accion: 'Activar' | 'Desactivar',
): Promise<void> {
  await page.goto('/admin/modulos');

  await duringExpectedOutage(page, async () => {
    await page.locator(`#modulo-${codigo}`).getByRole('switch').click();
    await page.getByRole('alertdialog').getByRole('button', { name: new RegExp(`^${accion}`) }).click();

    const overlay = page.getByRole('alertdialog', { name: 'Aplicando el cambio' });
    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({ timeout: 90_000 });
  });
}

/**
 * **El pie, que tampoco existe cuando no hay nada que poner dentro.**
 *
 * Vive aquí por lo mismo que el resto del archivo: es el único momento de la
 * suite en que M02 está activo y sin una sola red publicada. Afirmarlo después
 * de que alguien cree una no lo comprueba, lo hace imposible.
 *
 * El criterio es que **nadie vea ni lea un pie vacío**. El elemento sí está
 * en el documento, y eso fue una concesión con causa: hacerlo aparecer y
 * desaparecer cambia el padre de las contribuciones, las remonta, y el
 * remontaje las devuelve a «cargando» — el pie se va, vuelven a montar, y así
 * sin parar. Con `hidden` el elemento es estable y dentro solo cambia el
 * contenido.
 *
 * `getByRole` no lo ve porque `hidden` lo saca del árbol de accesibilidad, que
 * es exactamente lo que se quiere afirmar: para un lector de pantalla y para
 * el ojo, ahí no hay pie. Tampoco ocupa alto.
 */
test('Con M02 activo y sin redes publicadas, no hay pie en el documento', async ({ page }) => {
  // El ancla positiva primero: una aserción de ausencia sobre una página a
  // medio pintar pasa sola.
  await page.goto('/');
  await expect(
    page.getByRole('heading', { level: 1 }),
    'la portada no llegó a pintar, así que lo de abajo no afirma nada',
  ).toBeVisible();

  // Y que M02 **esté activo**: un pie ausente porque el módulo está apagado no
  // dice nada sobre el estado vacío. Es lo que convierte lo de abajo en una
  // afirmación.
  const capacidades = (await (await page.request.get('/api/capabilities')).json()) as {
    modules: { code: string }[];
  };
  expect(
    capacidades.modules.map((modulo) => modulo.code),
    'M02 no está activo: el pie faltaría por eso y no por no tener redes',
  ).toContain('cms');

  await expect(
    page.getByRole('contentinfo'),
    'el pie se pintó sin una sola red publicada',
  ).toHaveCount(0);
  await expect(
    page.getByRole('navigation', { name: 'Redes sociales' }),
    'quedó el bloque de redes sin ninguna red dentro',
  ).toHaveCount(0);

  // **Y ningún aviso de que el pie está vacío.** El visitante no tiene nada
  // que hacer con esa información; el pie sencillamente no está.
  await expect(
    page.getByText(/pie|footer/i),
    'la portada explica que el pie está vacío en vez de no ponerlo',
  ).toHaveCount(0);
});

/**
 * **Y durante la carga tampoco parpadea.**
 *
 * Es la misma regla que la portada aplica al aviso de vacío: lo que aún no se
 * sabe no se cuenta. Sin ella el pie aparecería al llegar la respuesta y
 * desaparecería si viene vacía, o al revés — un salto de la página en cada
 * visita.
 *
 * La respuesta se retrasa a propósito: sin retraso la carga dura milisegundos
 * y la prueba afirmaría sobre un estado que no llegó a existir.
 */
test('Mientras las redes se cargan, el pie no aparece ni parpadea', async ({ page }) => {
  await page.route('**/api/cms/social-links', async (route) => {
    await new Promise((listo) => setTimeout(listo, 3_000));
    await route.continue();
  });

  await page.goto('/');
  await expect(
    page.getByRole('heading', { level: 1 }),
    'la portada no llegó a pintar, así que lo de abajo no afirma nada',
  ).toBeVisible();

  // Durante la espera: nada de pie, y nada de aviso.
  await expect(
    page.getByRole('contentinfo'),
    'el pie se pintó antes de saber si había algo que poner dentro',
  ).toHaveCount(0);

  // Y cuando la respuesta llega vacía, sigue sin haberlo: ni un parpadeo.
  await page.waitForTimeout(4_000);
  await expect(
    page.getByRole('contentinfo'),
    'el pie apareció al asentarse una respuesta sin redes',
  ).toHaveCount(0);
});
