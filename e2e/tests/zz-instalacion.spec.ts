import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { psql, psqlArchivo } from '../setup/docker.js';
import { API_URL } from '../setup/env.js';
import { migrate, seed } from '../setup/migrate.js';
/**
 * El estado estructural de M03, para comparar los dos caminos de instalación.
 *
 * **Por qué esta sonda existe.** `POST /api/setup` instala Sales desde el
 * binario y `migrate()+seed()` no sabía restaurarlo. Los dos caminos tienen que
 * converger al mismo estado de base, y «sales existe» no lo demuestra: hay que
 * comparar **estructura e historial**. Cada campo de aquí es uno de los
 * aspectos que el encargo exige contrastar.
 *
 * Las listas se ordenan en JavaScript y no en SQL: el clúster usa colación ICU
 * `es-PE`, y dónde coloca ICU un `__migrations` inicial no es dónde lo coloca
 * `Array.sort()`. Depender de eso daría un rojo cierto y mudo.
 */
async function estadoDeSales() {
  const lista = async (sql: string) => {
    const salida = await psql(sql);
    return salida === '' ? [] : salida.split(',').sort();
  };

  // **El historial se lee solo si la tabla existe, y eso no es prudencia
  // decorativa.** PostgreSQL planifica la consulta entera antes de ejecutarla,
  // así que nombrar `sales.__migrations` cuando no existe falla en el parseo —
  // ni un `CASE WHEN to_regclass(...) IS NULL` lo salva—. Esta sonda tiene que
  // poder describir también el estado «M03 no está», que es justo el que la
  // prueba comprueba después de soltarlo: sin esto, la sonda revienta en vez de
  // contestar «vacío», y el rojo habla de psql en lugar del defecto.
  const historial = async () =>
    (await psql("SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'sales' AND c.relname = '__migrations'")) === '1'
      ? lista(`SELECT coalesce(string_agg("MigrationId", ','), '') FROM sales.__migrations`)
      : [];

  return {
    schema: await psql(
      "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'sales'",
    ),
    tablas: await lista(
      "SELECT coalesce(string_agg(table_name, ','), '') FROM information_schema.tables WHERE table_schema = 'sales'",
    ),
    migraciones: await historial(),
    // Por identidad y destino, no por cantidad: dos claves distintas hacia el
    // mismo sitio contarían igual y no serían lo mismo.
    fkCruzadas: await lista(`
      SELECT coalesce(string_agg(c.conname || '->' || rn.nspname || '.' || r.relname, ','), '')
        FROM pg_constraint c
        JOIN pg_class     d  ON d.oid  = c.conrelid
        JOIN pg_namespace dn ON dn.oid = d.relnamespace
        JOIN pg_class     r  ON r.oid  = c.confrelid
        JOIN pg_namespace rn ON rn.oid = r.relnamespace
       WHERE c.contype = 'f' AND dn.nspname = 'sales' AND rn.nspname <> 'sales'
    `),
    dependenciasDuras: await lista(`
      SELECT coalesce(string_agg(DISTINCT t.code, ','), '')
        FROM core.module_dependencies md
        JOIN core.modules m ON m.module_id = md.module_id
        JOIN core.modules t ON t.module_id = md.depends_on_module_id
       WHERE md.kind = 'hard' AND m.code = 'sales'
    `),
  };
}


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

test('Swagger lleva las rutas de M01, y ningún cuerpo de ningún módulo se queda sin ejemplo', async ({ page }) => {
  await loginAsE2eAdmin(page);

  // Contra la API directamente: el proxy de Vite solo reenvía `/api` y
  // `/media`, así que pedirlo por el frontend devuelve el index.html.
  const respuesta = await page.request.get(`${API_URL}/swagger/v1/swagger.json`);
  expect(respuesta.ok(), `Swagger no responde: ${respuesta.status()}`).toBe(true);

  const doc = (await respuesta.json()) as {
    paths: Record<string, Record<string, { summary?: string; description?: string }>>;
    components?: { schemas?: Record<string, { example?: unknown }> };
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
  // **Esta mitad no es de M01, es de la plataforma**, y el nombre de la
  // prueba lo decía mal hasta el 25 de agosto de 2026: recorre *todos* los
  // `*Request` del documento, venga de donde venga. Se llamaba «Todos los
  // endpoints de M01 están en Swagger», así que **nadie fue a mirarla cuando
  // entró M02** — y los doce cuerpos de M02 llevaban desde el primer día sin
  // ejemplo. Una comprobación de plataforma con nombre de módulo es una
  // comprobación que solo se lee cuando toca ese módulo.
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

  await psqlArchivo('/scripts/modules/catalog/02_seed.sql');

  const despues = await psql(
    "SELECT count(*) FROM catalog.categories UNION ALL SELECT count(*) FROM catalog.brands UNION ALL SELECT count(*) FROM catalog.products",
  );

  expect(despues.trim(), 'volver a sembrar cambió el contenido del catálogo').toBe(antes.trim());
});

test('El schema catalog se elimina sin llevarse nada de core, y M03 vuelve igual que lo dejó la instalación', async () => {
  test.setTimeout(300_000);

  // Lo que CORE tiene antes de desinstalar M01. Si M01 se lleva algo de aquí,
  // la promesa de módulos desmontables no se sostiene.
  const usuariosAntes = await psql('SELECT count(*) FROM core.admin_users');
  const ajustesAntes = await psql('SELECT count(*) FROM core.site_settings');
  const mediosAntes = await psql('SELECT count(*) FROM core.media_assets');

  expect(Number(usuariosAntes), 'la base de prueba debería tener usuarios').toBeGreaterThan(0);

  // =======================================================================
  // DOS dependientes duros de M01, y los dos tienen que salir primero
  //
  // `catalog/99_drop.sql` lleva una guarda que se niega mientras otro módulo
  // instalado dependa de él de forma dura, y en este árbol hay **dos**: `sales`
  // (M03) y `b2b` (M07). Los dos están en el binario, así que la instalación los
  // crea, y los dos declaran claves foráneas hacia `catalog`.
  //
  // Se retiran los dos por su propio mecanismo y vuelven con el mismo
  // `migrate()` + `seed()` de abajo. **El rechazo de la guarda con cualquiera de
  // ellos presente es el comportamiento correcto, no un fallo.**
  //
  // El orden entre `b2b` y `sales` es libre: ninguno declara claves foráneas
  // hacia el otro —la dependencia de M03 sobre M07 es **blanda**, y una blanda
  // no lleva FK—. Lo que no es libre es que los dos vayan antes de `catalog`.
  // =======================================================================

  const salesAntes = await estadoDeSales();

  // **La preparación se comprueba, y es la guarda que sostiene toda la
  // prueba.** Si el detector no encontrara nada —por un cambio de nombre, por
  // mirar el schema equivocado, por un `string_agg` que devuelve vacío— esta
  // prueba destruiría el catálogo y luego confirmaría en verde que «las cero
  // claves foráneas volvieron». Una detección vacía no puede producir verde.
  expect(
    salesAntes.schema,
    'la instalación del arnés debería haber creado el schema sales: M03 está en el binario',
  ).toBe('1');
  expect(
    salesAntes.fkCruzadas.filter((fk) => fk.includes('->catalog.')),
    'no se encontró ninguna clave foránea de sales hacia catalog, así que no hay nada que acreditar: revisa el detector antes de destruir nada',
  ).not.toEqual([]);
  expect(
    salesAntes.migraciones,
    'sales no tiene historial de migraciones: sin él no se puede afirmar que se reaplicaron',
  ).not.toEqual([]);

  // M03 primero, por su mecanismo soportado. Sin esto la guarda de M01 se
  // niega, y con razón.
  await psqlArchivo('/scripts/modules/sales/99_drop.sql');

  expect(
    (await estadoDeSales()).schema,
    'sales sigue ahí después de soltarlo: la guarda de catalog se va a negar',
  ).toBe('0');


  // M07 también. Se comprueba que está antes de soltarlo: si no estuviera, el
  // drop sería un no-op y la prueba seguiría como si lo hubiera retirado.
  expect(
    await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'b2b'"),
    'la instalación del arnés debería haber creado el schema b2b: M07 está en el binario',
  ).toBe('1');

  await psqlArchivo('/scripts/modules/b2b/99_drop.sql');

  expect(
    await psql("SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'b2b'"),
    'b2b sigue ahí después de soltarlo: la guarda de catalog se va a negar',
  ).toBe('0');

  await psqlArchivo('/scripts/modules/catalog/99_drop.sql');

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
  await psqlArchivo('/scripts/modules/catalog/99_drop.sql');

  // 4 · Se deja como se encontró. **Reinstalar es parte de la prueba**, no
  //     limpieza: el criterio dice «se crea y se elimina», así que volver a
  //     crearlo sobre una base que ya tuvo el módulo es la otra mitad.
  await migrate();
  await seed();

  const vuelta = await psql(
    "SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('catalog', 'sales', 'b2b')",
  );
  expect(
    vuelta.trim(),
    'no se pudieron volver a crear los tres schemas que esta prueba suelta',
  ).toBe('3');

  // =======================================================================
  // 5 · Y M03 vuelve **equivalente**, no «vuelve»
  //
  // Es el hallazgo causal de esta reparación, dicho en una aserción:
  //
  //   `POST /api/setup` instala Sales desde el binario y `migrate()+seed()` no
  //   sabía restaurarlo.
  //
  // Los dos caminos de instalación tienen que converger al mismo estado de
  // base. Se comparan los cinco aspectos de `estadoDeSales()` —schema, tablas,
  // historial, claves cruzadas por identidad y destino, y dependencias duras
  // registradas—, no «sales existe».
  // =======================================================================
  const salesDespues = await estadoDeSales();

  expect(salesDespues.schema, 'M03 no volvió: migrate() no lo reconstruye').toBe(salesAntes.schema);
  expect(salesDespues.tablas, 'M03 volvió con otras tablas').toEqual(salesAntes.tablas);
  expect(
    salesDespues.migraciones,
    'M03 volvió con otro historial de migraciones',
  ).toEqual(salesAntes.migraciones);
  expect(
    salesDespues.fkCruzadas,
    'M03 volvió sin las mismas claves foráneas cruzadas: el CASCADE de catalog se las llevó y nadie las devolvió',
  ).toEqual(salesAntes.fkCruzadas);
  expect(
    salesDespues.dependenciasDuras,
    'las dependencias duras registradas de M03 cambiaron',
  ).toEqual(salesAntes.dependenciasDuras);

  // 6 · Y nada ajeno quedó dañado por la cirugía de los dos schemas.
  expect(await psql('SELECT count(*) FROM core.admin_users')).toBe(usuariosAntes);
  expect(
    await psql('SELECT count(*) FROM core.media_assets'),
    'la cirugía de catalog y sales se llevó medios de CORE',
  ).toBe(mediosAntes);
  expect(
    await psql(
      "SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('core', 'cms', 'crm')",
    ),
    'la cirugía se llevó algún schema que no tocaba',
  ).toBe('3');
});
