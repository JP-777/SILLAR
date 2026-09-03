import path from 'node:path';
import { composeExec } from './docker.js';
import { CONNECTION_STRING, ROOT } from './env.js';
import { run } from './shell.js';

const BACKEND = path.join(ROOT, 'backend');

/**
 * Aplica las migraciones de CORE, Catalog, Cms y CRM contra la base e2e.
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
}

/** Los seeds del producto. Ninguno lleva datos de negocio (SPEC de M01 §6.9, de M02 §6.6). */
export async function seed(): Promise<void> {
  // `ON_ERROR_STOP=1` en los cuatro: sin él, `psql` se come el error de un
  // seed y el arnés sigue con la base a medio preparar, fallando después en
  // una prueba que no tiene la culpa.
  for (const modulo of ['core', 'catalog', 'cms', 'crm']) {
    // El de `crm` está hoy intencionalmente vacío (`SELECT 1`), y se aplica
    // igual: es el único módulo que el arnés activa, y no aplicar su seed
    // sería una asimetría que solo se nota el día que deje de estar vacío.
    await composeExec('db', ['psql', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', 'sillar_e2e', '-f', `/scripts/modules/${modulo}/02_seed.sql`]);
  }
}
