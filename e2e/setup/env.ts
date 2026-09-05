import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Lee `e2e/.env.e2e` a mano en vez de depender de un paquete: es un archivo
 * de cuatro secciones, y `docker compose --env-file` ya sabe leerlo solo.
 * Esto es solo para lo que Node necesita fuera de docker: la cadena de
 * conexión para `dotnet ef` y la URL base para Playwright.
 */
function parseEnvFile(file: string): Record<string, string> {
  const result: Record<string, string> = {};

  for (const rawLine of readFileSync(file, 'utf8').split(/\r?\n/)) {
    const line = rawLine.trim();

    if (line.length === 0 || line.startsWith('#')) {
      continue;
    }

    const separator = line.indexOf('=');
    if (separator <= 0) {
      continue;
    }

    const key = line.slice(0, separator).trim();
    let value = line.slice(separator + 1).trim();

    if (value.length >= 2 && (value[0] === '"' || value[0] === "'") && value[value.length - 1] === value[0]) {
      value = value.slice(1, -1);
    }

    result[key] = value;
  }

  return result;
}

const HERE = path.dirname(fileURLToPath(import.meta.url));

/** Raíz del repositorio: `e2e/setup/env.ts` está dos niveles por debajo. */
export const ROOT = path.resolve(HERE, '..', '..');

/** Carpeta `e2e/`. */
export const E2E_DIR = path.resolve(HERE, '..');

export const ENV_FILE = path.join(E2E_DIR, '.env.e2e');

const values = parseEnvFile(ENV_FILE);

export const PROJECT_NAME = values.COMPOSE_PROJECT_NAME ?? 'sillar_e2e';

export const API_PORT = values.API_PORT ?? '55081';
/** La API sola, sin frontend delante. Para las llamadas de `global-setup.ts`. */
export const API_URL = `http://localhost:${API_PORT}`;

/** Puerto de la base efímera. Solo se anuncia al conservar el stack con `E2E_KEEP_STACK`. */
export const DB_PORT = values.POSTGRES_PORT ?? '55432';

export const FRONTEND_PORT = values.FRONTEND_PORT ?? '55173';
/** El proxy de Vite: lo que Playwright navega, igual que un navegador real. */
export const FRONTEND_URL = `http://localhost:${FRONTEND_PORT}`;

export const CONNECTION_STRING = values.ConnectionStrings__Default;

if (!CONNECTION_STRING) {
  throw new Error(`e2e/.env.e2e no define ConnectionStrings__Default`);
}

/**
 * Carpeta de archivos subidos que la API ve montada en `/data/media`
 * (`docker-compose.yml:94`). Se resuelve contra la raíz porque `MEDIA_PATH` se
 * escribe relativo a ella —`./e2e/.media-e2e`— igual que lo lee `docker compose`.
 *
 * **La exporta `global-setup` para crearla antes de levantar nada**, y ese es
 * todo el motivo de que esté aquí: ver `global-setup.ts`.
 */
export const MEDIA_DIR = path.resolve(ROOT, values.MEDIA_PATH ?? './e2e/.media-e2e');
