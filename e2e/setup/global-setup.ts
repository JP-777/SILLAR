import { chmodSync, mkdirSync, rmSync, statSync } from 'node:fs';
import path from 'node:path';
import type { FullConfig } from '@playwright/test';
import { activateModule, completeSetup, createLesserAdmin, login, waitApiReady } from './api.js';
import { composeBuildAndUpApi, composeDown, composeUpDb, waitDbHealthy } from './docker.js';
import { E2E_DIR, MEDIA_DIR } from './env.js';
import { migrate, seed } from './migrate.js';

/**
 * Crea la carpeta de medios **antes** de que la levante docker, y se asegura de
 * que el proceso de dentro del contenedor pueda escribir en ella.
 *
 * Si no existe cuando arranca el servicio, **docker la crea como `root`**. El
 * proceso de la API no es root: la imagen base define `app` con UID 1654
 * (`backend/Dockerfile:50-51`), así que toda subida responde 500 y once pruebas
 * caen a la vez sin que nada diga por qué. Es lo que le pasó a la worktree
 * `sillar-footer` el 4 de septiembre de 2026, y se diagnosticó comparando el
 * propietario con el de otra worktree, que no es una señal que esté a mano.
 *
 * **Por qué el modo y no `chown`.** Cambiar el propietario a 1654 exige ser
 * root, y el arnés no lo es ni debe serlo. Se abre en escritura para todos, que
 * es lo aceptable aquí y solo aquí: la carpeta está fuera del control de
 * versiones (`.gitignore:49`), no contiene nada del producto y `composeDown()`
 * la vacía en cada corrida. En Windows `chmodSync` no hace nada y tampoco hace
 * falta: ahí el montaje no arrastra el UID del host.
 *
 * Si la carpeta ya existe con el propietario equivocado —quedó de una corrida
 * anterior a este arreglo— `chmod` falla con `EPERM`, y entonces el arnés lo
 * dice por su nombre en vez de dejar que el fallo aparezca once pruebas después.
 */
function prepararCarpetaDeMedios(): void {
  mkdirSync(MEDIA_DIR, { recursive: true });

  try {
    chmodSync(MEDIA_DIR, 0o777);
  } catch (error) {
    const propietario = (() => {
      try {
        return `${statSync(MEDIA_DIR).uid}`;
      } catch {
        return 'desconocido';
      }
    })();

    throw new Error(
      `No se pudo abrir en escritura ${MEDIA_DIR} (propietario UID ${propietario}).\n` +
        'La creó docker como root en una corrida anterior. La API corre con UID 1654 y no\n' +
        'puede escribir dentro, así que toda subida respondería 500.\n' +
        `Bórrala y vuelve a lanzar la suite:  sudo rm -rf ${MEDIA_DIR}\n` +
        `Causa original: ${String(error)}`,
    );
  }
}

/**
 * Levanta el sistema entero, solo, para el arnés: stack docker efímero,
 * migraciones, seeds, instalación y un grafo de módulos de mentira montado —
 * exactamente lo que dice `e2e/README.md`.
 *
 * Nada de esto toca `sillar_dev`: `.env.e2e` le da a este stack su propio
 * project name, sus propios puertos y su propio volumen.
 */
export default async function globalSetup(_config: FullConfig): Promise<void> {
  const screenshots = path.join(E2E_DIR, 'screenshots');
  rmSync(screenshots, { recursive: true, force: true });
  mkdirSync(screenshots, { recursive: true });

  // Antes de docker, no después: si docker llega primero, la crea como root.
  prepararCarpetaDeMedios();

  console.log('[e2e] destruyendo un stack anterior, si quedó alguno...');
  await composeDown();

  console.log('[e2e] levantando la base de datos...');
  await composeUpDb();
  await waitDbHealthy();

  console.log('[e2e] aplicando migraciones (CORE, Catalog, Cms, CRM)...');
  await migrate();

  console.log('[e2e] aplicando seeds (sin datos de negocio)...');
  await seed();

  console.log('[e2e] construyendo y levantando la API (Debug, con módulos de mentira)...');
  await composeBuildAndUpApi();
  await waitApiReady();

  console.log('[e2e] completando la instalación...');
  await completeSetup();
  // El host se detiene tras responder /api/setup (SetupEndpoints.Complete) y
  // Docker lo relanza solo. login() reintenta mientras reciba 404: es la
  // única señal fiable de "el proceso viejo, en modo instalación, todavía no
  // cedió el puesto" — /api/setup/status relee la base en cada llamada, así
  // que puede reportar instalación completa desde ESE MISMO proceso viejo,
  // sin que eso signifique que el nuevo (con /api/admin/auth/login montado)
  // ya esté arriba.
  console.log('[e2e] iniciando sesión (reintenta hasta que el proceso nuevo esté arriba)...');
  const session = await login();
  console.log('[e2e] creando el segundo usuario, de rol admin...');
  await createLesserAdmin(session);

  console.log('[e2e] montando el grafo de módulos de mentira (las cuatro variantes de tarjeta)...');
  // core: núcleo, sin interruptor. demo_catalog + demo_sales: activos.
  // demo_crm + demo_services: inactivos y activables, sin tocar.
  // demo_service_orders + demo_tracking: bloqueados, sin tocar — les falta
  // demo_services y demo_service_orders respectivamente.
  await activateModule(session, 'demo_catalog');
  await activateModule(session, 'demo_sales');

  // M01 de verdad, no el de mentira: es el que tiene pantallas propias y el
  // que prueba `catalogo.spec.ts`. Arranca sin una sola marca —el seed no
  // trae contenido de negocio (SPEC de M01 §6.9)—, que es justo lo que hace
  // observable el estado vacío.
  console.log('[e2e] activando M01 catálogo...');
  await activateModule(session, 'catalog');

  // M02 de verdad, por el mismo motivo que M01: tiene cinco pantallas propias
  // y cuatro bloques de portada, y **sin activarlo la suite entera podía estar
  // en verde sin haber cargado una sola de ellas**. Arranca vacío —su seed no
  // trae contenido (SPEC de M02 §6.6)—, que es lo que hace observable el
  // estado vacío de `aa-vacios.spec.ts`.
  console.log('[e2e] activando M02 contenido...');
  await activateModule(session, 'cms');

  // M04 real. Se activa en el arnés porque sus pruebas HTTP necesitan que el
  // host registre el esquema de autenticación propio de clientes y sus rutas.
  //
  // **Los tres conviven a propósito.** M01 aporta una sección de portada que
  // pinta siempre, M02 cuatro que dependen de lo publicado y M04 una más: es
  // la única combinación donde el registro de contribuciones
  // (`homeContributions.tsx`) decide de verdad, en vez de acertar porque solo
  // había un candidato.
  console.log('[e2e] activando M04 clientes y contacto...');
  await activateModule(session, 'crm');

  console.log('[e2e] entorno listo.');
}
