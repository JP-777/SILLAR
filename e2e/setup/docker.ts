import { DB_NAME, ENTORNO_DE_COMPOSE, ENV_FILE, PROJECT_NAME, ROOT } from './env.js';
import { problemaDeStackAjeno } from './identidad.js';
import { run, runCapture, sleep } from './shell.js';

const BASE_ARGS = ['compose', '-p', PROJECT_NAME, '--env-file', ENV_FILE];

/**
 * **Toda llamada a compose pasa por aquí, y ése es el punto.**
 *
 * El `--env-file` sigue haciendo falta —trae la contraseña, el entorno de
 * ASP.NET y el perfil de compilación, que son iguales en todos los árboles—,
 * pero la identidad va por el entorno del proceso, que en la interpolación de
 * compose gana al archivo. Ver `ENTORNO_DE_COMPOSE` en `env.ts`, donde está la
 * medida que lo demuestra.
 *
 * Los dos envoltorios existen para que no haya ninguna llamada que se pueda
 * escribir olvidándose del entorno: es la misma lección que puso la guarda
 * dentro de `composeDown` en vez de en sus llamadores. Una precaución que hay
 * que acordarse de repetir en diez sitios ya falló en uno.
 */
const OPCIONES = { cwd: ROOT, env: ENTORNO_DE_COMPOSE } as const;

function compose(args: string[]): Promise<void> {
  return run('docker', [...BASE_ARGS, ...args], OPCIONES);
}

function composeCapture(args: string[]): Promise<string> {
  return runCapture('docker', [...BASE_ARGS, ...args], OPCIONES);
}

/**
 * Destruye el stack e2e por completo, volumen incluido. Segura de llamar sobre
 * nada — y, desde el 5 de septiembre de 2026, **segura de llamar sobre el stack
 * de otro**: si lo levantó otra worktree, no lo toca.
 *
 * **Por qué la guarda está aquí y no solo en quien llama.** Estuvo en
 * `global-setup`, y no servía: `globalTeardown` se ejecuta igual cuando
 * `globalSetup` lanza —medido, no supuesto— y llamaba a esta función sin
 * preguntar, así que el stack ajeno moría de todos modos un segundo más tarde.
 * La guarda va en la operación destructiva, no en uno de sus llamadores: así
 * cubre a los dos y a cualquiera que se añada después.
 */
export async function composeDown(): Promise<void> {
  const problema = problemaDeStackAjeno(await duenoDelStackEnPie(), ROOT);

  if (problema !== null) {
    console.error(`[e2e] NO se destruye el stack, porque no es de esta worktree.\n  ${problema}`);
    return;
  }

  return compose(['down', '-v']);
}

/**
 * Desde qué worktree se levantó el stack e2e que ya está en pie, si lo hay.
 *
 * **No hace falta inventar un marcador:** `docker compose` etiqueta cada
 * contenedor con `com.docker.compose.project.working_dir`, que es exactamente
 * el árbol desde el que se lanzó. Se lee esa.
 *
 * Devuelve `null` si no hay ningún contenedor del proyecto en pie, o si no se
 * pudo preguntar —que aquí sí es lo mismo a efectos de decidir: sin dato no se
 * bloquea a nadie, porque una barrera que para en falso sobre el arnés
 * compartido bloquearía a los dos frentes en vez de a uno.
 */
export async function duenoDelStackEnPie(): Promise<string | null> {
  const ids = await composeCapture(['ps', '-q']).catch(() => '');
  const primero = ids.trim().split('\n').filter(Boolean)[0];

  if (!primero) {
    return null;
  }

  const dir = await runCapture(
    'docker',
    ['inspect', '-f', '{{index .Config.Labels "com.docker.compose.project.working_dir"}}', primero],
    { cwd: ROOT },
  ).catch(() => '');

  return dir.trim() || null;
}

/** Levanta solo la base de datos. */
export function composeUpDb(): Promise<void> {
  return compose(['up', '-d', 'db']);
}

/**
 * Construye y levanta la API con el perfil `full`. `.env.e2e` fija
 * `BUILD_CONFIGURATION=Debug` y `MODULES_INCLUDE_DEMO=true`, así que esta
 * imagen —`sillar-api-e2e`, nunca `sillar-api`— trae el grafo de módulos de
 * mentira que hacen falta para ver las cuatro variantes de tarjeta.
 */
export function composeBuildAndUpApi(): Promise<void> {
  return compose(['--profile', 'full', 'up', '-d', '--build', 'api']);
}

/** Ejecuta un comando dentro de un servicio ya levantado (para `psql`, típicamente). */
export function composeExec(service: string, args: string[]): Promise<void> {
  return compose(['exec', '-T', service, ...args]);
}

/**
 * Ejecuta una consulta y devuelve su resultado en crudo, sin cabeceras.
 *
 * `-tA` quita el marco y el relleno: lo que vuelve es el valor, que es lo que
 * se puede comparar entre dos momentos.
 */
export async function psql(sql: string): Promise<string> {
  const salida = await composeCapture([
    'exec', '-T', 'db',
    'psql', '-U', 'postgres', '-d', DB_NAME, '-tA', '-c', sql,
  ]);

  return salida.trim();
}

/**
 * Aplica un archivo `.sql` **contra la base de este árbol**.
 *
 * **Por qué existe, y es una lección que ya habíamos aprendido.** El nombre de
 * la base lo escribían los llamadores, uno por uno, dentro de un
 * `composeExec('db', ['psql', …, '-d', 'sillar_e2e', …])`. Cuando la identidad
 * pasó a derivarse del directorio, esa base dejó de existir con ese nombre y
 * los seeds murieron con
 *
 *     FATAL: database "sillar_e2e" does not exist
 *
 * en las dos worktrees a la vez, después de que el stack se hubiera levantado
 * correctamente — que es lo que hace el fallo difícil de leer: los contenedores
 * tenían el nombre bueno y la consulta iba al nombre viejo.
 *
 * Se arregló como se arregló la escopeta de `composeDown`: **la operación es la
 * dueña del dato, no quien la llama**. Un llamador nuevo no puede escribir el
 * nombre mal porque ya no escribe ningún nombre.
 */
export function psqlArchivo(rutaEnElContenedor: string): Promise<void> {
  return composeExec('db', [
    'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', DB_NAME,
    '-f', rutaEnElContenedor,
  ]);
}

/** Espera a que el contenedor de la base de datos esté `healthy`. */
export async function waitDbHealthy(timeoutMs = 60_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const status = await composeCapture(['ps', 'db', '--format', '{{.Health}}']).catch(() => '');

    if (status.trim() === 'healthy') {
      return;
    }

    await sleep(2000);
  }

  throw new Error('La base de datos e2e no llegó a "healthy" a tiempo.');
}

/**
 * **Qué ejecución del servicio está corriendo ahora mismo**, como una cadena
 * comparable: `<container-id>:<started-at>`.
 *
 * Las dos mitades hacen falta. El identificador cambia cuando Compose recrea
 * el contenedor; la marca de arranque cambia cuando **el mismo** contenedor se
 * reinicia, que es lo que hace Docker cuando el host se detiene solo. Con una
 * sola de las dos, la mitad de los reinicios pasaría desapercibida.
 *
 * El identificador se pide a Compose por el **nombre del servicio**, nunca por
 * el del contenedor: ese nombre lo compone Compose con el proyecto, y
 * escribirlo a mano aquí lo rompería el día que cambie `PROJECT_NAME`.
 *
 * Devuelve cadena vacía si en este instante no hay nada que mirar —el
 * contenedor entre dos vidas—, que durante un reinicio es un estado normal y
 * no un error.
 */
export async function serviceRuntimeIdentity(service: string): Promise<string> {
  const containerId = await composeCapture(['ps', '-q', service])
    .then((salida) => salida.trim())
    .catch(() => '');

  if (!containerId) {
    return '';
  }

  const startedAt = await runCapture(
    'docker',
    ['inspect', '--format', '{{.State.StartedAt}}', containerId],
    { cwd: ROOT },
  )
    .then((salida) => salida.trim())
    .catch(() => '');

  return startedAt ? `${containerId}:${startedAt}` : '';
}

/**
 * Espera a que el servicio esté corriendo **otra ejecución** distinta de la que
 * se observó antes.
 *
 * **Es una pregunta a Docker, no una estimación.** La alternativa —esperar un
 * rato, o dar por bueno que la API responde— no distingue el proceso viejo del
 * nuevo: mientras el host anterior siga aceptando conexiones, cualquier sondeo
 * contesta que todo va bien y el reinicio ni siquiera ha empezado.
 *
 * `previa` tiene que venir con algo. Comparar contra una cadena vacía daría
 * por bueno el primer estado que apareciera, que es exactamente el error que
 * esta función existe para no cometer.
 */
export async function waitServiceRestarted(
  service: string,
  previa: string,
  timeoutMs = 120_000,
): Promise<void> {
  if (!previa) {
    throw new Error(
      `No se puede esperar el reinicio de '${service}': no se pudo leer su identidad antes.`,
    );
  }

  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const actual = await serviceRuntimeIdentity(service);

    // Vacía significa «todavía no hay contenedor que mirar», que es justo lo
    // que pasa en medio del reinicio. Se sigue esperando.
    if (actual && actual !== previa) {
      return;
    }

    await sleep(1000);
  }

  throw new Error(
    `'${service}' no se reinició en ${timeoutMs / 1000} s: sigue en la ejecución ${previa}.`,
  );
}
