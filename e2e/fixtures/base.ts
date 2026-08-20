import { test as base, expect, type Page } from '@playwright/test';

interface Collector {
  errors: string[];
  paused: boolean;
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
export const test = base.extend({
  page: async ({ page }, use, testInfo) => {
    const collector: Collector = { errors: [], paused: false };
    collectors.set(page, collector);

    page.on('console', (message) => {
      if (message.type() === 'error' && !collector.paused) {
        collector.errors.push(`[consola] ${message.text()}`);
      }
    });

    page.on('pageerror', (error) => {
      if (!collector.paused) {
        collector.errors.push(`[excepción sin capturar] ${error.message}`);
      }
    });

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
