import path from 'node:path';
import { composeExec } from './docker.js';
import { CONNECTION_STRING, ROOT } from './env.js';
import { run } from './shell.js';

const BACKEND = path.join(ROOT, 'backend');

/**
 * Aplica las migraciones de CORE y Catalog contra la base e2e.
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
}

/** Los dos seeds del producto. Ninguno lleva datos de negocio (SPEC de M01 §6.9). */
export async function seed(): Promise<void> {
  await composeExec('db', ['psql', '-U', 'postgres', '-d', 'sillar_e2e', '-f', '/scripts/modules/core/02_seed.sql']);
  await composeExec('db', ['psql', '-U', 'postgres', '-d', 'sillar_e2e', '-f', '/scripts/modules/catalog/02_seed.sql']);
}
