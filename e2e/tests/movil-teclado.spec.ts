import type { Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * **El repaso sistemático de móvil y teclado.**
 *
 * Existe por un caso concreto: `layout.css` declaraba esconder la barra
 * lateral por debajo de 860 px dentro de una media query escrita **antes** de
 * su regla base, así que a igual especificidad ganaba la última y **no
 * escondía nada**. El panel llevaba roto en móvil desde que existe, y lo cazó
 * de rebote una comprobación de otra cosa.
 *
 * De ahí la forma de este archivo: **una pasada que recorre todas las
 * pantallas**, y no una comprobación dentro de la prueba de cada una. Lo que
 * falló no era de ninguna pantalla — era del armazón que las contiene, y por
 * eso nadie lo miró.
 *
 * Las dos afirmaciones son las que se pueden hacer sobre cualquier pantalla
 * sin saber qué enseña:
 *
 * 1. **Nada se sale por la derecha** a 390 px, y el mensaje **nombra al
 *    culpable**: «se sale algo» obliga a abrir el navegador para saber qué.
 * 2. **Se puede llegar al contenido con el teclado**, y el foco se ve.
 */

const PANEL = [
  { path: '/admin', name: 'Inicio' },
  { path: '/admin/modulos', name: 'Módulos' },
  { path: '/admin/configuracion', name: 'Configuración' },
  { path: '/admin/archivos', name: 'Archivos' },
  { path: '/admin/usuarios', name: 'Usuarios' },
  { path: '/admin/auditoria', name: 'Auditoría' },
  { path: '/admin/mi-contrasena', name: 'Mi contraseña' },
  { path: '/admin/catalogo/marcas', name: 'Marcas' },
  { path: '/admin/catalogo/categorias', name: 'Categorías' },
  { path: '/admin/catalogo/productos', name: 'Productos' },
] as const;

const TIENDA = [
  { path: '/catalogo', name: 'Catálogo' },
  { path: '/', name: 'Inicio público' },
] as const;

const MOVIL = { width: 390, height: 844 };

/** Abre una pantalla y espera a que haya terminado de cargar sus datos. */
async function abrir(page: Page, path: string) {
  await page.goto(path);
  await expect(page.locator('main')).toBeVisible();
  await page.waitForLoadState('networkidle');
  await expect(page.locator('main .ui-spinner')).toHaveCount(0);
}

/**
 * Lo que se sale del ancho de la ventana, nombrado.
 *
 * Se descartan los elementos ocultos y los que están fuera a propósito —el
 * enlace de salto vive desplazado hasta que se enfoca— comprobando que
 * ocupan sitio: un elemento de ancho cero no se «sale», está guardado.
 */
async function loQueSeSale(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    // **Primero: ¿desborda la página?** Un elemento más ancho que la ventana
    // no es un defecto si vive dentro de una caja que se desplaza a lo ancho
    // —una tabla dentro de `.ui-table-scroll` es exactamente eso—. Si el
    // documento no se sale, no hay nada que arreglar y listar elementos daría
    // una lista de falsos positivos con muy buena pinta.
    if (document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1) {
      return [];
    }

    const ancho = document.documentElement.clientWidth;

    /** Si algún ancestro se desplaza a lo ancho, el elemento está contenido. */
    const contenido = (el: HTMLElement) => {
      for (let p = el.parentElement; p; p = p.parentElement) {
        const desborde = getComputedStyle(p).overflowX;
        if (desborde === 'auto' || desborde === 'scroll' || desborde === 'hidden') {
          return true;
        }
      }
      return false;
    };

    return Array.from(document.querySelectorAll<HTMLElement>('body *'))
      .filter((el) => {
        const caja = el.getBoundingClientRect();
        return caja.width > 0 && caja.height > 0 && caja.right > ancho + 1 && !contenido(el);
      })
      .slice(0, 8)
      .map((el) => {
        const clase = el.className && typeof el.className === 'string' ? `.${el.className}` : '';
        return `${el.tagName.toLowerCase()}${clase}`;
      });
  });
}

test('A 390 px, ninguna pantalla del panel se sale por la derecha', async ({ page }) => {
  await page.setViewportSize(MOVIL);
  await loginAsE2eAdmin(page);

  // **Se recorren todas antes de fallar.** Una pasada existe para decir
  // cuántas están rotas, no cuál es la primera: parar en la primera obliga a
  // arreglar y volver a correr para descubrir la siguiente.
  const rotas: string[] = [];

  for (const pantalla of PANEL) {
    await abrir(page, pantalla.path);

    const culpables = await loQueSeSale(page);
    if (culpables.length > 0) {
      rotas.push(`${pantalla.name}: ${culpables.join(', ')}`);
    }
  }

  expect(rotas, `se salen a 390 px:\n  ${rotas.join('\n  ')}`).toHaveLength(0);
});

test('A 390 px, ninguna pantalla pública se sale por la derecha', async ({ page }) => {
  await page.setViewportSize(MOVIL);

  const rotas: string[] = [];

  // **Sin sesión, que es como llega quien visita la tienda.** Y sin válvula:
  // visitar la tienda sin sesión dejaba cuatro errores de consola —«quién
  // soy» y el token CSRF respondían 401— y eso obligaba a apartarlos aquí, en
  // `aa-vacios` y en la ficha de abajo. **Tres sitios donde una regresión de
  // esa área no se habría visto.** Arreglado en el origen.
  for (const pantalla of TIENDA) {
    await abrir(page, pantalla.path);

    const culpables = await loQueSeSale(page);
    if (culpables.length > 0) {
      rotas.push(`${pantalla.name}: ${culpables.join(', ')}`);
    }
  }

  expect(rotas, `se salen a 390 px:\n  ${rotas.join('\n  ')}`).toHaveLength(0);

  // La ficha de un producto: es la pantalla pública con más cosas dentro
  // —galería, precio, presentaciones— y la que más fácil desborda.
  const listado = (await (await page.request.get('/api/catalog/products?pageSize=1')).json()) as {
    items: { slug: string }[];
  };

  if (listado.items.length > 0) {
    await abrir(page, `/producto/${listado.items[0].slug}`);
    const culpables = await loQueSeSale(page);

    expect(culpables, `la ficha se sale a 390 px: ${culpables.join(', ')}`).toHaveLength(0);
  }
});

test('A 390 px, el menú del panel sigue estando y lleva a todas partes', async ({ page }) => {
  await page.setViewportSize(MOVIL);
  await loginAsE2eAdmin(page);
  await abrir(page, '/admin');

  // **Esconder la barra lateral no era la respuesta**: no hay menú en la
  // barra superior, así que sin ella el panel se queda sin ninguna forma de
  // navegar. Lo que se afirma no es que la barra esté, es que **se pueda
  // llegar a las pantallas**.
  const menu = page.getByRole('navigation', { name: 'Secciones del panel' });
  await expect(menu).toBeVisible();

  for (const nombre of ['Módulos', 'Usuarios', 'Archivos', 'Marcas', 'Productos']) {
    await expect(
      menu.getByRole('link', { name: nombre }),
      `a 390 px no se llega a «${nombre}»`,
    ).toBeVisible();
  }
});

test('Con el teclado se llega al contenido de cada pantalla, y el foco se ve', async ({ page }) => {
  await loginAsE2eAdmin(page);

  for (const pantalla of PANEL) {
    await abrir(page, pantalla.path);

    // Desde el principio del documento: es donde empieza quien llega con
    // teclado, y donde vive el enlace de salto.
    await page.evaluate(() => document.body.focus());
    await page.keyboard.press('Tab');

    const primero = await page.evaluate(() => {
      const activo = document.activeElement;
      if (!activo || activo === document.body) {
        return null;
      }

      const estilo = getComputedStyle(activo);
      return {
        texto: (activo.textContent ?? '').trim().slice(0, 40),
        // El foco tiene que verse. `base.css:64` lo pinta con `outline`, así
        // que se comprueba el valor calculado y no que la regla exista.
        conAnillo: estilo.outlineStyle !== 'none' && parseFloat(estilo.outlineWidth) > 0,
      };
    });

    expect(primero, `«${pantalla.name}»: el primer Tab no enfoca nada`).not.toBeNull();
    expect(
      primero!.conAnillo,
      `«${pantalla.name}»: el primer elemento enfocado no enseña el foco («${primero!.texto}»)`,
    ).toBe(true);

    // Y desde ahí se llega al contenido sin recorrer el mundo: el enlace de
    // salto es el primero justamente para eso.
    await page.keyboard.press('Enter');

    await expect
      .poll(
        () =>
          page.evaluate(() => {
            const main = document.querySelector('main');
            return main === document.activeElement || main?.contains(document.activeElement) === true;
          }),
        { message: `«${pantalla.name}»: el enlace de salto no lleva al contenido` },
      )
      .toBe(true);
  }
});
