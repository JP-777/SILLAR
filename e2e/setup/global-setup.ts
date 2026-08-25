import { mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import type { FullConfig } from '@playwright/test';
import { activateModule, completeSetup, createLesserAdmin, login, waitApiReady } from './api.js';
import { composeBuildAndUpApi, composeDown, composeUpDb, waitDbHealthy } from './docker.js';
import { E2E_DIR } from './env.js';
import { migrate, seed } from './migrate.js';

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

  console.log('[e2e] destruyendo un stack anterior, si quedó alguno...');
  await composeDown();

  console.log('[e2e] levantando la base de datos...');
  await composeUpDb();
  await waitDbHealthy();

  console.log('[e2e] aplicando migraciones (CORE, Catalog, Cms)...');
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

  console.log('[e2e] entorno listo.');
}
