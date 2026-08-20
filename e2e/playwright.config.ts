import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';
import { API_URL, E2E_DIR, FRONTEND_PORT, FRONTEND_URL } from './setup/env.js';

const FRONTEND_DIR = path.join(E2E_DIR, '..', 'frontend');

/**
 * Arnés de extremo a extremo de SILLAR: backend y frontend juntos, contra un
 * stack docker efímero que `globalSetup`/`globalTeardown` levantan y
 * destruyen solos. Ver `e2e/README.md` antes de tocar nada de aquí.
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  // El stack es uno solo, compartido por toda la corrida: dos specs
  // escribiendo el mismo grafo de módulos a la vez se pisarían.
  workers: 1,
  retries: 0,
  reporter: [['html', { outputFolder: './playwright-report', open: 'never' }], ['list']],
  globalSetup: './setup/global-setup.ts',
  globalTeardown: './setup/global-teardown.ts',
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  // Vite, no la API: es lo que un navegador real ve. Su proxy
  // (frontend/vite.config.ts) reenvía /api y /media a la API del stack e2e,
  // así que la cookie de sesión viaja sin fricción de CORS, igual que en
  // desarrollo. Playwright lo arranca y lo destruye solo; docker no sabe que
  // existe.
  webServer: {
    command: `pnpm dev --port ${FRONTEND_PORT} --strictPort`,
    cwd: FRONTEND_DIR,
    url: FRONTEND_URL,
    reuseExistingServer: false,
    stdout: 'pipe',
    stderr: 'pipe',
    timeout: 30_000,
    env: {
      SILLAR_API_ORIGIN: API_URL,
    },
  },
  use: {
    baseURL: FRONTEND_URL,
    trace: 'retain-on-failure',
    // Las capturas "de verdad" las hace themeRecorder, a propósito, en los
    // pasos relevantes. Esta es solo la red de seguridad de Playwright para
    // cuando algo falla en un paso que no se instrumentó.
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      // La transversal **otra vez**, con `prefers-reduced-motion` activo.
      //
      // Sin esto la política de movimiento estaba escrita y sin ejercitar por
      // nadie: la regla global sustituye las transiciones de TODO elemento por
      // un fundido de opacidad, incluidos los que antes cambiaban de golpe. Si
      // algún control dependiera de desaparecer para dejar de recibir eventos,
      // aquí es donde se ve — y no en una lectura del CSS.
      name: 'chromium-movimiento-reducido',
      testMatch: /transversal\.spec\.ts/,
      // Va en `contextOptions`: el `use` de un proyecto no lo expone como
      // propiedad de primer nivel en esta versión de Playwright.
      use: {
        ...devices['Desktop Chrome'],
        contextOptions: { reducedMotion: 'reduce' },
      },
    },
  ],
});
