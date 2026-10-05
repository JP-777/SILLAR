import type { Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { psql, psqlArchivo } from '../setup/docker.js';
import { migrate, seed } from '../setup/migrate.js';

/**
 * **Instalar, desinstalar y reinstalar M07 — los dos niveles a la vez.**
 *
 * Cubre 5.1–5.7 de `docs/modules/b2b/PLAN-DE-PRUEBAS-M07.md` y, con ellos, el
 * criterio de terminado de `CLAUDE.md`: «se puede instalar y desinstalar sin
 * romper nada del resto del sistema».
 *
 * **Une los dos niveles a propósito.** `zz-b2b-ciclo.spec.ts` prueba el nivel de
 * producto —activar y desactivar—; este prueba el nivel de esquema —migrar y
 * soltar—, y los encadena: se desactiva por el mecanismo soportado **antes** de
 * tocar el esquema, y se reactiva **después** de restaurarlo. Una prueba que
 * acreditara el esquema dejando el producto en un estado imposible no
 * acreditaría nada.
 *
 * ---
 *
 * ## Por qué el prefijo `zz-`, y qué se rompe si alguien lo cambia
 *
 * **El prefijo es deliberado, no decorativo.**
 *
 * - **Depende de `workers: 1` y `fullyParallel: false`**
 *   (`e2e/playwright.config.ts:15-17`). Aquí se **sueltan schemas enteros** de
 *   la base compartida: con dos trabajadores, otra spec se encontraría sin
 *   `b2b` o sin `catalog` a media prueba.
 * - **Tiene que ejecutarse después de las specs funcionales.** Playwright
 *   ordena los archivos alfabéticamente, y `zz-` es la convención del arnés
 *   para «corre al final, cuando nadie más va a mirar»
 *   (`zz-instalacion.spec.ts:16-19`).
 * - **Renombrarla o reordenarla contamina las pruebas posteriores.** Durante
 *   esta spec hay ventanas en las que el schema `b2b` no existe y una en la que
 *   tampoco existe `catalog`: cualquier spec que corriera dentro de esas
 *   ventanas fallaría por algo que no es suyo.
 * - **Reconstruir el estado al final es parte del criterio, no limpieza.** El
 *   criterio dice «se crea **y** se elimina»; volver a crearlo sobre una base
 *   que ya tuvo el módulo es la otra mitad de la afirmación
 *   (`zz-instalacion.spec.ts:146-148`).
 *
 * ---
 *
 * ## Las guardas de C6, que nadie había ejecutado
 *
 * `database/modules/catalog/99_drop.sql` y `database/modules/crm/99_drop.sql`
 * llevan desde `51d0275` una guarda que **se niega** a soltar el schema
 * mientras otro módulo instalado dependa de él de forma dura. M07 es ese
 * módulo, y hasta hoy esa guarda no había dicho no a nadie en el arnés: es el
 * §2 de `ANTES-DE-EMPEZAR-UN-MODULO.md` —una barrera que nunca se ha visto
 * disparar— y aquí se provoca en las dos direcciones.
 */

/** Las cinco tablas de M07, más su historial, que vive dentro del schema. */
const TABLAS_B2B = [
  '__migrations',
  'institution_requests',
  'quote_lines',
  'quote_number_series',
  'quotes',
  'special_order_leads',
];

/** Recuento de los módulos ajenos, para comparar fila por fila. */
async function ajenos(): Promise<string> {
  return psql(`
    SELECT (SELECT count(*) FROM core.admin_users)
        || '/' || (SELECT count(*) FROM core.site_settings)
        || '/' || (SELECT count(*) FROM core.media_assets)
        || '/' || (SELECT count(*) FROM catalog.products)
        || '/' || (SELECT count(*) FROM catalog.categories)
        || '/' || (SELECT count(*) FROM crm.customers)
        || '/' || (SELECT count(*) FROM cms.banners)
  `);
}

async function existeSchema(nombre: string): Promise<string> {
  return psql(
    `SELECT count(*) FROM information_schema.schemata WHERE schema_name = '${nombre}'`,
  );
}

/**
 * Las tablas que hay ahora mismo en el schema de M07.
 *
 * **Se ordenan en JavaScript, no en SQL**, y el motivo no es estilo: el clúster
 * usa colación ICU `es-PE` (`CLAUDE.md`, convenciones de base de datos), y dónde
 * coloca ICU un guion bajo inicial —`__migrations`— no es lo mismo que dónde lo
 * coloca `Array.sort()`. Una aserción que dependiera de eso fallaría por la
 * colación y no por el esquema, que es el peor rojo posible: cierto y mudo.
 */
async function tablasDeB2b(): Promise<string[]> {
  const salida = await psql(`
    SELECT coalesce(string_agg(table_name, ','), '')
    FROM information_schema.tables WHERE table_schema = 'b2b'
  `);

  return salida === '' ? [] : salida.split(',').sort();
}

/** Las claves foráneas que salen de `b2b` hacia otro schema, con su destino. */
async function fkCruzadas(): Promise<string[]> {
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

/** Mueve el interruptor de un módulo por la pantalla y espera el reinicio real. */
async function cambiarModulo(page: Page, codigo: string, accion: 'Activar' | 'Desactivar') {
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
}

test('M07 se instala, se suelta y se reinstala sin tocar a nadie, y sus claves cruzadas vuelven', async ({
  page,
}) => {
  // Dos reinicios reales, dos migraciones completas y cirugía de dos schemas.
  test.setTimeout(600_000);
  await loginAsE2eAdmin(page);

  // =======================================================================
  // 5.1 · El schema está, con sus tablas y su historial DENTRO
  // =======================================================================
  expect(await existeSchema('b2b'), 'el arnés no migró M07').toBe('1');
  expect(
    await tablasDeB2b(),
    'el schema b2b no tiene las tablas que declara su migración',
  ).toEqual(TABLAS_B2B);

  // El historial dentro del propio schema es la regla 1 de módulos de
  // `CLAUDE.md`: `HasDefaultSchema("b2b")` más su `__migrations`. Si estuviera
  // en `public`, la lista de arriba no lo encontraría.
  expect(
    await psql("SELECT count(*) FROM b2b.__migrations WHERE \"MigrationId\" LIKE '%B2bInitial'"),
    'el historial de migraciones de M07 no está dentro de su schema',
  ).toBe('1');

  // =======================================================================
  // 5.2 y 5.3 · Idempotencia: hacerlo dos veces da lo mismo que una
  // =======================================================================
  const esquemaAntes = await tablasDeB2b();
  const contenidoAntes = await psql(`
    SELECT (SELECT count(*) FROM b2b.special_order_leads)
        || '/' || (SELECT count(*) FROM b2b.institution_requests)
        || '/' || (SELECT count(*) FROM b2b.quotes)
        || '/' || (SELECT count(*) FROM b2b.quote_number_series)
  `);

  // El seed de M07 está intencionalmente vacío (`02_seed.sql`), y se prueba
  // igual: lo que se afirma es que volver a sembrar no cambia nada, y eso solo
  // deja de ser trivial el día que deje de estar vacío.
  await psqlArchivo('/scripts/modules/b2b/02_seed.sql');
  await psqlArchivo('/scripts/modules/b2b/02_seed.sql');

  expect(await tablasDeB2b(), 'sembrar dos veces cambió el esquema de M07').toEqual(esquemaAntes);
  expect(
    await psql(`
      SELECT (SELECT count(*) FROM b2b.special_order_leads)
          || '/' || (SELECT count(*) FROM b2b.institution_requests)
          || '/' || (SELECT count(*) FROM b2b.quotes)
          || '/' || (SELECT count(*) FROM b2b.quote_number_series)
    `),
    'sembrar dos veces cambió el contenido de M07',
  ).toBe(contenidoAntes);

  // =======================================================================
  // LAS GUARDAS, con M07 instalado · AMBAS deben negarse
  // =======================================================================
  const ajenosAntes = await ajenos();

  for (const dependencia of ['catalog', 'crm']) {
    // `psqlArchivo` lanza si `psql` sale con código distinto de 0, y
    // `ON_ERROR_STOP=1` hace que el `RAISE EXCEPTION` de la guarda lo sea.
    await expect(
      psqlArchivo(`/scripts/modules/${dependencia}/99_drop.sql`),
      `con M07 instalado, soltar ${dependencia} debería rechazarse y no lo hizo`,
    ).rejects.toThrow();

    // **Y lo que importa de verdad: no ha borrado nada.** El código de salida
    // solo dice que algo falló; que el módulo siga entero es la promesa de la
    // guarda —«No se ha borrado nada»— y se comprueba por el estado.
    expect(
      await existeSchema(dependencia),
      `la guarda rechazó soltar ${dependencia} pero el schema desapareció`,
    ).toBe('1');
  }

  expect(
    await ajenos(),
    'las guardas rechazaron el drop pero algo cambió en los módulos ajenos',
  ).toBe(ajenosAntes);

  // =======================================================================
  // 1 · DESACTIVAR por el mecanismo soportado, antes de tocar el esquema
  // =======================================================================
  await cambiarModulo(page, 'b2b', 'Desactivar');
  await expect(page.locator('#modulo-b2b')).toContainText('Inactivo');

  // 2 · Y ya no quedan sus rutas ni sus enlaces.
  await page.goto('/admin');
  await expect(page.locator('main')).toBeVisible();
  await expect(
    page.getByRole('navigation', { name: 'Secciones del panel' }).locator('a[href^="/admin/solicitudes/"]'),
    'quedó un enlace de M07 con el módulo desactivado',
  ).toHaveCount(0);

  const apagado = await page.request.get('/api/admin/b2b/quotes');
  expect(apagado.status(), 'la bandeja de M07 responde con el módulo desactivado').toBe(404);

  // =======================================================================
  // 3 · SOLTAR el schema de M07 · 5.4
  // =======================================================================
  await psqlArchivo('/scripts/modules/b2b/99_drop.sql');

  expect(await existeSchema('b2b'), 'el schema b2b sigue ahí después de soltarlo').toBe('0');

  // **Nadie más se ha movido.** Las cinco claves foráneas de M07 apuntaban a
  // `crm.customers`, `catalog.products` y `catalog.product_items`: si el
  // CASCADE se hubiera ido hacia arriba, aquí faltarían clientes o productos.
  expect(
    await ajenos(),
    'soltar M07 se llevó filas de CORE, M01, M02 o M04',
  ).toBe(ajenosAntes);

  // =======================================================================
  // 4 · El host y los módulos ajenos siguen operativos
  // =======================================================================
  for (const [ruta, quien] of [
    ['/api/admin/catalog/products?pageSize=1', 'M01'],
    ['/api/admin/crm/customers?pageSize=1', 'M04'],
    ['/api/admin/settings', 'CORE'],
  ] as const) {
    const respuesta = await page.request.get(ruta);
    expect(
      respuesta.status(),
      `con el schema de M07 soltado, ${quien} dejó de responder en ${ruta}: ${respuesta.status()}`,
    ).toBe(200);
  }

  // Y el panel sigue cargando: no hay error de arranque.
  await page.goto('/admin');
  await expect(page.locator('main'), 'el panel no carga sin el schema de M07').toBeVisible();

  // 5.5 · Soltarlo dos veces no falla.
  await psqlArchivo('/scripts/modules/b2b/99_drop.sql');
  expect(await existeSchema('b2b'), 'el segundo drop de M07 recreó algo').toBe('0');

  // =======================================================================
  // Y AHORA las guardas ya no están bloqueadas POR M07 — ejecutado, no deducido
  //
  // Se hace sobre `catalog` y no sobre los dos: soltar `crm` de verdad se
  // llevaría los clientes que las specs posteriores necesitan, y la mitad que
  // podía fallar en silencio es la de arriba —la guarda diciendo no—. Aquí lo
  // que se demuestra es que el no era **por M07** y no por cualquier otra cosa.
  // =======================================================================
  expect(
    await fkCruzadas(),
    'con el schema de M07 soltado siguen existiendo claves foráneas suyas',
  ).toEqual([]);

  await psqlArchivo('/scripts/modules/catalog/99_drop.sql');
  expect(
    await existeSchema('catalog'),
    'sin M07, la guarda de M01 siguió bloqueando: el schema no se soltó',
  ).toBe('0');

  // =======================================================================
  // 5 · MIGRAR + SEMBRAR · 5.6 — y reinstalar es parte de la prueba
  // =======================================================================
  await migrate();
  await seed();

  expect(await existeSchema('catalog'), 'M01 no se pudo volver a crear').toBe('1');
  expect(await existeSchema('b2b'), 'M07 no se pudo volver a crear').toBe('1');
  expect(
    await tablasDeB2b(),
    'M07 reinstalado no tiene las mismas tablas que tenía',
  ).toEqual(TABLAS_B2B);

  // =======================================================================
  // 6 · REACTIVAR, y 7 · sus superficies vuelven
  // =======================================================================
  await cambiarModulo(page, 'b2b', 'Activar');
  await expect(page.locator('#modulo-b2b')).toContainText('Activo');

  await page.goto('/admin');
  await expect(page.locator('main')).toBeVisible();
  await expect(
    page.getByRole('navigation', { name: 'Secciones del panel' }).locator('a[href^="/admin/solicitudes/"]'),
    'al reinstalar y reactivar M07 no volvieron sus 3 enlaces de menú',
  ).toHaveCount(3);

  for (const [ruta, titulo] of [
    ['/admin/solicitudes/personalizadas', 'Solicitudes personalizadas'],
    ['/admin/solicitudes/institucionales', 'Solicitudes por volumen'],
    ['/admin/solicitudes/cotizaciones', 'Cotizaciones'],
  ] as const) {
    await page.goto(ruta);
    await expect(
      page.getByRole('heading', { name: titulo, exact: true }),
      `al reinstalar M07 la pantalla ${ruta} no volvió`,
    ).toBeVisible();
  }

  const vivo = await page.request.get('/api/admin/b2b/quotes');
  expect(vivo.status(), 'al reinstalar y reactivar M07 su bandeja no responde').toBe(200);

  // =======================================================================
  // 8 · 5.7 · Las CINCO claves foráneas cruzadas, contadas en pg_constraint
  //
  // Es el hallazgo que el plan marcaba: `DROP SCHEMA ... CASCADE` se lleva en
  // silencio las FK que otro módulo declaró, y reinstalar **el otro** no las
  // devuelve porque su migración ya consta aplicada. Aquí se reinstala M07, que
  // es el dueño de las cinco, así que tienen que volver todas.
  // =======================================================================
  expect(
    await fkCruzadas(),
    'tras reinstalar M07 faltan claves foráneas hacia crm o catalog',
  ).toEqual(
    // Las cinco, ordenadas en JavaScript igual que las ordena el ayudante: dos
    // hacia `catalog` —`products` desde las personalizaciones y
    // `product_items` desde las líneas— y tres hacia `crm.customers`, una por
    // cada tabla que guarda un cliente.
    [
      'catalog.product_items',
      'catalog.products',
      'crm.customers',
      'crm.customers',
      'crm.customers',
    ],
  );
});
