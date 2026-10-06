import type { Page } from '@playwright/test';
import { duringExpectedOutage, expect } from './base.js';
import { psql } from '../setup/docker.js';

/**
 * **El grafo de módulos, para las pruebas que necesitan apagar uno.**
 *
 * ---
 *
 * ## Por qué existe, y qué rojo vino a resolver
 *
 * M07 declara `HardDependencies => ["core", "catalog", "crm"]`
 * (`B2BModule.cs:57`) y el arnés lo activa en el escenario normal
 * (`global-setup.ts`). Desde ese momento la plataforma **impide correctamente**
 * desactivar M01 o M04: su interruptor llega `disabled`, y eso es el producto
 * aplicando el grafo, no un fallo.
 *
 * Siete pruebas anteriores a M07 apagaban `catalog` o `crm` sin contemplar al
 * nuevo dependiente, así que la puerta canónica sobre `60da5dc` salió roja en
 * esas siete. **El arreglo es adaptar las pruebas al grafo, no aflojar el
 * grafo:** no se quita B2B del arnés, no se afloja ninguna dependencia dura y
 * no se fuerza ningún interruptor que el producto deba bloquear.
 *
 * Este ayudante es ese «suspende B2B un momento» escrito una vez, en lugar de
 * siete copias del mismo bloque.
 *
 * ## Lo que este ayudante no sabe
 *
 * Conoce el panel de módulos y `/api/capabilities`, y nada más. **No conoce
 * producto ajeno y no altera ninguna regla de negocio:** usa el mismo
 * mecanismo soportado que usaría una persona —el interruptor— y comprueba el
 * resultado contra las capacidades que publica el propio sistema.
 */

/** El código de M07, escrito una vez. */
const B2B = 'b2b';

/** Los códigos de los módulos activos, según el propio sistema. */
export async function capacidadesActivas(page: Page): Promise<string[]> {
  const respuesta = await page.request.get('/api/capabilities');

  expect(
    respuesta.ok(),
    `/api/capabilities respondió ${respuesta.status()}: ${await respuesta.text()}`,
  ).toBe(true);

  const cuerpo = (await respuesta.json()) as { modules: { code: string }[] };

  return cuerpo.modules.map((m) => m.code);
}

/**
 * Mueve el interruptor de un módulo por la pantalla y espera a que el proceso
 * vuelva.
 *
 * Es la misma mecánica que cada spec tenía copiada: activar o desactivar
 * detiene el host a propósito (`Modules:RestartAfterActivation=true` en
 * `.env.e2e`) y Docker lo relanza, así que se espera a que el diálogo de
 * «Aplicando el cambio» desaparezca.
 */
export async function cambiarModulo(
  page: Page,
  codigo: string,
  accion: 'Activar' | 'Desactivar',
): Promise<void> {
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

  // **Y se espera a que la API vuelva a servir de verdad antes de devolver el
  // control.** Que el diálogo se cierre dice que el panel dejó de esperar, no
  // que el proceso esté sirviendo: con dos cambios de módulo seguidos —los que
  // hace falta encadenar desde que M07 depende duro de M01 y M04— la navegación
  // siguiente llegaba a un proxy que todavía reconectaba y la SPA no montaba
  // nada. El síntoma era «React no montó nada», que no dice de qué es.
  await expect
    .poll(async () => (await page.request.get('/api/capabilities')).status(), {
      message: 'la API no volvió a servir tras cambiar un módulo',
      timeout: 90_000,
    })
    .toBe(200);

  // **Y al servidor web también, que es la otra mitad.** Las pruebas navegan
  // contra Vite, no contra la API: su proxy reenvía `/api` y además sirve el
  // paquete de la SPA. Cuando el contenedor se reinicia, ese proxy puede
  // devolver 500 con cuerpo vacío —«socket hang up»— y entonces la aplicación
  // no monta nada, que es el `nada-montado` de la guarda de `base.ts`. Esperar
  // solo a la API dejaba fuera justo ese hueco.
  await expect
    .poll(async () => (await page.request.get('/')).status(), {
      message: 'el servidor web no volvió a servir tras cambiar un módulo',
      timeout: 90_000,
    })
    .toBe(200);
}

/**
 * Ejecuta `cuerpo` con M07 desactivado, y lo deja como estaba.
 *
 * **El orden de la restauración es el punto.** B2B depende duro de `catalog` y
 * de `crm`, así que no se puede reactivar hasta que esas dependencias estén
 * activas otra vez: **dependencias primero, B2B después**. Por eso el cuerpo
 * restaura lo suyo y este ayudante reactiva B2B al final, en un `finally`, para
 * que una prueba que falle a mitad no deje el grafo contaminado para las
 * siguientes.
 *
 * **Y comprueba el estado inicial antes de tocar nada.** Si B2B no estuviera
 * activo, suspenderlo sería un no-op y la prueba seguiría como si lo hubiera
 * hecho: eso convertiría este ayudante en el sitio donde un fallo se esconde.
 */
export async function sinB2B<T>(page: Page, cuerpo: () => Promise<T>): Promise<T> {
  expect(
    await capacidadesActivas(page),
    'el escenario e2e debería tener M07 activo: lo activa global-setup.ts y es la C9',
  ).toContain(B2B);

  await cambiarModulo(page, B2B, 'Desactivar');

  // **Se comprueba contra `/api/capabilities`, no contra la tarjeta del panel.**
  // La primera versión de este ayudante miraba `#modulo-b2b` justo después del
  // reinicio y fallaba con «element(s) not found»: la pantalla acababa de
  // recargarse y la lista todavía no estaba pintada. El estado del grafo lo dice
  // el servidor, no el DOM, y preguntárselo a él no depende de a qué velocidad
  // repinte la SPA.
  expect(
    await capacidadesActivas(page),
    'M07 sigue activo tras desactivarlo: el escenario no es el que la prueba necesita',
  ).not.toContain(B2B);

  try {
    return await cuerpo();
  } finally {
    // Las dependencias duras ya las restauró el cuerpo; si no lo hizo, este
    // `Activar` falla ruidosamente y eso es mejor que seguir en silencio.
    await cambiarModulo(page, B2B, 'Activar');
    expect(
      await capacidadesActivas(page),
      'M07 no volvió a estar activo: el grafo queda contaminado para las pruebas siguientes',
    ).toContain(B2B);
  }
}

/**
 * Las claves foráneas que salen del schema de M07 hacia otro schema.
 *
 * **Una sola medición, usada por los dos sitios que la necesitan.** La escribió
 * `zz-b2b-instalacion.spec.ts` para su ciclo físico; `zz-z-m04-ciclo.spec.ts`
 * necesita exactamente la misma comprobación cuando reconstruye M04, y dos
 * consultas parecidas que divergieran un día serían peor que ninguna. El SQL es
 * el mismo, byte a byte, que tenía aquella spec.
 *
 * Se ordena en JavaScript y no en SQL: el clúster usa colación ICU `es-PE`, y
 * depender de dónde coloca ICU un nombre daría un rojo cierto y mudo.
 */
export async function fkCruzadasDeB2b(): Promise<string[]> {
  const salida = await psql(`
    SELECT coalesce(string_agg(rn.nspname || '.' || r.relname, ','), '')
      FROM pg_constraint c
      JOIN pg_class     d  ON d.oid  = c.conrelid
      JOIN pg_namespace dn ON dn.oid = d.relnamespace
      JOIN pg_class     r  ON r.oid  = c.confrelid
      JOIN pg_namespace rn ON rn.oid = r.relnamespace
     WHERE c.contype = 'f' AND dn.nspname = 'b2b' AND rn.nspname <> 'b2b'
  `);

  return salida === '' ? [] : salida.split(',').sort();
}

/**
 * Las cinco de M07, en el orden en que las devuelve {@link fkCruzadasDeB2b}.
 *
 * Tres hacia `crm.customers` —una por cada tabla que guarda un cliente— y dos
 * hacia `catalog`: `products` desde las personalizaciones y `product_items`
 * desde las líneas de cotización.
 */
export const FK_CRUZADAS_DE_B2B = [
  'catalog.product_items',
  'catalog.products',
  'crm.customers',
  'crm.customers',
  'crm.customers',
] as const;
