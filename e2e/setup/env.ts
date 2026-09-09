import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { cadenaDeConexion, identidadDeLaWorktree } from '../../scripts/identidad.mjs';

/**
 * Lee `e2e/.env.e2e` a mano en vez de depender de un paquete: es un archivo
 * corto, y `docker compose --env-file` ya sabe leerlo solo. Esto es solo para
 * lo que Node necesita fuera de docker.
 *
 * **Lo que este archivo ya NO trae es la identidad de la worktree.** Ni el
 * nombre del proyecto, ni los tres puertos, ni el nombre de la base, ni la
 * cadena de conexión. Todo eso se deriva del directorio del árbol en
 * `scripts/identidad.mjs`, que explica por qué. Aquí solo quedan los valores
 * que son iguales en todos los árboles.
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

/**
 * La identidad de este árbol, calculada de su directorio.
 *
 * `ROOT` es la raíz de ESTA worktree, no la del repositorio principal: dos
 * árboles del mismo repositorio dan dos identidades distintas, que es
 * exactamente lo que hacía falta.
 */
const IDENTIDAD = identidadDeLaWorktree(ROOT);

export const PROJECT_NAME = IDENTIDAD.e2e.proyecto;

/** Nombre de la base efímera del stack e2e. Lo usa `docker.ts` para `psql -d`. */
export const DB_NAME = IDENTIDAD.e2e.base;

export const API_PORT = String(IDENTIDAD.e2e.puertoApi);
/** La API sola, sin frontend delante. Para las llamadas de `global-setup.ts`. */
export const API_URL = `http://localhost:${API_PORT}`;

/** Puerto de la base efímera. Solo se anuncia al conservar el stack con `E2E_KEEP_STACK`. */
export const DB_PORT = String(IDENTIDAD.e2e.puertoDb);

export const FRONTEND_PORT = String(IDENTIDAD.e2e.puertoFrontend);
/** El proxy de Vite: lo que Playwright navega, igual que un navegador real. */
export const FRONTEND_URL = `http://localhost:${FRONTEND_PORT}`;

const POSTGRES_USER = values.POSTGRES_USER ?? 'postgres';
const POSTGRES_PASSWORD = values.POSTGRES_PASSWORD ?? '';

/**
 * **La cadena se compone; no se lee.**
 *
 * Aquí estaba el defecto que dio origen a todo esto: `ConnectionStrings__Default`
 * llevaba el puerto escrito dentro, otra vez, al lado de un `POSTGRES_PORT`
 * que decía lo mismo. Era el valor que siempre se olvidaba al estrenar un
 * árbol, y que fuese justo ése era la señal de que no debía escribirse.
 * Ahora sale del puerto ya resuelto: no hay dos sitios que puedan discrepar.
 */
export const CONNECTION_STRING = cadenaDeConexion({
  puerto: IDENTIDAD.e2e.puertoDb,
  base: DB_NAME,
  usuario: POSTGRES_USER,
  contrasena: POSTGRES_PASSWORD,
});

/**
 * Lo que hay que pasarle a `docker compose` por el entorno del proceso para
 * que levante ESTE stack y no el del vecino.
 *
 * **Va por el entorno y no por el `--env-file` a propósito, y eso está
 * medido:** en la interpolación de compose el entorno del proceso gana al
 * archivo. Se comprobó el 6 de septiembre de 2026 lanzando
 * `POSTGRES_PORT=59999 docker compose --env-file e2e/.env.e2e config`, que
 * imprimió `published: "59999"` y no el `55432` del archivo. El diseño entero
 * se apoyaba en ese punto, así que se midió en vez de leerse.
 */
export const ENTORNO_DE_COMPOSE: Record<string, string> = {
  COMPOSE_PROJECT_NAME: PROJECT_NAME,
  POSTGRES_DB: DB_NAME,
  POSTGRES_PORT: DB_PORT,
  API_PORT,
  FRONTEND_PORT,
  ConnectionStrings__Default: CONNECTION_STRING,
};

/**
 * Carpeta de archivos subidos que la API ve montada en `/data/media`
 * (`docker-compose.yml:94`). Se resuelve contra la raíz porque `MEDIA_PATH` se
 * escribe relativo a ella —`./e2e/.media-e2e`— igual que lo lee `docker compose`.
 *
 * **La exporta `global-setup` para crearla antes de levantar nada**, y ese es
 * todo el motivo de que esté aquí: ver `global-setup.ts`.
 */
export const MEDIA_DIR = path.resolve(ROOT, values.MEDIA_PATH ?? './e2e/.media-e2e');
