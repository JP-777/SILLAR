import { test as base, expect, type Page } from '@playwright/test';
import { clasificarArranque, type ObservacionDeArranque } from './diagnosticoArranque.js';

interface Collector {
  errors: string[];
  paused: boolean;
  /** Chromium avisó de que el renderizador de esta página se cayó. */
  crashed: boolean;
}

const collectors = new WeakMap<Page, Collector>();

/**
 * El `test` que importa cada spec, en vez del de `@playwright/test` a secas.
 *
 * Falla si la consola del navegador tuvo **cualquier** error durante la
 * prueba — cero errores es el criterio de aprobado, no una opción por
 * prueba. Por eso vive en el `page` mismo y no en un helper que alguien
 * podría olvidar llamar. La única excepción es {@link duringExpectedOutage},
 * angosta y documentada ahí mismo.
 */
/** Salto de línea. Aparte, para que ningún escape se pierda al editar. */
const SALTO = String.fromCharCode(10);

export const test = base.extend({
  page: async ({ page }, use, testInfo) => {
    const collector: Collector = { errors: [], paused: false, crashed: false };
    collectors.set(page, collector);

    page.on('console', (message) => {
      if (message.type() === 'error' && !collector.paused) {
        collector.errors.push(`[consola] ${message.text()}`);
      }
    });

    page.on('crash', () => {
      collector.crashed = true;
    });

    page.on('pageerror', (error) => {
      if (!collector.paused) {
        collector.errors.push(`[excepción sin capturar] ${error.message}`);
      }
    });

    // --- Navegar es ir **y esperar a que la aplicación haya pintado** ------
    //
    // `page.goto()` vuelve cuando el documento carga, que es **antes de que
    // React monte nada**. Sobre ese hueco, cualquier aserción de ausencia
    // —`toHaveCount(0)`, `not.toContainText`, `toBeHidden`— se cumple sola: el
    // `body` está vacío. Comprobado: `toBeHidden` pasa incluso con un selector
    // que no ha existido nunca.
    //
    // Costó descubrirlo porque **depende de la velocidad**: la misma prueba
    // pasaba vacía con el archivo entero y fallaba en solitario, sin que nadie
    // tocara nada. O sea que un archivo puede afirmar hoy y no afirmar mañana.
    //
    // **Se envuelve `goto` en vez de ofrecer un ayudante.** Un ayudante hay
    // que acordarse de usarlo, y la prueba número 82 la escribe alguien que no
    // ha leído esto. Así no hay forma de llegar al estado malo.
    //
    // El ancla es el enlace de salto (`App.tsx:125`): lo pinta el armazón en
    // **toda** pantalla, panel y tienda, y solo existe cuando la aplicación ha
    // terminado de arrancar. No dice que los datos de la pantalla hayan
    // llegado —eso es de cada prueba— pero sí que hay algo pintado contra lo
    // que afirmar.
    const irDeVerdad = page.goto.bind(page);

    page.goto = async (url, opciones) => {
        const respuesta = await irDeVerdad(url, opciones);

        // La pantalla de instalación se renderiza antes que los proveedores y
        // no lleva enlace de salto: es la única del producto que no lo tiene.
        const pinto = await page
          .locator('a.pf-skip, [data-pantalla="instalacion"]')
          .first()
          .waitFor({ state: 'attached', timeout: 15_000 })
          .then(() => true, () => false);

        if (!pinto) {
          // **Mismo plazo y mismo fallo que antes.** Lo único nuevo es decir
          // qué había en pantalla: «no pintó» confundía un arranque colgado con
          // una página de error, la de migraciones o el asistente, que sí
          // pintan. Ver `diagnosticoArranque.ts`.
          const diagnostico = await diagnosticarArranque(page, collector.crashed);
          await testInfo.attach('diagnostico-arranque.json', {
            body: JSON.stringify(diagnostico, null, 2),
            contentType: 'application/json',
          });
          throw new Error(
            [
              `Se navegó a «${sinConsulta(url)}» y el armazón no apareció en 15 s: ${diagnostico.estado}.`,
              diagnostico.explicacion,
              ...(diagnostico.titulo ? [`Pantalla: «${diagnostico.titulo}».`] : []),
              ...(diagnostico.aviso ? [`Aviso: «${diagnostico.aviso}».`] : []),
              ...(diagnostico.errorAlLeer ? [`Al leer el documento: ${diagnostico.errorAlLeer}`] : []),
              'El ancla es `a.pf-skip`, que el armazón monta en toda pantalla.',
              'Si esta ruta no debe tenerlo, es un caso nuevo y hay que declararlo aquí.',
            ].join(SALTO),
          );
        }

        return respuesta;
      };

    await use(page);

    if (collector.errors.length > 0) {
      await testInfo.attach('errores-de-consola.txt', {
        body: collector.errors.join('\n\n'),
        contentType: 'text/plain',
      });
    }

    expect(
      collector.errors,
      `La consola del navegador tuvo ${collector.errors.length} error(es) durante la prueba`,
    ).toEqual([]);
  },
});

/**
 * Lee lo mínimo del documento para clasificar un arranque que no llegó al
 * armazón. Solo después de agotar el plazo, así que no cambia cuánto se espera
 * a que la aplicación pinte.
 *
 * La lectura lleva su propio límite porque un renderizador colgado no responde
 * a `evaluate`: sin él, el diagnóstico se colgaría con la página.
 */
/** La URL sin su consulta: puede llevar un token de un solo uso. */
function sinConsulta(url: string): string {
  const corte = url.search(/[?#]/);
  return corte < 0 ? url : `${url.slice(0, corte)}?…`;
}

async function diagnosticarArranque(page: Page, caido: boolean) {
  if (caido) {
    return clasificarArranque(null, 'Chromium emitió el evento «crash» para esta página.');
  }

  const LIMITE_DE_LECTURA = 5_000;
  try {
    const observacion = await Promise.race([
      page.evaluate((): ObservacionDeArranque => {
        const tarjeta = document.querySelector('.pf-centered .ui-card__title');
        const aviso = document.querySelector('.pf-centered [role="alert"]');
        return {
          ruta: location.pathname,
          indicadorDeCarga: document.querySelector('.pf-boot') !== null,
          tituloDeTarjeta: tarjeta?.textContent?.trim() || null,
          aviso: aviso?.textContent ?? null,
          formularioDeInstalacion: document.querySelector('.pf-centered form') !== null,
          raizVacia: (document.getElementById('root')?.childElementCount ?? 0) === 0,
        };
      }),
      new Promise<never>((_, rechazar) =>
        setTimeout(() => rechazar(new Error(`sin respuesta en ${LIMITE_DE_LECTURA} ms`)), LIMITE_DE_LECTURA),
      ),
    ]);
    return clasificarArranque(observacion);
  } catch (error) {
    return clasificarArranque(null, error instanceof Error ? error.message : String(error));
  }
}

/**
 * Silencia el contador de errores de consola mientras dure `fn`. La única
 * excepción al cero-errores-sin-excepciones de este arnés, y a propósito muy
 * angosta: existe por una razón física, no de comodidad.
 *
 * Mientras el contenedor real se reinicia, el sondeo de reconexión
 * (`shared/http/connection.ts`, `probe()`) falla una y otra vez a propósito —
 * es la señal que hace funcionar la pantalla de reconexión— y cada fallo de
 * red real genera su propio "Failed to load resource" en la consola del
 * navegador. No hay forma de observar un reinicio real sin que ocurra. Fuera
 * de esta ventana el cero sigue siendo el cero: cualquier error antes de
 * entrar o después de salir de `fn` sigue haciendo fallar la prueba.
 */
export async function duringExpectedOutage<T>(page: Page, fn: () => Promise<T>): Promise<T> {
  const collector = collectors.get(page);
  if (!collector) {
    throw new Error('duringExpectedOutage necesita el `page` de este mismo fixture.');
  }

  collector.paused = true;
  try {
    return await fn();
  } finally {
    collector.paused = false;
  }
}

export { expect };
