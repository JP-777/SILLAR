import type { FullConfig } from '@playwright/test';
import { composeDown } from './docker.js';
import { API_URL, DB_PORT } from './env.js';
import { buildGallery } from './gallery.js';

/**
 * Corre siempre que `globalSetup` haya terminado, pase lo que pase con las
 * pruebas: Playwright lo garantiza. Es lo que deja la base "como la
 * encontró" — no restaurando nada, sino destruyendo un stack que nunca
 * existió hasta que empezó esta corrida.
 *
 * Con `E2E_KEEP_STACK=1` no lo destruye, para poder mirar un fallo en vez de
 * reproducirlo.
 */
export default async function globalTeardown(_config: FullConfig): Promise<void> {
  console.log('[e2e] generando la galería de capturas...');
  buildGallery();

  // Con E2E_KEEP_STACK=1 el stack se queda en pie para poder mirar el fallo
  // en vez de reproducirlo desde cero. No se activa sola al fallar: el
  // teardown no recibe los resultados de las pruebas, solo la configuración,
  // y adivinarlo desde aquí dejaría stacks vivos sin que nadie lo pidiera.
  if (process.env.E2E_KEEP_STACK === '1') {
    console.log(
      '[e2e] E2E_KEEP_STACK=1: el stack se queda EN PIE.\n' +
        `[e2e]   API en ${API_URL} · base en el puerto ${DB_PORT}\n` +
        '[e2e]   Para tirarlo:  pnpm stack:down',
    );
    return;
  }

  console.log('[e2e] destruyendo el stack e2e...');
  await composeDown();
}
