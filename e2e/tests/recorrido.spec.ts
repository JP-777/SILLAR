import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * **El recorrido de la demostración, entero y seguido.**
 *
 * No es una prueba de una pantalla: es el camino que alguien va a hacer
 * delante de gente, en el orden en que lo va a hacer. Cada pantalla ya está
 * probada por su cuenta; lo que esto añade es **la costura entre ellas** — lo
 * que se crea en un paso tiene que existir en el siguiente, y aparecer en la
 * tienda sin que nadie recargue nada a mano.
 *
 * Los pasos 7 y 8 del recorrido —desactivar M01, comprobar que el panel sigue
 * en pie, reactivarlo— **no se repiten aquí**: los cubre
 * `catalogo.spec.ts:232` de punta a punta, y reiniciar el host dos veces más
 * por prueba cuesta minuto y medio sin afirmar nada nuevo.
 *
 * PNG de 1×1 de verdad: el servidor mira el contenido, no la extensión
 * (ADR-011), así que un archivo inventado no pasaría.
 */

const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

test('El recorrido de la demostración, de entrar al panel a verlo en la tienda', async ({
  page,
}) => {
  const record = themeRecorder(page, 'recorrido');
  const sello = Date.now();

  // Los nombres llevan el sello para que el recorrido se pueda repetir sin
  // chocar con lo que dejó la vuelta anterior. En la demostración de verdad
  // se escriben a mano y sin sello, claro.
  const marca = `Prisma ${sello}`;
  const madre = `Escritorio ${sello}`;
  const hija = `Cuadernos ${sello}`;
  const simple = `Cuaderno anillado Prisma A4 ${sello}`;
  const conVarias = `Plumón de pizarra Prisma ${sello}`;

  // --- 1 · Entrar al panel -------------------------------------------------
  await loginAsE2eAdmin(page);
  await page.goto('/admin');
  await expect(page.getByRole('navigation')).toContainText('Marcas');

  // --- 2 · Crear una marca con su imagen -----------------------------------
  // La imagen se sube en Archivos y se elige en la ficha: subir es de la
  // galería, y dos sitios donde subir lo mismo acaban en dos criterios sobre
  // qué formatos valen.
  await page.goto('/admin/archivos');
  await page.setInputFiles('.gal-drop input[type="file"]', {
    name: `logo-${sello}.png`,
    mimeType: 'image/png',
    buffer: PNG,
  });
  // La ficha de la galería, no el aviso de que se subió: el aviso se va solo
  // y la ficha es lo que hay que poder elegir después.
  await expect(page.locator('.gal__name', { hasText: `logo-${sello}.png` })).toBeVisible();

  await page.goto('/admin/catalogo/marcas');
  await page.getByRole('button', { name: /^(Nueva marca|Crear la primera marca)$/ }).first().click();

  const fichaMarca = page.getByRole('dialog');
  await fichaMarca.getByLabel(/^Nombre/).fill(marca);
  await fichaMarca.getByRole('button', { name: `logo-${sello}.png` }).click();
  await fichaMarca.getByRole('button', { name: 'Crear marca' }).click();
  await expect(fichaMarca).toBeHidden();

  // Marcas no tiene buscador —04A decidió que con pocas marcas sobra— así que
  // la fila se busca en la tabla.
  await expect(page.locator('tbody tr').filter({ hasText: marca })).toBeVisible();

  await record('recorrido-marca-creada');

  // --- 3 · Dos categorías, una dentro de otra ------------------------------
  await page.goto('/admin/catalogo/categorias');

  for (const [nombre, padre] of [
    [madre, null],
    [hija, madre],
  ] as const) {
    await page
      .getByRole('button', { name: /^(Nueva categoría|Crear la primera categoría)$/ })
      .first()
      .click();

    const ficha = page.getByRole('dialog');
    await ficha.getByLabel(/^Nombre/).fill(nombre);

    if (padre) {
      await ficha.getByLabel(/^Cuelga de/).selectOption({ label: padre });
    }

    await ficha.getByRole('button', { name: 'Crear categoría' }).click();
    await expect(ficha).toBeHidden();
  }

  // **El árbol se ve**: la hija aparece debajo de la madre y sangrada, que es
  // lo único que distingue una jerarquía de una lista.
  const filaHija = page.locator('tbody tr').filter({ hasText: hija });
  await expect(filaHija).toBeVisible();
  await expect(filaHija.locator('.cat-tree')).not.toHaveCSS('padding-left', '0px');

  await record('recorrido-arbol-de-categorias');

  // --- 4 · Un producto con todo --------------------------------------------
  await page.goto('/admin/catalogo/productos');
  await page
    .getByRole('button', { name: /^(Nuevo producto|Crear el primer producto)$/ })
    .first()
    .click();

  const alta = page.getByRole('dialog');
  await alta.getByLabel(/^Nombre/).fill(simple);
  await alta.getByLabel('Descripción corta').fill('Tapa dura, 100 hojas.');
  await alta.getByLabel('Precio').fill('12.5');
  await alta.getByLabel('Marca').selectOption({ label: marca });
  await alta.getByRole('checkbox', { name: hija }).check();
  await alta.getByRole('button', { name: 'Crear producto' }).click();
  await expect(alta).toBeHidden();

  // La imagen se asocia desde la ficha, que es donde vive la galería del
  // producto.
  await page.getByLabel('Buscar').fill(simple);
  await page.locator('tbody tr').filter({ hasText: simple }).getByRole('button', { name: 'Editar' }).click();

  const ficha = page.getByRole('dialog');
  await ficha.getByRole('button', { name: `logo-${sello}.png` }).click();
  // Sobre el elemento y no sobre el rol: una imagen decorativa lleva `alt=""`
  // y entonces no expone `role="img"`. Lo que se afirma es que la galería del
  // producto tiene una imagen dentro.
  await expect(ficha.locator('img').first()).toBeVisible();

  // **Y se espera a que la recarga termine antes de guardar.** Asociar una
  // imagen recarga la ficha con el cajón abierto (`ProductsPage.tsx:197`), y
  // una prueba pulsa «Guardar» decenas de milisegundos después de ver la
  // miniatura — algo que una persona no hace. En una vuelta de la suite
  // entera el cajón se quedó abierto aquí, sin ningún aviso, y no se ha
  // vuelto a reproducir en seis intentos: la espera quita la carrera de la
  // prueba, **no demuestra que no la haya en el producto**. Anotado como
  // riesgo abierto en la bitácora.
  await page.waitForLoadState('networkidle');
  await ficha.getByRole('button', { name: 'Guardar cambios' }).click();

  // **Se espera al desenlace, no a la ausencia de uno.**
  //
  // Aquí había `expect(getByRole('alert')).toHaveCount(0)` seguido de
  // `expect(ficha).toBeHidden()`, y era el fallo que este arnés lleva todo el
  // día persiguiendo, en mi propia línea de diagnóstico: **la aserción de que
  // no hay error corría antes de que el error pudiera renderizarse**, así que
  // pasaba siempre. Cuando el cajón se quedaba abierto —dos veces— la prueba
  // moría en la línea siguiente sin decir por qué, que es justo lo que esa
  // línea existía para evitar.
  //
  // Ahora se pregunta cómo acabó el guardado y se exige que acabara bien. Si
  // acabó mal, **el mensaje trae el motivo**.
  await expect
    .poll(
      async () => {
        const guardado = page
          .locator('.ui-toast')
          .filter({ hasText: 'Se guardaron los cambios' });

        if ((await guardado.count()) > 0) {
          return 'guardado';
        }

        const alerta = page.getByRole('alert');

        if ((await alerta.count()) > 0) {
          return `el guardado falló: ${(await alerta.first().innerText()).trim()}`;
        }

        return 'sin desenlace todavía';
      },
      { message: 'tras pulsar «Guardar cambios», el producto no llegó a guardarse' },
    )
    .toBe('guardado');

  await expect(ficha).toBeHidden();

  await record('recorrido-producto-completo');

  // --- 5 · Un producto con dos presentaciones de precio distinto -----------
  await page.getByRole('button', { name: 'Nuevo producto', exact: true }).click();

  const alta2 = page.getByRole('dialog');
  await alta2.getByLabel(/^Nombre/).fill(conVarias);
  await alta2.getByLabel('Precio').fill('4.50');
  await alta2.getByRole('checkbox', { name: hija }).check();
  await alta2.getByRole('button', { name: 'Crear producto' }).click();
  await expect(alta2).toBeHidden();

  await page.getByLabel('Buscar').fill(conVarias);
  await page
    .locator('tbody tr')
    .filter({ hasText: conVarias })
    .getByRole('button', { name: 'Editar' })
    .click();

  const conPresentaciones = page.getByRole('dialog');
  await conPresentaciones
    .getByRole('button', { name: 'Este producto viene en varias presentaciones' })
    .click();

  await conPresentaciones.getByLabel(/^Valor de la presentación 1/).fill('Negro');
  await conPresentaciones.getByLabel(/^Valor de la presentación 2/).fill('Azul metálico');

  // La segunda cuesta más: es lo que hace que la tarjeta tenga que decir
  // «Desde», y sin eso el paso 6 no probaría nada.
  await conPresentaciones.locator('.cat-variants__inherit').nth(1).click();
  await conPresentaciones.getByLabel(/^Precio de la presentación 2/).fill('5.90');

  await conPresentaciones.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(conPresentaciones).toBeHidden();

  await record('recorrido-dos-presentaciones');

  // **Antes de ir a la tienda, se comprueba qué quedó guardado.** Sin esto,
  // un fallo más adelante dice «la tarjeta no aparece» y no distingue entre
  // «no se guardó», «se despublicó» y «la tienda no lo lista».
  const guardado = (await (
    await page.request.get(
      `/api/admin/catalog/products?q=${encodeURIComponent(conVarias)}&pageSize=50`,
    )
  ).json()) as { items: { id: string; name: string; isPublic: boolean; isActive: boolean }[] };

  const enElPanel = guardado.items.find((p) => p.name === conVarias);
  expect(enElPanel, `«${conVarias}» no está en el panel después de guardarlo`).toBeTruthy();
  expect(enElPanel!.isPublic, `«${conVarias}» se guardó despublicado`).toBe(true);
  expect(enElPanel!.isActive, `«${conVarias}» se guardó dado de baja`).toBe(true);

  const ficha2 = (await (
    await page.request.get(`/api/admin/catalog/products/${enElPanel!.id}`)
  ).json()) as { items: { variantValue: string | null; priceOverride: number | null }[] };

  expect(
    ficha2.items.filter((i) => i.variantValue !== null),
    'las dos presentaciones no se guardaron',
  ).toHaveLength(2);

  // --- 6 · La tienda pública -----------------------------------------------
  // Los dos nacen publicados, así que no hay ningún paso intermedio: se va a
  // la tienda y están.

  // 6.1 · Aparecen en el catálogo, y el de dos presentaciones dice «Desde».
  //
  // **Se espera a que el filtro se haya aplicado**, no solo a que aparezca
  // una tarjeta: la búsqueda es en vivo y hasta que llega su respuesta la
  // rejilla sigue enseñando el catálogo entero. Con pocos productos eso no se
  // nota; con la suite llena, la primera tarjeta buscada puede estar en ese
  // catálogo sin filtrar y la segunda no, y entonces la prueba mira el render
  // de antes.
  // Se entra ya filtrado, en vez de teclear: lo que este paso afirma es que
  // **lo creado está en la tienda**, y teclear mete en medio el retardo de la
  // búsqueda en vivo. Buscar tecleando se prueba en el paso 6.5 y en
  // `tienda.spec.ts`, que es su sitio.
  await page.goto(`/catalogo?q=${sello}`);
  await expect(page.locator('.ti-card')).toHaveCount(2);
  await expect(page.locator('.ti-card').filter({ hasText: simple })).toBeVisible();

  const tarjetaVarias = page.locator('.ti-card').filter({ hasText: conVarias });
  await expect(tarjetaVarias).toContainText(/Desde\s+S\/\s*4[.,]50/);

  await record('recorrido-tienda-con-lo-creado');

  // 6.2 · La categoría hija lista sus dos productos.
  await page.goto('/catalogo');
  await page.getByRole('button', { name: hija }).click();
  await expect(page.locator('.ti-card')).toHaveCount(2);

  // 6.3 · La ficha, con su miga de pan de dos niveles.
  await page.locator('.ti-card').filter({ hasText: conVarias }).getByRole('link').first().click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(conVarias);
  await expect(page.getByRole('navigation', { name: 'Dónde estás' })).toContainText(madre);
  await expect(page.getByRole('navigation', { name: 'Dónde estás' })).toContainText(hija);

  // 6.4 · **Cambiar de presentación, y que el precio grande la siga.**
  // Es el paso que en la primera pasada del recorrido no hacía nada: la
  // lista era de solo lectura y el importe de arriba se quedaba en el de la
  // primera, con tres números distintos debajo.
  await expect(page.locator('.ti-price--detail')).toContainText(/S\/\s*4[.,]50/);

  await page.getByRole('radio', { name: /Azul metálico/ }).check();
  await expect(page.locator('.ti-price--detail')).toContainText(/S\/\s*5[.,]90/);

  // Y se puede hacer con el teclado, que es lo que dan los radios de verdad.
  await page.getByRole('radio', { name: /Azul metálico/ }).press('ArrowUp');
  await expect(page.locator('.ti-price--detail')).toContainText(/S\/\s*4[.,]50/);

  await record('recorrido-ficha-con-presentacion-elegida');

  // 6.5 · Y se encuentra buscándolo por su nombre.
  await page.goto('/catalogo');
  await page.getByLabel(/Buscar/).fill('Cuaderno anillado');
  await expect(page.locator('.ti-card').filter({ hasText: simple })).toBeVisible();
});
