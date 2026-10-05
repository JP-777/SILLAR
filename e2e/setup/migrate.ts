import path from 'node:path';
import { psqlArchivo } from './docker.js';
import { CONNECTION_STRING, ROOT } from './env.js';
import { run } from './shell.js';

const BACKEND = path.join(ROOT, 'backend');

/**
 * Aplica las migraciones de CORE, Catalog, Cms, CRM y Sales contra la base e2e.
 *
 * `ConnectionStrings__Default` se pasa como variable de entorno real al
 * proceso `dotnet`, no por `.env`: `DotEnv.Load()` nunca sobreescribe lo que
 * ya está en el entorno (`Sillar.Shared/Configuration/DotEnv.cs`), así que
 * esto es lo único que impide que apunte, por accidente, al `.env` de
 * desarrollo de la raíz del repositorio.
 */
async function applyMigrations(csproj: string): Promise<void> {
  await run(
    'dotnet',
    ['ef', 'database', 'update', '--project', csproj, '--startup-project', 'Sillar.Api'],
    { cwd: BACKEND, env: { ConnectionStrings__Default: CONNECTION_STRING } },
  );
}

export async function migrate(): Promise<void> {
  await applyMigrations('Sillar.Core');
  await applyMigrations('Sillar.Modules.Catalog');
  // **M02 se aplica desde que existe, no desde que alguien lo pruebe.** Sin
  // esta línea el schema `cms` no está en la base, así que activar el módulo
  // falla y sus pantallas no existen — que es por lo que una suite entera en
  // verde podía no decir nada de M02.
  await applyMigrations('Sillar.Modules.Cms');
  // Y M04 por lo mismo: sin su schema, activar `crm` falla y con él se caen
  // el acceso de clientes, el perfil y la bandeja de contacto.
  await applyMigrations('Sillar.Modules.Crm');
  // **M03, y va el último porque sus dos claves foráneas cruzadas apuntan a
  // `catalog` y a `crm`**: esas tablas tienen que existir antes.
  //
  // **Por qué estaba ausente y por qué eso era un defecto.** La instalación del
  // arnés va por `POST /api/setup`, que migra **todos los módulos del binario**,
  // y M03 está en el binario (`Sillar.Api.csproj`). Así que el escenario e2e
  // siempre tuvo el schema `sales`… **pero `migrate()` no sabía reconstruirlo**.
  // Los dos caminos de instalación no convergían: una prueba destructiva que
  // soltara `sales` —o que se llevara sus claves foráneas con un `CASCADE`
  // ajeno— dejaba la base sin forma de volver, porque su historial de
  // migraciones ya no estaba para reaplicarse, o estaba y entonces EF no
  // reaplicaba nada.
  //
  // No se activa `sales` en `global-setup.ts`: esto repara la
  // **restaurabilidad** del arnés, no la cobertura funcional de M03.
  await applyMigrations('Sillar.Modules.Sales');
}

/** Los seeds del producto. Ninguno lleva datos de negocio (SPEC de M01 §6.9, de M02 §6.6). */
export async function seed(): Promise<void> {
  // `ON_ERROR_STOP=1` en los cinco: sin él, `psql` se come el error de un
  // seed y el arnés sigue con la base a medio preparar, fallando después en
  // una prueba que no tiene la culpa.
  for (const modulo of ['core', 'catalog', 'cms', 'crm', 'sales']) {
    // Los de `crm` y `sales` están hoy intencionalmente vacíos, y se aplican
    // igual: no aplicarlos sería una asimetría que solo se nota el día que
    // dejen de estar vacíos.
    await psqlArchivo(`/scripts/modules/${modulo}/02_seed.sql`);
  }
}
