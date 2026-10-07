import path from 'node:path';
import { psqlArchivo } from './docker.js';
import { CONNECTION_STRING, ROOT } from './env.js';
import { run } from './shell.js';

const BACKEND = path.join(ROOT, 'backend');

/**
 * Aplica las migraciones de CORE, Catalog, Cms, CRM, Services, ServiceOrders,
 * Sales y B2B
 * contra la base e2e.
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
  // M05a: el instalador también lo migraría, pero la base e2e sale de aquí
  // completa, y una lista que omite un módulo es la que un día se olvida.
  await applyMigrations('Sillar.Modules.Services');
  // M05b depende duro de M05a, por lo que se migra inmediatamente después.
  // Su presencia aquí es independiente de que no tenga seed de negocio.
  await applyMigrations('Sillar.Modules.ServiceOrders');
  // M06 depende duro de M05b y su FK física apunta a
  // service_orders.service_orders, por lo que siempre se migra después.
  await applyMigrations('Sillar.Modules.Tracking');
  // **M03, y va el último porque es el único con claves foráneas cruzadas**:
  // sus dos apuntan a `catalog` y a `crm` (dependencias duras declaradas en
  // `SalesModule.cs:74`), así que esas tablas tienen que existir antes. M05a
  // solo depende de `core` (`ServicesModule.cs:24`), así que su sitio es
  // indiferente y se conserva donde `main` lo puso.
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
  // **M07 el último, por lo mismo que M03 y además de él:** sus cinco claves
  // foráneas cruzadas apuntan a `catalog` y a `crm` (dependencias duras
  // declaradas en `B2BModule.cs:57`), así que esas tablas tienen que existir
  // antes. Es la mitad del arnés de la C9 —la otra es el `ProjectReference` del
  // host—, y sin ella la etapa e2e podía ejecutar el producto entero **sin que
  // M07 existiera en el escenario**: activar `b2b` fallaría y una suite en
  // verde no diría nada de M07.
  //
  // **El hueco que M03 tenía aquí ya no es deuda:** lo cerró `main` con la
  // reparación de restaurabilidad de M03, y por eso `Sillar.Modules.Sales`
  // aparece arriba. Esta línea es el mismo arreglo aplicado a M07, no su
  // repetición.
  await applyMigrations('Sillar.Modules.B2B');
}

/** Los seeds del producto. Ninguno lleva datos de negocio (SPEC de M01 §6.9, de M02 §6.6). */
export async function seed(): Promise<void> {
  // `ON_ERROR_STOP=1` en todos: sin él, `psql` se come el error de un
  // seed y el arnés sigue con la base a medio preparar, fallando después en
  // una prueba que no tiene la culpa.
  for (const modulo of ['core', 'catalog', 'cms', 'crm', 'services', 'service_orders', 'tracking', 'sales', 'b2b']) {
    // Los de `crm`, `service_orders`, `sales` y `b2b` están hoy intencionalmente vacíos, y se
    // aplican igual: no aplicarlos sería una asimetría que solo se nota el día
    // que dejen de estar vacíos.
    await psqlArchivo(`/scripts/modules/${modulo}/02_seed.sql`);
  }
}
