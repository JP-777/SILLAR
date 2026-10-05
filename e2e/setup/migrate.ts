import path from 'node:path';
import { psqlArchivo } from './docker.js';
import { CONNECTION_STRING, ROOT } from './env.js';
import { run } from './shell.js';

const BACKEND = path.join(ROOT, 'backend');

/**
 * Aplica las migraciones de CORE, Catalog, Cms, CRM y B2B contra la base e2e.
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
  // **M07, y va el último porque sus claves foráneas cruzadas apuntan a
  // `catalog` y a `crm`** (dependencias duras declaradas en `B2BModule.cs:57`):
  // sus tablas tienen que existir antes. Es la mitad del arnés de la C9 —la
  // otra es el `ProjectReference` del host, ya puesto—, y sin ella la etapa e2e
  // podía ejecutar el producto entero **sin que M07 existiera en el escenario**:
  // activar `b2b` fallaría, y una suite en verde no diría nada de M07. Es lo
  // mismo que le pasó a M02 y a M04 antes de sus dos líneas de arriba.
  await applyMigrations('Sillar.Modules.B2B');
}

/** Los seeds del producto. Ninguno lleva datos de negocio (SPEC de M01 §6.9, de M02 §6.6). */
export async function seed(): Promise<void> {
  // `ON_ERROR_STOP=1` en los cinco: sin él, `psql` se come el error de un
  // seed y el arnés sigue con la base a medio preparar, fallando después en
  // una prueba que no tiene la culpa.
  for (const modulo of ['core', 'catalog', 'cms', 'crm', 'b2b']) {
    // Los de `crm` y `b2b` están hoy intencionalmente vacíos, y se aplican
    // igual: son módulos que el arnés activa, y no aplicar su seed sería una
    // asimetría que solo se nota el día que dejen de estar vacíos.
    await psqlArchivo(`/scripts/modules/${modulo}/02_seed.sql`);
  }
}
