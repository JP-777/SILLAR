import { ENV_FILE, PROJECT_NAME, ROOT } from './env.js';
import { run, runCapture, sleep } from './shell.js';

const BASE_ARGS = ['compose', '-p', PROJECT_NAME, '--env-file', ENV_FILE];

/** Destruye el stack e2e por completo, volumen incluido. Segura de llamar sobre nada. */
export function composeDown(): Promise<void> {
  return run('docker', [...BASE_ARGS, 'down', '-v'], { cwd: ROOT });
}

/** Levanta solo la base de datos. */
export function composeUpDb(): Promise<void> {
  return run('docker', [...BASE_ARGS, 'up', '-d', 'db'], { cwd: ROOT });
}

/**
 * Construye y levanta la API con el perfil `full`. `.env.e2e` fija
 * `BUILD_CONFIGURATION=Debug` y `MODULES_INCLUDE_DEMO=true`, así que esta
 * imagen —`sillar-api-e2e`, nunca `sillar-api`— trae el grafo de módulos de
 * mentira que hacen falta para ver las cuatro variantes de tarjeta.
 */
export function composeBuildAndUpApi(): Promise<void> {
  return run('docker', [...BASE_ARGS, '--profile', 'full', 'up', '-d', '--build', 'api'], { cwd: ROOT });
}

/** Ejecuta un comando dentro de un servicio ya levantado (para `psql`, típicamente). */
export function composeExec(service: string, args: string[]): Promise<void> {
  return run('docker', [...BASE_ARGS, 'exec', '-T', service, ...args], { cwd: ROOT });
}

/**
 * Ejecuta una consulta y devuelve su resultado en crudo, sin cabeceras.
 *
 * `-tA` quita el marco y el relleno: lo que vuelve es el valor, que es lo que
 * se puede comparar entre dos momentos.
 */
export async function psql(sql: string): Promise<string> {
  const salida = await runCapture(
    'docker',
    [...BASE_ARGS, 'exec', '-T', 'db', 'psql', '-U', 'postgres', '-d', 'sillar_e2e', '-tA', '-c', sql],
    { cwd: ROOT },
  );

  return salida.trim();
}

/** Espera a que el contenedor de la base de datos esté `healthy`. */
export async function waitDbHealthy(timeoutMs = 60_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const status = await runCapture(
      'docker',
      [...BASE_ARGS, 'ps', 'db', '--format', '{{.Health}}'],
      { cwd: ROOT },
    ).catch(() => '');

    if (status.trim() === 'healthy') {
      return;
    }

    await sleep(2000);
  }

  throw new Error('La base de datos e2e no llegó a "healthy" a tiempo.');
}
