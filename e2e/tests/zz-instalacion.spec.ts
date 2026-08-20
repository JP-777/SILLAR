import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { composeExec, psql } from '../setup/docker.js';
import { API_URL } from '../setup/env.js';
import { migrate, seed } from '../setup/migrate.js';

/**
 * Los tres criterios de cierre de M01 que **no tocaba ninguna prueba**: el
 * schema, la idempotencia y Swagger.
 *
 * Son del paso 5 y no dependen de las variantes, así que se pueden adelantar
 * enteros. Los tres se comprueban **haciéndolo**, no leyendo: es el criterio
 * que sostiene la promesa del producto —módulos desmontables— y una promesa
 * que nadie ejecuta no está probada.
 *
 * **El archivo se llama `zz-` a propósito.** Playwright ejecuta los archivos
 * en orden alfabético, y aquí se **desinstala el schema del catálogo**: si
 * corriera antes, dejaría sin base a todas las specs de M01. Al final, y
 * reconstruyéndolo después, no le quita nada a nadie.
 */

/** Los 27 endpoints de M01, en la forma que Swagger declara sus rutas. */
const RUTAS_M01 = [
  '/api/catalog/brands',
  '/api/admin/catalog/brands',
  '/api/admin/catalog/brands/{id}',
  '/api/catalog/categories',
  '/api/catalog/categories/{slug}',
  '/api/admin/catalog/categories',
  '/api/admin/catalog/categories/{id}',
  '/api/catalog/products',
  '/api/catalog/products/{slug}',
  '/api/admin/catalog/products',
  '/api/admin/catalog/products/{id}',
  '/api/admin/catalog/products/{id}/categories',
  '/api/admin/catalog/products/{id}/images',
  '/api/admin/catalog/products/{id}/images/{imageId}',
  '/api/admin/catalog/products/{id}/images/order',
  '/api/admin/catalog/products/{id}/items',
  '/api/admin/catalog/items/{itemId}',
  '/api/admin/catalog/items/lookup',
] as const;

test('Todos los endpoints de M01 están en Swagger', async ({ page }) => {
  await loginAsE2eAdmin(page);

  // Contra la API directamente: el proxy de Vite solo reenvía `/api` y
  // `/media`, así que pedirlo por el frontend devuelve el index.html.
  const respuesta = await page.request.get(`${API_URL}/swagger/v1/swagger.json`);
  expect(respuesta.ok(), `Swagger no responde: ${respuesta.status()}`).toBe(true);

  const doc = (await respuesta.json()) as {
    paths: Record<string, Record<string, { summary?: string; description?: string }>>;
  };

  const faltan = RUTAS_M01.filter((ruta) => !(ruta in doc.paths));
  expect(faltan, `rutas de M01 ausentes de Swagger:\n${faltan.join('\n')}`).toEqual([]);

  // **Cada operación lleva resumen.** Es la parte de «con ejemplos» que sí se
  // puede afirmar: que ninguna quede documentada solo con su verbo.
  const sinResumen: string[] = [];

  for (const ruta of RUTAS_M01) {
    for (const [verbo, operacion] of Object.entries(doc.paths[ruta])) {
      if (!operacion.summary || operacion.summary.trim() === '') {
        sinResumen.push(`${verbo.toUpperCase()} ${ruta}`);
      }
    }
  }

  expect(sinResumen, `operaciones sin resumen:\n${sinResumen.join('\n')}`).toEqual([]);

  // **Y cada cuerpo lleva su ejemplo.** Sin esto, el generador documenta cada
  // campo con `"string"`, que no le sirve a nadie que llegue de fuera.
  //
  // Se mira en `components.schemas` y **no en el `requestBody`**: cuando el
  // cuerpo es un `$ref` —que es siempre— el ejemplo vive en el esquema
  // referenciado. Buscarlo en el `requestBody` daba cero con los ejemplos ya
  // puestos: un detector mal parametrizado da el mismo rojo que uno bueno.
  const esquemas = doc.components?.schemas ?? {};

  const sinEjemplo = Object.keys(esquemas)
    .filter((nombre) => nombre.endsWith('Request'))
    .filter((nombre) => esquemas[nombre].example === undefined);

  expect(sinEjemplo, `cuerpos de petición sin ejemplo:\n${sinEjemplo.join('\n')}`).toEqual([]);
});

test('Los scripts del catálogo son idempotentes', async () => {
  // Idempotente significa que correrlo dos veces da lo mismo que correrlo una.
  // Así que se corre dos veces y se comparan **los estados**, no la ausencia
  // de excepciones: «se aplicó sin error» no demuestra que haga lo que dice.
  const antes = await psql(
    "SELECT count(*) FROM catalog.categories UNION ALL SELECT count(*) FROM catalog.brands UNION ALL SELECT count(*) FROM catalog.products",
  );

  await composeExec('db', [
    'psql', '-U', 'postgres', '-d', 'sillar_e2e', '-v', 'ON_ERROR_STOP=1',
    '-f', '/scripts/modules/catalog/02_seed.sql',
  ]);

  const despues = await psql(
    "SELECT count(*) FROM catalog.categories UNION ALL SELECT count(*) FROM catalog.brands UNION ALL SELECT count(*) FROM catalog.products",
  );

  expect(despues.trim(), 'volver a sembrar cambió el contenido del catálogo').toBe(antes.trim());
});

test('El schema catalog se elimina sin llevarse nada de core', async () => {
  test.setTimeout(180_000);

  // Lo que CORE tiene antes de desinstalar M01. Si M01 se lleva algo de aquí,
  // la promesa de módulos desmontables no se sostiene.
  const usuariosAntes = await psql('SELECT count(*) FROM core.admin_users');
  const ajustesAntes = await psql('SELECT count(*) FROM core.site_settings');
  const mediosAntes = await psql('SELECT count(*) FROM core.media_assets');

  expect(Number(usuariosAntes), 'la base de prueba debería tener usuarios').toBeGreaterThan(0);

  await composeExec('db', [
    'psql', '-U', 'postgres', '-d', 'sillar_e2e', '-v', 'ON_ERROR_STOP=1',
    '-f', '/scripts/modules/catalog/99_drop.sql',
  ]);

  // 1 · El schema ya no está.
  const schema = await psql(
    "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'catalog'",
  );
  expect(schema.trim(), 'el schema catalog sigue ahí después de desinstalarlo').toBe('0');

  // 2 · CORE sigue entero, fila por fila. Las cuatro claves foráneas de M01
  //     apuntaban a `core.media_assets`: si el CASCADE se hubiera ido hacia
  //     arriba, aquí faltarían medios.
  expect(await psql('SELECT count(*) FROM core.admin_users')).toBe(usuariosAntes);
  expect(await psql('SELECT count(*) FROM core.site_settings')).toBe(ajustesAntes);
  expect(
    await psql('SELECT count(*) FROM core.media_assets'),
    'desinstalar el catálogo se llevó medios de CORE',
  ).toBe(mediosAntes);

  // 3 · Y es idempotente: volver a desinstalarlo no falla.
  await composeExec('db', [
    'psql', '-U', 'postgres', '-d', 'sillar_e2e', '-v', 'ON_ERROR_STOP=1',
    '-f', '/scripts/modules/catalog/99_drop.sql',
  ]);

  // 4 · Se deja como se encontró. **Reinstalar es parte de la prueba**, no
  //     limpieza: el criterio dice «se crea y se elimina», así que volver a
  //     crearlo sobre una base que ya tuvo el módulo es la otra mitad.
  await migrate();
  await seed();

  const vuelta = await psql(
    "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'catalog'",
  );
  expect(vuelta.trim(), 'el schema catalog no se pudo volver a crear').toBe('1');
});
