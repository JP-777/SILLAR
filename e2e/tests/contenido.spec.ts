import type { APIRequestContext, Locator, Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * M02 · Los recorridos de las pantallas nuevas.
 *
 * Existe porque **394 pruebas en verde decían que nada se había roto y nada
 * sobre si lo nuevo funciona**: hasta hoy el arnés ni aplicaba las
 * migraciones de M02 (`e2e/setup/migrate.ts`) ni activaba el módulo
 * (`e2e/setup/global-setup.ts`), así que ninguna de sus cinco pantallas se
 * había cargado una sola vez.
 *
 * **Todo se afirma por el efecto observable, no por el 200.** Que un `PUT`
 * responda bien y la tabla siga enseñando lo de antes es exactamente el fallo
 * que estas pruebas tienen que ver. Por eso ninguna comprueba una respuesta
 * del API salvo cuando el API es el *escenario* —crear un producto que luego
 * se destaca— y no lo que se está probando.
 *
 * **La mitad vacía de la portada no está aquí**, está en `aa-vacios.spec.ts`.
 * Los cuatro bloques desaparecen cuando su lista llega vacía, y afirmarlo en
 * un archivo que crea contenido no lo comprueba: lo hace imposible.
 */

const BANNERS = '/admin/contenido/banners';
const REDES = '/admin/contenido/redes-sociales';
const DESTACADOS = '/admin/contenido/productos-destacados';

/** El PNG más pequeño que se puede subir. En memoria: nada de binarios en el repositorio. */
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

/** El sello que separa esta corrida de cualquier fila que dejara otra prueba. */
const SELLO = Date.now();

async function csrf(api: APIRequestContext): Promise<Record<string, string>> {
  const token = (await (await api.get('/api/admin/auth/csrf')).json()) as { csrfToken: string };
  return { 'X-CSRF-Token': token.csrfToken };
}

/** Sube una imagen a la galería de CORE y devuelve el nombre con el que se verá. */
async function subirImagen(api: APIRequestContext, nombre: string): Promise<string> {
  const respuesta = await api.post('/api/admin/media', {
    headers: await csrf(api),
    multipart: {
      ownerModuleCode: 'cms',
      file: { name: nombre, mimeType: 'image/png', buffer: PNG },
    },
  });

  expect(respuesta.ok(), `subir «${nombre}»: ${respuesta.status()} ${await respuesta.text()}`).toBe(true);
  return nombre;
}

/**
 * El `.ui-field` cuya etiqueta es exactamente ésta.
 *
 * Hace falta para los dos `ImagePicker` del formulario de banner: los dos
 * enseñan la galería entera, así que un botón con el nombre del archivo
 * aparece dos veces.
 *
 * **Y ni `hasText` ni `filter({ has })` sirven aquí.** El primero casa los dos
 * campos, porque la pista del móvil dice «la imagen de escritorio». El
 * segundo pide un localizador relativo al elemento filtrado, y uno construido
 * desde el cajón arrastra su `role=dialog` delante, así que no casa nunca —
 * falla en silencio hasta agotar el tiempo. Se resuelve con `:has()` de CSS,
 * que se evalúa entero dentro del propio `.ui-field`.
 */
function campo(panel: Locator, etiqueta: string): Locator {
  return panel.locator(`.ui-field:has(> label.ui-field__label:text-is("${etiqueta}"))`);
}

/** Las filas con datos: `Table` pinta el estado vacío como una fila más. */
function filas(raiz: Page | Locator): Locator {
  return raiz.locator('tbody tr').filter({ hasNot: raiz.locator('.ui-table__state') });
}

/** Los títulos de los cuatro bloques de portada, en el orden en que se componen. */
const BLOQUES = ['Novedades', 'Promociones', 'Productos destacados', 'Trabajos destacados'] as const;

/* ========================================================================
 * 1 · Banners: crear → editar → reordenar → desactivar
 * ===================================================================== */

test('Un banner se crea, se edita, cambia de sitio y se desactiva, y la portada lo sigue', async ({
  page,
}) => {
  const record = themeRecorder(page, 'contenido-banners');
  await loginAsE2eAdmin(page);

  const foto = await subirImagen(page.request, `banner-${SELLO}.png`);
  const primero = `Vuelta al cole ${SELLO}`;
  const segundo = `Liquidacion de verano ${SELLO}`;

  await page.goto(BANNERS);

  // --- Crear ------------------------------------------------------------
  await crearBanner(page, { titulo: primero, alt: 'Estanteria con cuadernos', foto });

  const filaPrimero = page.locator('tbody tr').filter({ hasText: primero });
  await expect(filaPrimero, 'el banner recién creado no aparece en la tabla').toBeVisible();
  // Vigente, no solo creado: sin ventana de fechas nace publicándose, y es lo
  // que hace que la portada de más abajo tenga algo que enseñar.
  await expect(filaPrimero.getByText('Vigente')).toBeVisible();
  await expect(filaPrimero.getByText('Activo')).toBeVisible();

  await record('banner-recien-creado');

  // --- Editar -----------------------------------------------------------
  //
  // El subtítulo, y no el título: el título es lo que localiza la fila, así
  // que cambiarlo dejaría la aserción buscando algo que ella misma movió.
  const subtitulo = `Hasta el 30 de marzo ${SELLO}`;
  await filaPrimero.getByRole('button', { name: 'Editar' }).click();

  const edicion = page.getByRole('dialog');
  await expect(edicion).toBeVisible();
  await edicion.getByLabel('Subtítulo', { exact: true }).fill(subtitulo);
  await edicion.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(edicion, 'tras «Guardar cambios» el cajón no se cerró').toBeHidden();

  await expect(
    filaPrimero.getByText(subtitulo),
    'el subtítulo guardado no llegó a la tabla',
  ).toBeVisible();

  // --- Reordenar --------------------------------------------------------
  await crearBanner(page, { titulo: segundo, alt: 'Mochilas en oferta', foto });

  await expect(filas(page), 'hacen falta los dos banners para poder reordenar').toHaveCount(2);
  await expect(filas(page).first()).toContainText(primero);

  await filaPrimero.getByRole('button', { name: 'Bajar' }).click();

  // **El efecto es el orden de la tabla**, no el aviso: un `PUT /order` que
  // responde 200 y devuelve la lista como estaba pasa igual el aviso.
  await expect(
    filas(page).first(),
    'tras «Bajar», el primer banner sigue siendo el mismo',
  ).toContainText(segundo);
  await expect(filas(page).nth(1)).toContainText(primero);

  await record('banners-reordenados');

  // --- Y la portada lleva los dos, en ese mismo orden --------------------
  await enPortada(page, 'Novedades', [segundo, primero]);
  await page.goto(BANNERS);

  // --- Desactivar -------------------------------------------------------
  await filaPrimero.getByRole('button', { name: 'Desactivar', exact: true }).click();

  const confirmacion = page.getByRole('alertdialog', { name: 'Desactivar banner' });
  await expect(confirmacion).toBeVisible();
  await confirmacion.getByRole('button', { name: 'Desactivar banner' }).click();
  await expect(confirmacion).toBeHidden();

  // **Por columna, no por texto.** Al desactivar, la fila dice «Inactivo» dos
  // veces —la insignia de publicación y la editorial usan la misma palabra—,
  // así que buscarlo por texto casa dos elementos y la aserción muere por
  // modo estricto. Editorial es la tercera columna (`BannersPage.tsx:79`).
  await expect(
    filaPrimero.locator('td').nth(2),
    'el banner desactivado sigue marcado como activo en la columna editorial',
  ).toHaveText('Inactivo');
  // La fila **se queda**: desactivar no es borrar, y es lo que promete el
  // propio diálogo («La fila y su posición se conservan en administración»).
  await expect(filas(page)).toHaveCount(2);

  await record('banner-desactivado');

  // Y en la portada ya no está, mientras el otro sigue.
  await enPortada(page, 'Novedades', [segundo]);
  await expect(
    page.getByRole('heading', { name: primero }),
    'el banner desactivado sigue publicándose en la portada',
  ).toHaveCount(0);
});

/** Crea un banner por pantalla, con imagen y texto alternativo — los dos que hacen falta para publicarlo. */
async function crearBanner(
  page: Page,
  datos: { titulo: string; alt: string; foto: string },
): Promise<void> {
  await page.getByRole('button', { name: /^(Nuevo banner|Crear el primer banner)$/ }).first().click();

  const cajon = page.getByRole('dialog');
  await expect(cajon).toBeVisible();

  await cajon.getByLabel('Título', { exact: true }).fill(datos.titulo);
  await cajon.getByLabel('Texto alternativo', { exact: true }).fill(datos.alt);
  await campo(cajon, 'Imagen de escritorio').getByRole('button', { name: datos.foto }).click();

  await cajon.getByRole('button', { name: 'Crear banner' }).click();
  await expect(cajon, `tras «Crear banner» el cajón de «${datos.titulo}» no se cerró`).toBeHidden();
}

/* ========================================================================
 * 2 · Redes sociales: crear → desactivar → reactivar la MISMA fila
 * ===================================================================== */

test('Reactivar una red social recupera su misma fila, no crea otra', async ({ page }) => {
  const record = themeRecorder(page, 'contenido-redes');
  await loginAsE2eAdmin(page);
  await page.goto(REDES);

  // Dos, y en este orden: con una sola no se puede ver que la reactivada
  // vuelve **a su sitio**, que es la mitad del criterio.
  const urlInstagram = `https://instagram.com/sillar-${SELLO}`;
  await crearRed(page, 'Instagram', urlInstagram);
  await crearRed(page, 'WhatsApp', `https://wa.me/519${SELLO % 100000000}`);

  await expect(filas(page)).toHaveCount(2);
  const instagram = page.locator('tbody tr').filter({ hasText: urlInstagram });

  // **El antes se lee del API, no de la pantalla**, porque el identificador
  // no se enseña nunca (y no debe): es la única forma de afirmar «la misma
  // fila» y no «una fila que se le parece».
  // **Se busca por la dirección, no por el nombre de la red.** El listado
  // devuelve `platform` como el valor del selector —en minúscula— mientras la
  // tabla lo enseña como llega; casarlo por nombre depende de una convención
  // de mayúsculas que no es de esta prueba. La dirección la escribió ella.
  const antes = await leerRedes(page.request);
  const instagramAntes = antes.find((red) => red.url === urlInstagram);
  expect(instagramAntes, 'no se encontró la red recién creada en el listado').toBeTruthy();

  await record('redes-las-dos-activas');

  // --- Desactivar -------------------------------------------------------
  await instagram.getByRole('button', { name: 'Desactivar', exact: true }).click();
  const confirmacion = page.getByRole('alertdialog', { name: 'Desactivar red social' });
  await confirmacion.getByRole('button', { name: 'Desactivar red social' }).click();
  await expect(confirmacion).toBeHidden();

  await expect(instagram.getByText('Inactiva')).toBeVisible();
  await expect(
    instagram.getByRole('button', { name: 'Reactivar' }),
    'una red inactiva no ofrece cómo volver a activarla',
  ).toBeVisible();

  // Y desaparece del footer público mientras está inactiva.
  await page.goto('/');
  await expect(
    page.getByRole('link', { name: 'Instagram' }),
    'la red desactivada sigue enlazada en el footer',
  ).toHaveCount(0);
  await page.goto(REDES);

  await record('red-desactivada');

  // --- Reactivar --------------------------------------------------------
  await instagram.getByRole('button', { name: 'Reactivar' }).click();
  await expect(instagram.getByText('Activa', { exact: true })).toBeVisible();

  // **Y aquí está el criterio**: mismo identificador, mismo contenido, misma
  // posición. Reactivar podría estar implementado como «crear otra igual» y
  // la pantalla se vería idéntica; lo único que las distingue es esto.
  const despues = await leerRedes(page.request);
  expect(despues, 'reactivar dejó un número distinto de redes').toHaveLength(antes.length);

  const instagramDespues = despues.find((red) => red.id === instagramAntes!.id);
  expect(
    instagramDespues,
    'la fila reactivada tiene otro identificador: se recreó en vez de recuperarse',
  ).toBeTruthy();
  expect(instagramDespues!.url, 'la reactivada perdió su dirección').toBe(instagramAntes!.url);
  expect(instagramDespues!.isActive, 'la reactivada no quedó activa').toBe(true);
  expect(
    despues.findIndex((red) => red.id === instagramAntes!.id),
    'la reactivada volvió en otra posición de la lista',
  ).toBe(antes.findIndex((red) => red.id === instagramAntes!.id));

  await record('red-reactivada');
});

async function crearRed(page: Page, plataforma: string, url: string): Promise<void> {
  await page.getByRole('button', { name: /^(Nueva red social|Añadir la primera red)$/ }).first().click();

  const cajon = page.getByRole('dialog');
  await expect(cajon).toBeVisible();
  await cajon.getByLabel(/^Red social/).selectOption({ label: plataforma });
  await cajon.getByLabel(/^Dirección/).fill(url);
  await cajon.getByRole('button', { name: 'Añadir red social' }).click();
  await expect(cajon, `tras añadir «${plataforma}» el cajón no se cerró`).toBeHidden();
}

async function leerRedes(api: APIRequestContext) {
  const respuesta = await api.get('/api/admin/cms/social-links');
  expect(respuesta.ok(), `listar redes: ${respuesta.status()}`).toBe(true);
  return (await respuesta.json()) as { id: number; platform: string; url: string; isActive: boolean }[];
}

/* ========================================================================
 * 3 · Productos destacados: buscar → elegir → destacar → actualizar
 * ===================================================================== */

test('Un producto se busca, se destaca y su snapshot se pone al día cuando el catálogo cambia', async ({
  page,
}) => {
  const record = themeRecorder(page, 'contenido-destacados');
  await loginAsE2eAdmin(page);

  // El escenario sí por API: lo que se prueba es la pantalla de M02, no la de
  // M01, que ya tiene las suyas.
  const nombre = `Zepelin escolar ${SELLO}`;
  const producto = await crearProducto(page.request, nombre, `zepelin-escolar-${SELLO}`);

  await page.goto(DESTACADOS);
  await page.getByRole('button', { name: /^(Destacar producto|Destacar el primer producto)$/ }).first().click();

  const cajon = page.getByRole('dialog');
  await expect(cajon).toBeVisible();

  // --- Buscar -----------------------------------------------------------
  //
  // Palabra completa: la búsqueda de M01 va por `to_tsvector` y **no encuentra
  // prefijos**, cosa que el propio campo advierte por escrito.
  await cajon.getByLabel(/^Buscar producto/).fill('zepelin');
  await cajon.getByRole('button', { name: 'Buscar' }).click();

  const resultado = cajon.locator('li').filter({ hasText: nombre });
  await expect(resultado, 'la búsqueda no encontró el producto recién creado').toBeVisible();

  // --- Elegir -----------------------------------------------------------
  //
  // El botón dice qué pasó: «Elegir» pasa a «Elegido». Se afirma sobre eso y
  // sobre `aria-pressed`, que es lo que lo cuenta a quien no ve la pantalla.
  const elegir = resultado.getByRole('button', { name: 'Elegir' });
  await expect(elegir).toHaveAttribute('aria-pressed', 'false');
  await elegir.click();

  const elegido = resultado.getByRole('button', { name: 'Elegido' });
  await expect(elegido, 'elegir un resultado no cambió el botón').toBeVisible();
  await expect(elegido).toHaveAttribute('aria-pressed', 'true');
  await expect(cajon.getByText('Producto elegido')).toBeVisible();

  await record('destacado-producto-elegido');

  // --- Destacar ---------------------------------------------------------
  await cajon.getByRole('button', { name: 'Destacar producto' }).click();
  await expect(cajon, 'tras «Destacar producto» el cajón no se cerró').toBeHidden();

  const fila = page.locator('tbody tr').filter({ hasText: nombre });
  await expect(fila, 'el producto destacado no aparece en la tabla').toBeVisible();
  await expect(fila.getByText('Vinculado')).toBeVisible();
  await expect(fila.getByText('Destacado activo')).toBeVisible();

  // **Y no ofrece reenlazar**, porque el vínculo está vivo. Es una ausencia,
  // pero anclada: la fila de arriba ya está pintada y sus otras insignias
  // afirmadas, así que esto no puede cumplirse en vacío.
  await expect(
    fila.getByRole('button', { name: 'Volver a enlazar' }),
    'ofrece reenlazar un producto cuyo vínculo está intacto',
  ).toHaveCount(0);
  await expect(fila.getByRole('button', { name: 'Actualizar datos' })).toBeVisible();

  await record('destacado-recien-creado');

  // --- El snapshot no envejece en silencio ------------------------------
  //
  // Escribí esto al revés la primera vez: afirmaba que renombrar en M01 **no**
  // cambiaba el destacado hasta que alguien pulsara, y falló. La costura está
  // hecha justamente para que no haga falta pulsar — `ProductoActualizado`
  // tiene su manejador en CMS (`CmsModule.cs:47`) y rehace el snapshot.
  //
  // Se deja escrito porque es el criterio que de verdad importa de un dato
  // copiado: **que no se quede viejo sin que nadie se entere.**
  const renombrado = `${nombre} reeditado`;
  await renombrarProducto(page.request, producto, renombrado, `zepelin-escolar-${SELLO}`);

  await page.goto(DESTACADOS);
  await expect(
    page.locator('tbody tr').filter({ hasText: renombrado }),
    'renombrar el producto en M01 no llegó al destacado de M02',
  ).toBeVisible();

  await record('destacado-al-dia-por-evento');

  // --- Y la relectura a mano sigue estando, para cuando el evento no llegue -
  //
  // El bus es en proceso, serial y sin reintentos: un manejador que falle deja
  // el snapshot atrás y **nadie lo reintenta**. Por eso hay un botón, y por eso
  // se ejercita. Su desenlace visible es el recuento, no un 200.
  await page.getByRole('button', { name: 'Actualizar todos' }).click();

  const recuento = page.getByText('Actualización terminada');
  await expect(recuento, '«Actualizar todos» no dijo en qué quedó').toBeVisible();
  await expect(
    page.getByText(/Pendientes de reenlace: 0/),
    'la reconciliación dejó destacados pendientes de reenlace sin que nada se borrara',
  ).toBeVisible();

  await record('destacados-reconciliados');
});

async function crearProducto(api: APIRequestContext, name: string, slug: string): Promise<string> {
  const respuesta = await api.post('/api/admin/catalog/products', {
    headers: await csrf(api),
    data: {
      name,
      slug,
      shortDescription: null,
      description: null,
      primaryCategoryId: null,
      categoryIds: null,
      brandId: null,
      listPrice: 24.9,
      saleUnit: null,
      variantLabel: null,
      code: null,
      barcode: null,
    },
  });

  expect(respuesta.ok(), `crear «${name}»: ${respuesta.status()} ${await respuesta.text()}`).toBe(true);
  return ((await respuesta.json()) as { id: string }).id;
}

async function renombrarProducto(
  api: APIRequestContext,
  id: string,
  name: string,
  slug: string,
): Promise<void> {
  const actual = (await (await api.get(`/api/admin/catalog/products/${id}`)).json()) as Record<
    string,
    unknown
  >;

  const respuesta = await api.put(`/api/admin/catalog/products/${id}`, {
    headers: await csrf(api),
    data: { ...actual, name, slug },
  });

  expect(respuesta.ok(), `renombrar el producto: ${respuesta.status()} ${await respuesta.text()}`).toBe(
    true,
  );
}

/* ========================================================================
 * 4 · La portada con contenido: cada bloque aparece cuando lo tiene
 * ===================================================================== */

test('Con contenido de los cuatro tipos, la portada enseña los cuatro bloques', async ({ page }) => {
  const record = themeRecorder(page, 'contenido-portada');
  await loginAsE2eAdmin(page);
  const api = page.request;

  // La mitad vacía de este criterio está en `aa-vacios.spec.ts` y **tiene que
  // haber corrido antes**: en cuanto exista una promoción, «sin contenido no
  // hay bloque» deja de poder comprobarse. El prefijo `aa-` es lo que lo
  // garantiza, y el motivo está escrito en la cabecera de aquel archivo.
  // **Los cuatro los crea esta prueba**, aunque los recorridos de arriba ya
  // dejen banners y destacados. Depender de lo que otra prueba dejó hace que
  // ésta pase o falle según el orden y según si aquélla llegó al final — y una
  // prueba que se rompe por el fallo de otra no dice nada de lo suyo.
  const imagen = await subirImagenId(api, `portada-${SELLO}.png`);
  const banner = `Temporada escolar ${SELLO}`;
  const promocion = `Dos por uno ${SELLO}`;
  const trabajo = `Sellos personalizados ${SELLO}`;
  const destacado = `Regla milimetrada ${SELLO}`;

  await crearBannerPorApi(api, banner, imagen);
  await crearPromocion(api, promocion, imagen);
  await crearTrabajo(api, trabajo, imagen);
  await destacarPorApi(api, await crearProducto(api, destacado, `regla-milimetrada-${SELLO}`));

  await page.goto('/');

  // Los cuatro, con lo que cada uno tiene dentro: un bloque puede pintar su
  // título y quedarse sin tarjetas, y eso también es estar roto.
  for (const bloque of BLOQUES) {
    await expect(
      page.getByRole('heading', { name: bloque, level: 2 }),
      `el bloque «${bloque}» no aparece aunque tiene contenido`,
    ).toBeVisible();
  }

  for (const contenido of [banner, promocion, trabajo, destacado]) {
    await expect(
      page.getByRole('heading', { name: contenido, level: 3 }),
      `«${contenido}» no llegó a su tarjeta de la portada`,
    ).toBeVisible();
  }

  await record('portada-con-los-cuatro-bloques');
});

async function subirImagenId(api: APIRequestContext, nombre: string): Promise<string> {
  const respuesta = await api.post('/api/admin/media', {
    headers: await csrf(api),
    multipart: {
      ownerModuleCode: 'cms',
      file: { name: nombre, mimeType: 'image/png', buffer: PNG },
    },
  });

  expect(respuesta.ok(), `subir «${nombre}»: ${respuesta.status()}`).toBe(true);
  return ((await respuesta.json()) as { mediaAssetId: string }).mediaAssetId;
}

async function crearBannerPorApi(
  api: APIRequestContext,
  title: string,
  imageDesktopId: string,
): Promise<void> {
  const respuesta = await api.post('/api/admin/cms/banners', {
    headers: await csrf(api),
    data: {
      title,
      subtitle: null,
      imageDesktopId,
      imageMobileId: null,
      altText: 'Utiles sobre un pupitre',
      linkUrl: null,
      linkLabel: null,
      startsAt: null,
      endsAt: null,
    },
  });

  expect(respuesta.ok(), `crear el banner: ${respuesta.status()} ${await respuesta.text()}`).toBe(true);
}

async function destacarPorApi(api: APIRequestContext, productId: string): Promise<void> {
  const respuesta = await api.post('/api/admin/cms/featured-products', {
    headers: await csrf(api),
    data: { productId, startsAt: null, endsAt: null },
  });

  expect(respuesta.ok(), `destacar el producto: ${respuesta.status()} ${await respuesta.text()}`).toBe(
    true,
  );
}

async function crearPromocion(api: APIRequestContext, title: string, imageId: string): Promise<void> {
  const respuesta = await api.post('/api/admin/cms/promotions', {
    headers: await csrf(api),
    data: {
      title,
      subtitle: null,
      description: 'Válido hasta agotar existencias.',
      badgeText: 'Oferta',
      imageId,
      altText: 'Cuadernos apilados',
      linkUrl: null,
      linkLabel: null,
      startsAt: null,
      endsAt: null,
    },
  });

  expect(respuesta.ok(), `crear la promoción: ${respuesta.status()} ${await respuesta.text()}`).toBe(true);
}

async function crearTrabajo(api: APIRequestContext, title: string, imageId: string): Promise<void> {
  const respuesta = await api.post('/api/admin/cms/featured-projects', {
    headers: await csrf(api),
    data: { title, description: 'Trabajo de imprenta.', imageId, altText: 'Sellos de goma' },
  });

  expect(respuesta.ok(), `crear el trabajo: ${respuesta.status()} ${await respuesta.text()}`).toBe(true);
}

/** Comprueba qué títulos enseña un bloque de la portada, y en qué orden. */
async function enPortada(page: Page, bloque: string, esperados: string[]): Promise<void> {
  await page.goto('/');

  const seccion = page.locator('section').filter({
    has: page.getByRole('heading', { name: bloque, level: 2 }),
  });
  await expect(seccion, `el bloque «${bloque}» no está en la portada`).toBeVisible();

  const titulos = seccion.getByRole('heading', { level: 3 });
  await expect(titulos, `«${bloque}» enseña otro número de tarjetas`).toHaveCount(esperados.length);

  for (const [indice, esperado] of esperados.entries()) {
    await expect(
      titulos.nth(indice),
      `«${bloque}»: la tarjeta ${indice + 1} no es la esperada`,
    ).toHaveText(esperado);
  }
}
/* ========================================================================
 * 5 · El pie público: la primera contribución de M02 fuera del panel
 * ===================================================================== */

/**
 * **La mitad con datos del pie.** Su mitad vacía vive en `aa-vacios.spec.ts`,
 * y la separación es la misma de siempre: afirmar que sin redes no hay pie en
 * un archivo que crea redes no lo comprueba, lo hace imposible.
 *
 * Va **antes** de la limpieza de abajo y no después, por lo que aquélla
 * advierte de sí misma: su bucle desactiva también `social-links`, así que una
 * red publicada después de ella se quedaría publicada y rompería las pruebas
 * de los archivos siguientes, no la suya.
 *
 * El pie es de la plataforma y no pone nada suyo — ni nombre del negocio, ni
 * copyright, ni aviso legal. Es un contenedor, y M02 es por ahora su único
 * contribuyente.
 *
 * **Se comprueba también en la tienda**, no solo en la portada: un pie que
 * aparece en la portada y desaparece al abrir el catálogo no es alcance
 * reducido, es un defecto que el visitante ve.
 */
test('Una red publicada aparece en el pie de toda la web pública', async ({ page }) => {
  const direccion = `https://www.tiktok.com/@sillar${SELLO}`;

  await loginAsE2eAdmin(page);
  await page.goto(REDES);
  await crearRed(page, 'TikTok', direccion);

  await page.goto('/');

  const pie = page.getByRole('contentinfo');
  await expect(pie, 'con una red publicada la portada sigue sin pie').toBeVisible();

  const enlace = pie.getByRole('link', { name: 'TikTok' });
  await expect(enlace, 'el enlace no dice a dónde lleva').toBeVisible();
  await expect(enlace).toHaveAttribute('href', direccion);

  // `noopener` no es adorno: sin él la página de destino recibe una referencia
  // a la nuestra por `window.opener`.
  await expect(enlace, 'enlace externo sin rel="noopener"').toHaveAttribute('rel', /noopener/);

  // **Y la plataforma no pone nada suyo dentro.**
  await expect(
    pie.getByText(/©|copyright|todos los derechos/i),
    'el pie de plataforma se inventó contenido propio',
  ).toHaveCount(0);

  // La tienda es otra ruta, y el pie tiene que seguir ahí.
  await page.goto('/catalogo');
  await expect(
    page.getByRole('contentinfo').getByRole('link', { name: 'TikTok' }),
    'el pie desaparece al salir de la portada',
  ).toBeVisible();
});


/**
 * **Qué mide esta prueba.** `PublicLayout` tiene que sobrevivir al cambio de
 * hijo `/` → `/catalogo` → `/`. Si se remonta, sus contribuyentes también se
 * remontan y M02 vuelve a pedir `/api/cms/social-links`. Por eso se toma una
 * línea base después del primer montaje y se exige que navegar entre hijos del
 * mismo layout añada **cero peticiones**.
 *
 * **Por qué se compara el delta y no un número absoluto.** El arnés levanta
 * Vite en desarrollo y la aplicación usa `StrictMode`; React ejecuta un ciclo
 * adicional de Effects en el montaje inicial. `useResource` carga desde un
 * Effect, así que la línea base puede contener más de una petición sin que haya
 * ocurrido navegación ni un remonte provocado por las rutas. Esa duplicación
 * pertenece al entorno de desarrollo; la propiedad que esta regresión vigila
 * es que el contador **no aumente después de estabilizar ese primer montaje**.
 *
 * **Qué NO mide.** No demuestra que React no vuelva a renderizar: puede hacerlo
 * todas las veces que necesite. Tampoco prueba cómo se construye el registro de
 * superficie ni fija cuántas veces ejecuta Effects `StrictMode`. Solo vigila la
 * propiedad observable de la que depende este diseño: navegar entre hijos del
 * mismo `PublicLayout` no desmonta y vuelve a montar al contribuyente del pie.
 */
test('Navegar por la web pública no remonta al contribuyente del pie', async ({ page }) => {
  const direccion = `https://www.youtube.com/@sillar${SELLO}`;

  await loginAsE2eAdmin(page);
  await page.goto(REDES);
  await crearRed(page, 'YouTube', direccion);

  // El producto no forma parte del comportamiento bajo prueba: únicamente
  // garantiza que la portada pinte su Link real a `/catalogo`, para que el
  // recorrido siguiente sea navegación SPA y no un `page.goto()`.
  await crearProducto(
    page.request,
    `Producto para navegación del pie ${SELLO}`,
    `producto-navegacion-pie-${SELLO}`,
  );

  let peticionesDeRedes = 0;
  page.on('request', (request) => {
    if (
      request.method() === 'GET'
      && new URL(request.url()).pathname === '/api/cms/social-links'
    ) {
      peticionesDeRedes += 1;
    }
  });

  // Única navegación completa: aquí nace el documento y se estabiliza su
  // primer montaje. A partir de este punto el contador es nuestra línea base.
  await page.goto('/');

  const pie = page.getByRole('contentinfo');
  const youtube = pie.getByRole('link', { name: 'YouTube' });

  await expect(pie, 'el pie no apareció en la portada').toBeVisible();
  await expect(youtube, 'la red publicada no llegó al pie de la portada').toBeVisible();
  await page.waitForLoadState('networkidle');

  const peticionesIniciales = peticionesDeRedes;
  expect(
    peticionesIniciales,
    'el montaje inicial no pidió los enlaces sociales',
  ).toBeGreaterThan(0);

  // Navegación SPA real: este Link pertenece a React Router. Usar page.goto()
  // aquí recargaría el documento y haría inútil la prueba de permanencia.
  await page.getByRole('link', { name: 'Ver el catálogo' }).click();
  await expect(page).toHaveURL(/\/catalogo$/);
  await expect(
    page.getByRole('contentinfo').getByRole('link', { name: 'YouTube' }),
    'el pie perdió su contenido al entrar al catálogo',
  ).toBeVisible();
  expect(
    peticionesDeRedes,
    'entrar al catálogo remontó el contribuyente y volvió a pedir las redes',
  ).toBe(peticionesIniciales);

  // El Link anterior añadió esta entrada al historial de React Router.
  // Volver cambia de hijo del mismo layout sin crear un documento nuevo.
  await page.goBack();
  await expect(page).toHaveURL(/\/$/);
  await expect(
    page.getByRole('contentinfo').getByRole('link', { name: 'YouTube' }),
    'el pie perdió su contenido al volver a la portada',
  ).toBeVisible();
  expect(
    peticionesDeRedes,
    'volver a la portada remontó el contribuyente y volvió a pedir las redes',
  ).toBe(peticionesIniciales);
});

/* ========================================================================
 * 6 · Y al retirar el contenido, la portada vuelve a como estaba
 * ===================================================================== */

/**
 * **El criterio de terminado de M02, y la limpieza de este archivo a la vez.**
 *
 * Las dos cosas son la misma: si desactivar todo el contenido dejara un hueco
 * —una sección vacía, un separador, un título sin tarjetas—, esta prueba lo
 * vería. Y mientras no lo deje, el archivo devuelve la portada al estado en
 * que la encontró.
 *
 * **Lo segundo no es cortesía, es necesario.** La prueba de aquí abajo afirma
 * que, apagados M01 y M04, la portada dice «Todavía no hay contenido
 * publicado.», y eso solo puede ser cierto si ningún otro módulo está
 * aportando. Sin esta limpieza, aquélla falla, **no llega a reactivar los
 * módulos**, y detrás caen otras que dan por hecho que el catálogo sigue
 * instalado. Una prueba que publica contenido y lo deja publicado no rompe la
 * suya: rompe las de después.
 *
 * (`tienda.spec.ts` afirma el caso complementario —con M04 aportando, el aviso
 * **no** sale— y depende de esta limpieza por el mismo motivo.)
 */
test('Al retirar todo el contenido, la portada vuelve a quedarse sin bloques', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const api = page.request;

  for (const coleccion of ['banners', 'promotions', 'featured-products', 'featured-projects', 'social-links']) {
    await desactivarTodo(api, coleccion);
  }

  await page.goto('/');
  await expect(
    page.getByRole('heading', { level: 1 }),
    'la portada no llegó a pintar, así que lo de abajo no afirma nada',
  ).toBeVisible();

  for (const bloque of BLOQUES) {
    await expect(
      page.getByRole('heading', { name: bloque, level: 2 }),
      `«${bloque}» sigue en la portada sin contenido que enseñar`,
    ).toHaveCount(0);
  }

  for (const id of ['cms-banners', 'cms-promotions', 'cms-featured-products', 'cms-featured-projects']) {
    await expect(
      page.locator(`section[aria-labelledby="${id}-title"]`),
      `«${id}» dejó su sección pintada como hueco`,
    ).toHaveCount(0);
  }
});

/** Desactiva todas las filas activas de una colección de CMS. */
async function desactivarTodo(api: APIRequestContext, coleccion: string): Promise<void> {
  const respuesta = await api.get(`/api/admin/cms/${coleccion}`);
  expect(respuesta.ok(), `listar ${coleccion}: ${respuesta.status()}`).toBe(true);

  const filas = (await respuesta.json()) as { id: number; isActive: boolean }[];
  const cabeceras = await csrf(api);

  for (const fila of filas.filter((candidata) => candidata.isActive)) {
    const borrado = await api.delete(`/api/admin/cms/${coleccion}/${fila.id}`, { headers: cabeceras });
    expect(
      borrado.ok(),
      `desactivar ${coleccion}/${fila.id}: ${borrado.status()} ${await borrado.text()}`,
    ).toBe(true);
  }
}

/* ========================================================================
 * 7 · El módulo activo que todavía no tiene nada publicado
 * ===================================================================== */

/**
 * **El caso que dejaba la portada muda, anclado por lo que se ve.**
 *
 * `PublicSite` decidía «aquí no hay nada» contando secciones de módulos
 * activos, que es contar **quién podría aportar**, no quién aportó. Con M02
 * activo y sin contenido la cuenta daba uno, así que no salía el aviso — y los
 * cuatro bloques devuelven `null` con la lista vacía. La portada se quedaba
 * con el nombre del negocio y **nada debajo**: ni contenido, ni explicación.
 *
 * `catalogHome` lo tapaba sin querer, porque pinta siempre
 * (`catalog/routes.tsx:51`). Por eso hay que apagar M01 para verlo: **mientras
 * hubo un solo módulo publicable, el agujero no era alcanzable.**
 *
 * Y por eso esta prueba apaga un módulo en vez de afirmar sobre una pantalla
 * quieta. Sale cara —el proceso se reinicia— y es el precio de que el arreglo
 * no se pueda deshacer sin que nadie se entere.
 *
 * ---
 *
 * **Desde M04 hay que apagar dos, y el motivo es exactamente el mismo.**
 * `crmHome` también pinta siempre (`crm/routes.tsx`), así que con M04 activo
 * la portada nunca está vacía y este caso vuelve a ser inalcanzable — igual
 * que lo tapaba `catalogHome` mientras M01 era el único módulo publicable.
 *
 * **No se rebajó la afirmación, se reconstruyó el escenario.** Cambiarla por
 * «se ve la sección de M04» habría dejado el agujero de la portada muda sin
 * ninguna prueba que lo cubra, que es justo lo que este archivo existe para
 * impedir. El día que un tercer módulo pinte siempre, aquí se apaga también.
 */
test('Con M02 activo y sin nada publicado, la portada lo dice en vez de quedarse muda', async ({
  page,
}) => {
  // Cuatro reinicios del proceso, no dos: se apagan M01 y M04 y se devuelven
  // los dos. El presupuesto sube en la misma proporción.
  test.setTimeout(300_000);
  await loginAsE2eAdmin(page);

  // Llega aquí con la portada ya limpia: la prueba anterior retiró todo el
  // contenido de M02. Lo que falta para el caso es que nadie más aporte, y los
  // que aportan sin depender de datos son M01 y M04.
  await cambiarModulo(page, 'catalog', 'Desactivar');
  await cambiarModulo(page, 'crm', 'Desactivar');

  try {
    await page.goto('/');

    // 1 · **El aviso se ve.** Es la afirmación entera: antes de este arreglo,
    //     aquí no había absolutamente nada.
    await expect(
      page.getByText('Todavía no hay contenido publicado.'),
      'la portada se quedó muda: ni contenido ni aviso',
    ).toBeVisible();

    // 2 · **Y no manda activar lo que ya está activo.** El texto viejo decía
    //     «Aparecerá cuando se active el módulo de contenido web», y en este
    //     caso el módulo está activo: era una instrucción imposible de seguir.
    await expect(
      page.getByText(/se active el módulo/),
      'el aviso sigue pidiendo activar un módulo que ya está activo',
    ).toHaveCount(0);

    // 3 · **Y M02 sigue activo mientras se dice todo esto**, que es lo que
    //     separa este caso del de «ningún módulo publicable». Sin esto, la
    //     prueba pasaría igual con M02 apagado y no probaría nada nuevo.
    const capacidades = (await (await page.request.get('/api/capabilities')).json()) as {
      modules: { code: string }[];
    };
    expect(
      capacidades.modules.map((modulo) => modulo.code),
      'M02 no está activo: entonces esto es el caso de siempre, no el nuevo',
    ).toContain('cms');
  } finally {
    // Se deja como se encontró, pase lo que pase arriba: si esta prueba falla
    // y deja un módulo apagado, las de después caen sin motivo propio.
    await cambiarModulo(page, 'crm', 'Activar');
    await cambiarModulo(page, 'catalog', 'Activar');
  }

  await expect(page.locator('#modulo-catalog')).toContainText('Activo');
  await expect(page.locator('#modulo-crm')).toContainText('Activo');
});

/** Mueve el interruptor de un módulo y espera a que el proceso vuelva. */
async function cambiarModulo(
  page: Page,
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
