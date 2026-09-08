#!/usr/bin/env node
/**
 * La puerta: lo que tiene que pasar antes de dar algo por terminado.
 *
 *     node scripts/verificar.mjs
 *
 * **Existe porque una regla escrita hay que acordarse de cumplirla y un
 * comando o pasa o no pasa.** Había tres comprobaciones lanzándose a mano
 * —las pruebas del backend, los tipos del frontend y la suite e2e— y por eso
 * un error de tipos vivió días dentro de una spec sin salir por ningún lado:
 * Playwright transpila sin comprobar tipos, así que la prueba corría igual.
 *
 * **Las etapas van de barata a cara.** Un error de tipos se ve en segundos y
 * no tiene por qué costar los diez minutos de la suite. La etapa de pruebas
 * backend tiene ahora una frontera deliberada: typechecks y build siguen
 * siendo baratos, pero antes de los tests backend se crea una PostgreSQL
 * efímera. Ese coste es deliberado porque existen pruebas de persistencia que
 * necesitan constraints, migraciones, triggers y concurrencia reales. La
 * puerta canónica completa requiere Docker/PostgreSQL; los comandos
 * individuales typecheck/build siguen pudiéndose ejecutar por separado cuando
 * solo se quiere feedback rápido.
 *
 * Node y no bash ni PowerShell: el desarrollo alterna entre Windows y Arch
 * Linux (ADR-006), y esto tiene que servir en los dos.
 *
 * **Se lanza a secas: `node scripts/verificar.mjs`.** No lleva envoltorio, y eso
 * es una decisión, no un olvido. Durante un tiempo se documentó anteponerle
 * `kde-inhibit` y `systemd-inhibit` contra la suspensión —son veinte minutos sin
 * que nadie toque el teclado— y `env PATH=…` para que `dotnet ef` se encontrara.
 * Las dos cosas las hace ahora la propia puerta, más abajo, por dos motivos:
 * un paso manual se olvida, y el envoltorio **estaba mal**. `kde-inhibit` no
 * propaga el código de salida de su hijo, así que convertía cualquier rojo en un
 * cero para quien mirase `$?`. Ver `docs/ENTORNO.md`.
 *
 * **BD efímera para las pruebas backend.** Las pruebas PostgreSQL (CRM, CMS)
 * son destructivas —TRUNCATE, DROP SCHEMA— y nunca deben tocar sillar_dev ni
 * sillar_e2e. La puerta crea una base propia `sillar_verify_<timestamp>_<pid>`
 * a partir del servidor del contenedor `db`, aplica migraciones (sin seeds:
 * las pruebas crean sus propios datos), ejecuta las pruebas backend contra
 * ella y la destruye al terminar, también si fallan. El nombre exacto se
 * pasa además al proceso backend en `SILLAR_VERIFY_DATABASE`; los fixtures de
 * CRM y CMS comprueban que `Database` en la cadena coincida con ese valor —es
 * la única autoridad para el nombre de la base— y fallan inmediatamente si no.
 *
 * **Limpieza.** El `finally` destruye la base efímera en una ejecución normal
 * o con fallo normal. Un barrido inicial elimina bases huérfanas de
 * ejecuciones abortadas por SIGKILL, corte eléctrico o timeout externo: lista
 * las bases `sillar_verify_<timestamp>_<pid>`, interpreta el timestamp y
 * elimina solo las que superan las 12 horas —deliberadamente muy superior a
 * una ejecución normal— para no destruir otra puerta potencialmente activa.
 * SIGKILL no puede ejecutar `finally`; no se afirma lo contrario.
 *
 * Playwright sigue siendo dueño de sillar_e2e; la BD backend y la e2e son
 * deliberadamente distintas.
 */

import { spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { tomarCerrojo } from './cerrojo.mjs';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ENV_FILE = path.join(RAIZ, '.env');
const ENV_EJEMPLO = path.join(RAIZ, '.env.example');

/**
 * Las pruebas que **se espera** que se salten, por su nombre completo.
 *
 * La puerta canónica exige cero omisiones: la base efímera se migra sin seeds
 * y cada prueba que necesita datos crea su propio caso y lo revierte. Un Skip
 * nuevo significa que la puerta dejó de comprobar algo y por eso es rojo.
 */
const OMITIDAS_ESPERADAS = [];

/**
 * **Autoprueba de la limpieza.**
 *
 *     SILLAR_VERIFY_FORCE_FAIL=1 node scripts/verificar.mjs
 *
 * Mecanismo de diagnóstico de la limpieza de la base efímera. Cuando
 * `SILLAR_VERIFY_FORCE_FAIL` es exactamente la cadena `"1"` (y nada más),
 * la puerta:
 *
 *   1. crea la base efímera normalmente;
 *   2. aplica las migraciones normalmente;
 *   3. justo ANTES de ejecutar `dotnet test`, provoca un fallo deliberado
 *      de la etapa;
 *   4. el `finally` ejecuta igualmente el `DROP DATABASE`;
 *   5. la puerta termina con código != 0.
 *
 * No altera tests ni código del producto para provocar el fallo: la etapa
 * de pruebas devuelve un código != 0 sin llegar a lanzar `dotnet test`.
 *
 * Sirve para comprobar que, si la etapa de pruebas falla a mitad, la base
 * efímera queda destruida y `sillar_dev` intacta. Después de verificar el
 * fallo, una ejecución normal (`node scripts/verificar.mjs`) debe volver
 * a funcionar.
 *
 * El valor debe ser **exactamente** `"1"`. Cualquier otro valor —`"true"`,
 * `"yes"`, `""`, o ausencia de la variable— no activa el fallo.
 */
const FORCE_FAIL = process.env.SILLAR_VERIFY_FORCE_FAIL === '1';

/** Colores solo si la salida es una terminal. */
const color = process.stdout.isTTY
  ? {
      rojo: (t) => `\x1b[31m${t}\x1b[0m`,
      verde: (t) => `\x1b[32m${t}\x1b[0m`,
      gris: (t) => `\x1b[90m${t}\x1b[0m`,
      amarillo: (t) => `\x1b[33m${t}\x1b[0m`,
    }
  : { rojo: (t) => t, verde: (t) => t, gris: (t) => t, amarillo: (t) => t };

/**
 * Ejecuta y devuelve código de salida y salida completa.
 *
 * **WSLENV y el interop WSL→Windows.** En WSL, `dotnet` suele ser un symlink a
 * `dotnet.exe` de Windows. El interop WSL solo reenvía al proceso Windows las
 * variables listadas en `WSLENV`. Sin esto, `SILLAR_VERIFY_DATABASE` y
 * `ConnectionStrings__Default` no llegan a `dotnet test` ni a `dotnet ef`, los
 * guards de los fixtures fallan (bien: FAIL, no Skip) y la puerta no prueba lo
 * que cree probar. En Linux nativo, `WSLENV` es ignorada: inofensiva.
 *
 * Se añaden las claves custom pasadas en `opciones.env` a `WSLENV` preservando
 * lo que ya hubiera. En WSLENV el `:` separa entradas; una variable sin flags
 * se pasa sin traducción. Los flags opcionales (`/p`, `/l`, `/u`, `/w`) solo
 * se respetan para entradas que ya existían.
 *
 * Las variables de `process.env` estándar (PATH, HOME…) cruzan por defecto o por
 * la configuración del sistema; las custom son las que hay que declarar.
 */
function construirEnv(extra = {}) {
  const envFinal = { ...process.env, ...extra };

  // Solo tiene sentido declarar WSLENV si hay claves custom que propagar.
  const claves = Object.keys(extra);
  if (claves.length === 0) {
    return envFinal;
  }

  const existente = (envFinal.WSLENV ?? '').split(':').filter((v) => v.length > 0);
  const nombresExistentes = new Set(existente.map((v) => v.split('/')[0]));
  const añadidas = claves.filter((k) => !nombresExistentes.has(k));

  if (añadidas.length > 0) {
    envFinal.WSLENV = [...existente, ...añadidas].join(':');
  }

  return envFinal;
}

/** Ejecuta y devuelve código de salida y salida completa. */
function correr(comando, args, opciones = {}) {
  const resultado = spawnSync(comando, args, {
    cwd: opciones.cwd ?? RAIZ,
    encoding: 'utf8',
    shell: process.platform === 'win32',
    env: construirEnv(opciones.env),
    input: opciones.input,
  });

  return {
    codigo: resultado.status ?? 1,
    salida: `${resultado.stdout ?? ''}${resultado.stderr ?? ''}`,
    fallóAlLanzar: resultado.error != null,
  };
}

/**
 * Termina nombrando la etapa. No basta con morir: hay que decir dónde.
 *
 * **No llama `process.exit` directamente.** Lanza una excepción para que el
 * `finally` de la puerta ejecute `destruirBase()` antes de terminar. Si se
 * usara `process.exit`, el `finally` no correría y la base efímera quedaría
 * huérfana —justo lo que la autoprueba `SILLAR_VERIFY_FORCE_FAIL` verifica.
 */
class FalloEtapa extends Error {
  constructor(etapa, motivo, detalle) {
    super(`${motivo}${detalle ? `\n${detalle.trim()}` : ''}`);
    this.etapa = etapa;
  }
}

function abortar(etapa, motivo, detalle) {
  // No imprime aquí: el `finally` es el único reportero, para no duplicar
  // FALLÓ y para que la limpieza corra antes del mensaje final.
  throw new FalloEtapa(etapa, motivo, detalle);
}

// --- .env ------------------------------------------------------------------

/**
 * Lee `.env` a mano, igual que `e2e/setup/env.ts`. Solo para obtener la
 * cadena de conexión y los valores del contenedor cuando el entorno del
 * proceso no los trae. **El entorno del proceso tiene prioridad sobre el
 * archivo**, igual que `DotEnv.Load()`.
 */
function leerEnv(file) {
  const result = {};
  if (!existsSync(file)) {
    return result;
  }
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

const envValues = leerEnv(ENV_FILE);

/** El entorno del proceso manda; .env rellena lo que falte. */
function env(key) {
  return process.env[key] ?? envValues[key] ?? '';
}

/**
 * El mensaje que se lee cuando falta la cadena de conexión, que es **lo primero
 * que le pasa a una worktree recién creada**: `.env` está en `.gitignore`
 * (`.gitignore:2`), así que no se hereda, y la puerta muere antes de la etapa 1
 * sin llegar a decir «FALLÓ en la etapa».
 *
 * Se escribe aquí y no en `docs/` a propósito. Un paso manual documentado se
 * paga cada vez que alguien estrena un árbol; una frase en el punto exacto del
 * fallo se paga una vez. El remedio ya existe versionado —`.env.example`, que
 * sí trae `ConnectionStrings__Default`—, y lo único que faltaba era que el
 * fallo lo nombrara.
 *
 * Distingue los dos casos porque piden cosas distintas: no hay archivo, o el
 * archivo está y le falta la clave.
 */
function faltaLaCadena() {
  const hayEjemplo = existsSync(ENV_EJEMPLO);

  const existe = existsSync(ENV_FILE);

  const cabecera = existe
    ? `${ENV_FILE} existe pero no define ConnectionStrings__Default.`
    : `No hay ${ENV_FILE}. No se hereda al crear una worktree: está en .gitignore:2.`;

  let remedio;
  if (!hayEjemplo) {
    remedio = `Y tampoco hay ${ENV_EJEMPLO}, que es de donde debería salir.`;
  } else if (existe) {
    // Copiar encima borraría lo que ya esté puesto: aquí falta una línea, no el archivo.
    remedio =
      'Añádele la línea que .env.example trae para esa clave. No copies el ejemplo\n' +
      'encima: se llevaría por delante lo que ya esté puesto.';
  } else {
    remedio = 'Cópialo de la plantilla versionada, que sí la trae:\n    cp .env.example .env';
  }

  return [
    cabecera,
    remedio,
    '',
    'Cópialo de .env.example y NO de otra worktree: el .env del vecino apunta a',
    'SU PostgreSQL, y esta puerta crearía su base efímera dentro de la instalación',
    'de ese otro árbol. Revisa COMPOSE_PROJECT_NAME, POSTGRES_PORT, el Port= de la',
    'cadena y Sillar__Node__Code, que son los que identifican a este árbol.',
  ].join('\n');
}

// --- Cadena de conexión y BD efímera ---------------------------------------

/**
 * Toma `ConnectionStrings__Default` del entorno (prioridad) o, si no existe,
 * del `.env` de la raíz. Solo sustituye `Database=...` por el nombre efímero.
 * No imprime la cadena completa: contiene contraseña.
 */
function cadenaEfímera(nombreBase) {
  const original = env('ConnectionStrings__Default');
  if (!original) {
    throw new Error(faltaLaCadena());
  }
  const reemplazada = original.replace(/Database=[^;]*/i, `Database=${nombreBase}`);
  if (reemplazada === original) {
    throw new Error('No se pudo sustituir Database= en la cadena de conexión');
  }
  return reemplazada;
}

/**
 * Nombre de la base efímera: `sillar_verify_<timestamp>_<pid>`.
 *
 * `verificar.mjs` es la **única autoridad** para el nombre de la base. El
 * timestamp (ms desde la época) permite al barrido inicial distinguir una
 * base huérfana antigua de una base de otra puerta potencialmente activa; el
 * pid desempata corridas iniciadas en el mismo milisegundo. Los fixtures de
 * CRM y CMS no reconstruyen este prefijo: leen `SILLAR_VERIFY_DATABASE` y
 * comprueban que `Database` en su cadena de conexión coincida exactamente.
 */
const NOMBRE_BASE = `sillar_verify_${Date.now()}_${process.pid}`;

/**
 * Se atrapa aquí para que lo que se lea sea el remedio y no la traza. Este
 * fallo ocurre **antes de la etapa 1**, así que no puede salir por el «FALLÓ en
 * la etapa: n» de abajo, y una traza de Node por encima y por debajo del texto
 * entierra justo la línea que dice qué hacer.
 */
let CADENA_EFÍMERA;
try {
  CADENA_EFÍMERA = cadenaEfímera(NOMBRE_BASE);
} catch (error) {
  console.error(`\nLa puerta no llegó a arrancar.\n\n${error.message}\n`);
  process.exit(1);
}

/** Usuario PostgreSQL del contenedor. */
const POSTGRES_USER = env('POSTGRES_USER') || 'postgres';

/**
 * Ejecuta SQL administrativo dentro del servicio `db`. La puerta completa ya
 * depende de Docker por Playwright, así que no introduce una segunda
 * dependencia (por ejemplo psycopg2) solo para crear/destruir bases. `-X`
 * ignora configuración local de psql, `ON_ERROR_STOP` convierte un error SQL
 * en código != 0 y `-At` deja una fila por línea para que el barrido sea
 * inequívoco. Nunca se imprime la cadena de conexión ni la contraseña.
 */
function psqlAdmin(sql) {
  return correr('docker', [
    'compose', 'exec', '-T', 'db',
    'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', POSTGRES_USER, '-d', 'postgres', '-At', '-c', sql,
  ]);
}

/** El nombre generado o barrido debe tener exactamente el formato permitido. */
function nombreBaseValido(nombre) {
  return /^sillar_verify_[0-9]+_[0-9]+$/.test(nombre);
}

/** Crea la base efímera, sin importar si existía. */
function crearBase() {
  if (!nombreBaseValido(NOMBRE_BASE)) {
    throw new Error(`Nombre de base efímera no permitido: ${NOMBRE_BASE}`);
  }
  const drop = psqlAdmin(`DROP DATABASE IF EXISTS "${NOMBRE_BASE}" WITH (FORCE);`);
  if (drop.codigo !== 0) {
    throw new Error(`No se pudo limpiar la base efímera anterior:\n${drop.salida}`);
  }
  const create = psqlAdmin(`CREATE DATABASE "${NOMBRE_BASE}";`);
  if (create.codigo !== 0) {
    throw new Error(`No se pudo crear la base efímera:\n${create.salida}`);
  }
}

/** Destruye la base efímera. Segura de llamar sobre nada. */
function destruirBase() {
  if (!nombreBaseValido(NOMBRE_BASE)) {
    return { codigo: 1, salida: `Nombre de base efímera no permitido: ${NOMBRE_BASE}`, fallóAlLanzar: false };
  }
  return psqlAdmin(`DROP DATABASE IF EXISTS "${NOMBRE_BASE}" WITH (FORCE);`);
}

/**
 * Barrido de bases huérfanas al arrancar.
 *
 * El `finally` limpia la ejecución normal o con fallo normal. Pero SIGKILL,
 * corte eléctrico o un timeout externo matan el proceso sin pasar por
 * `finally`, dejando una base `sillar_verify_*` huérfana. Este barrido
 * recupera esos residuos **antes** de la corrida actual.
 *
 * - Lista las bases cuyo nombre sigue exactamente `sillar_verify_<digitos>_<digitos>`.
 * - Interpreta el timestamp (primer grupo numérico).
 * - Elimina **solo** las que superan las 12 horas.
 *
 * Nunca toca una base reciente, aunque sea ajena: dos puertas podrían estar
 * corriendo a la vez y el TTL de 12 h es deliberadamente muy superior a una
 * ejecución normal. No hace `DROP` indiscriminado de `sillar_verify_*`.
 */
const TTL_BARRIDO_MS = 12 * 60 * 60 * 1000;

function barrerBasesHuerfanas() {
  // Lista (nombre) de bases que cumplen el formato exacto.
  const listado = psqlAdmin(
    "SELECT datname FROM pg_database "
    + "WHERE datname ~ '^sillar_verify_[0-9]+_[0-9]+$' ORDER BY datname;",
  );
  if (listado.codigo !== 0) {
    throw new Error(`No se pudo listar bases huérfanas:\n${listado.salida}`);
  }
  const nombres = listado.salida.split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l.length > 0 && l !== 'datname' && /^sillar_verify_[0-9]+_[0-9]+$/.test(l));

  const ahora = Date.now();
  const eliminadas = [];
  const conservadas = [];
  for (const nombre of nombres) {
    const partes = nombre.match(/^sillar_verify_([0-9]+)_[0-9]+$/);
    if (!partes || !nombreBaseValido(nombre)) {
      conservadas.push(`${nombre} (formato no reconocido)`);
      continue;
    }
    const timestamp = Number(partes[1]);
    if (Number.isNaN(timestamp)) {
      conservadas.push(`${nombre} (timestamp no numérico)`);
      continue;
    }
    if (ahora - timestamp <= TTL_BARRIDO_MS) {
      conservadas.push(`${nombre} (reciente, ${(Math.round((ahora - timestamp) / 1000))}s)`);
      continue;
    }
    const drop = psqlAdmin(`DROP DATABASE IF EXISTS "${nombre}" WITH (FORCE);`);
    if (drop.codigo !== 0) {
      conservadas.push(`${nombre} (DROP falló: ${drop.salida.trim()})`);
    } else {
      eliminadas.push(nombre);
    }
  }
  return { eliminadas, conservadas };
}

/** Aplica las migraciones de un módulo contra la base efímera. */
function migrar(proyecto) {
  return correr('dotnet', [
    'ef', 'database', 'update',
    '--project', `backend/${proyecto}`,
    '--startup-project', 'backend/Sillar.Api',
    '--no-build',
  ], {
    cwd: RAIZ,
    env: {
      ConnectionStrings__Default: CADENA_EFÍMERA,
      SILLAR_VERIFY_DATABASE: NOMBRE_BASE,
    },
  });
}

// --- Antes de empezar ------------------------------------------------------
//
// **Lo que la puerta necesita, comprobado por ella.** Dar por hecho que el
// entorno está levantado sería cambiar una cosa que hay que recordar por
// otra, que es el fallo que esto viene a cerrar. Y el mensaje nombra el
// servicio, el puerto y el comando: un fallo genérico de Playwright a los
// sesenta segundos manda a leer una traza y no dice nada.

// En modo autoprueba no se comprueba ningún entorno: se provoca el veredicto y
// se sale. Anunciarlo aquí haría creer que sí, y esa clase de mentira pequeña es
// justo la que este bloque entero viene a quitar.
// **El cerrojo se toma antes que nada, y antes que ningún anuncio.**
//
// Antes que nada de lo caro, para que la negativa llegue en el primer segundo
// y no después de compilar el backend. Y antes del anuncio de aquí abajo
// porque estuvo un rato después: la segunda puerta escribía «Comprobando el
// entorno...» y se negaba a continuación, afirmando haber comprobado algo que
// no comprobó. No costaba nada y no rompía nada; es exactamente la clase de
// mentira pequeña que el comentario de este mismo bloque dice venir a quitar.
//
// La autoprueba del veredicto no toma cerrojo: es un diagnóstico que no toca
// la máquina, y bloquear con él a quien esté corriendo la puerta de verdad
// sería absurdo.
const soltarCerrojo =
  process.env.SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO === '1'
    ? () => {}
    : tomarCerrojo({ raiz: RAIZ, color });

process.on('exit', () => soltarCerrojo());

if (process.env.SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO !== '1') {
  console.log(color.gris('Comprobando el entorno...'));
}

// --- Lo que la puerta se prepara a sí misma --------------------------------

/**
 * Añade `~/.dotnet/tools` al `PATH` del proceso si hace falta.
 *
 * `dotnet ef` se instala ahí como herramienta global y **ningún archivo de
 * perfil añade esa carpeta**, así que las etapas 4 y 6 morían a los veinte
 * segundos con «command not found» — un fallo que no se parece en nada a lo que
 * es. Durante un tiempo el remedio fue anteponer `env PATH=…` a mano en cada
 * invocación; era un paso manual de los que se olvidan, y se olvidaba.
 *
 * Solo afecta a este proceso y a lo que lance: no toca ningún perfil.
 */
function asegurarPathDeHerramientas() {
  const carpeta = path.join(os.homedir(), '.dotnet', 'tools');

  if (!existsSync(carpeta)) {
    return;
  }

  const actual = process.env.PATH ?? '';
  const partes = actual.split(path.delimiter);

  if (partes.includes(carpeta)) {
    return;
  }

  process.env.PATH = actual ? `${actual}${path.delimiter}${carpeta}` : carpeta;
  console.log(color.gris(`  ~/.dotnet/tools añadido al PATH de esta corrida.`));
}

/**
 * Impide que el equipo se suspenda durante la corrida, y **devuelve la función
 * que suelta el bloqueo**.
 *
 * Una corrida son unos veinte minutos sin que nadie toque el teclado, que es
 * justo lo que la gestión de energía entiende como inactividad. Si la máquina se
 * suspende a mitad, el WiFi se desautentica y la suite muere con
 * `net::ERR_NETWORK_CHANGED` en pruebas que no tienen nada que ver.
 *
 * **Hacen falta los dos inhibidores, y por motivos distintos.** En este equipo
 * los eventos de energía los gestiona KDE PowerDevil, que pide la suspensión sin
 * pasar por systemd: comprobado el 3 de septiembre de 2026, con el inhibidor de
 * systemd verificado en modo `block` y el equipo suspendiéndose igual. `systemd`
 * cubre el resto de escritorios y las sesiones sin KDE.
 *
 * **Por qué los toma la puerta y no se envuelve el comando desde fuera.** Porque
 * envolverlo estaba mal: `kde-inhibit` **no propaga el código de salida de su
 * hijo** —siempre devuelve 0—, así que la receta documentada
 * `kde-inhibit … node scripts/verificar.mjs` convertía cualquier rojo en un
 * verde para quien mirase `$?`. Comprobado el 5 de septiembre de 2026: la misma
 * puerta fallida devuelve 1 sin envoltorio, 1 bajo `systemd-inhibit` y **0** bajo
 * `kde-inhibit`. Tomándolos desde dentro, el código de salida vuelve a ser el de
 * la puerta.
 *
 * Nada de esto es obligatorio: si un binario no está —Windows, un Linux sin KDE—
 * se dice y se sigue. Un bloqueo que no se pudo tomar es un riesgo conocido, no
 * un motivo para no correr las pruebas.
 */
function tomarInhibidores() {
  if (process.platform !== 'linux') {
    return () => {};
  }

  const porQué = 'SILLAR: puerta canónica en curso';

  // `sleep` acotado y no `infinity`: si algún día un SIGKILL se lleva a la
  // puerta sin pasar por la liberación, lo que quede se muere solo en dos horas
  // en vez de quedarse en la sesión. Una corrida son veinte minutos.
  const ESPERA = ['sleep', '7200'];

  const candidatos = [
    ['systemd-inhibit', ['--what=sleep:idle', '--mode=block', `--why=${porQué}`, ...ESPERA]],
    ['kde-inhibit', ['--power', ...ESPERA]],
  ];

  const vivos = [];
  const ausentes = [];

  for (const [comando, args] of candidatos) {
    try {
      // `detached` le da al hijo su propio grupo de procesos, y ese es el punto:
      // estos comandos envuelven a un `sleep`, así que matar solo al hijo deja
      // al nieto huérfano y vivo. Comprobado — un `sleep` suelto por corrida.
      // Con el grupo, `kill(-pid)` se los lleva a los dos.
      const hijo = spawn(comando, args, { stdio: 'ignore', detached: true });
      // `error` en vez de comprobar antes: spawn falla asíncrono si no existe.
      hijo.on('error', () => {});
      if (hijo.pid === undefined) {
        ausentes.push(comando);
        continue;
      }
      // Que un inhibidor vivo no impida a Node terminar cuando la puerta acabe.
      hijo.unref();
      vivos.push(hijo);
    } catch {
      ausentes.push(comando);
    }
  }

  if (vivos.length > 0) {
    console.log(color.gris(`  Suspensión bloqueada durante la corrida (${vivos.length}/2 inhibidores).`));
  }

  if (ausentes.length > 0) {
    console.log(color.gris(`  Sin ${ausentes.join(' ni ')}: la corrida NO está protegida de la suspensión.`));
  }

  let soltado = false;

  return () => {
    if (soltado) {
      return;
    }
    soltado = true;
    for (const hijo of vivos) {
      try {
        process.kill(-hijo.pid, 'SIGTERM'); // el grupo entero, no solo el hijo
      } catch {
        try {
          hijo.kill();
        } catch {
          // Ya no estaba. Soltar un bloqueo que no existe no es un fallo.
        }
      }
    }
  };
}

// --- De quién es el rojo ---------------------------------------------------

/**
 * Momento en que arrancó la corrida. Se usa para preguntarle al diario del
 * sistema solo por la ventana de esta puerta y no por todo el día.
 */
const INICIO = new Date();

/**
 * Firmas de fallo que **no son del código**. Cada una está inventariada en
 * `docs/ENTORNO.md` con su causa y cómo se reconoce.
 */
/**
 * **La línea de resultado de Playwright: la prueba de que la suite corrió.**
 *
 * `  1 failed` / `  126 passed (18.7m)`. Si está, el stack se levantó, el
 * navegador arrancó y las specs se ejecutaron — pase lo que pase después en el
 * log. Ninguna firma que signifique «algo del stack no llegó a levantarse»
 * puede hablar por encima de esto.
 *
 * Va anclada a principio de línea con `m` a propósito: dentro de un mensaje de
 * error puede aparecer cualquier cosa que se le parezca, pero el reportero
 * `list` la escribe siempre en su propia línea.
 */
const LA_SUITE_CORRIO = /^\s*\d+ (passed|failed|flaky|skipped)\b/m;

const FIRMAS_DE_ENTORNO = [
  [/ERR_NETWORK_CHANGED/i, 'la red cambió durante la corrida (causa 3 o 4 de docs/ENTORNO.md)'],
  [/ERR_NETWORK_IO_SUSPENDED/i, 'la entrada/salida de red quedó suspendida'],
  [/ERR_INTERNET_DISCONNECTED/i, 'el equipo se quedó sin red'],
  [/Temporary failure in name resolution/i, 'el DNS dejó de resolver (causa 3 de docs/ENTORNO.md)'],
  [/Cannot connect to the Docker daemon|docker daemon is not running/i, 'Docker no estaba en pie'],
  [/no space left on device/i, 'el disco se llenó'],
  // Tres formas del mismo hecho, y hubo que añadir las dos últimas.
  //
  // La primera es la de Node y la del navegador: `ECONNREFUSED`, o el mensaje
  // con el puerto pegado detrás. La tercera y la cuarta son **las de .NET**, y
  // no las cazaba ninguna de las dos: Npgsql parte la información en dos
  // líneas —el destino en una, el motivo en la siguiente— y `.` no cruza saltos
  // de línea, así que `Connection refused .*5\d{4}` no coincidía nunca con
  // esto:
  //
  //     Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:55900
  //      ---> System.Net.Sockets.SocketException (111): Connection refused
  //
  // Es decir: la etapa de migraciones podía morir por un stack que no llegó a
  // levantarse y el veredicto se quedaba callado, que es exactamente lo que la
  // bitácora §4 llama una barrera que no se distingue de una que funciona.
  [/Connection refused .*5\d{4}|ECONNREFUSED/i, 'algo del stack no llegó a levantarse', LA_SUITE_CORRIO],
  [/Failed to connect to \S*:5\d{4}/i, 'algo del stack no llegó a levantarse (.NET no llegó a la base)', LA_SUITE_CORRIO],
  [/SocketException \(111\)/i, 'algo del stack no llegó a levantarse (.NET: conexión rechazada)', LA_SUITE_CORRIO],
  // El Vite del arnés arranca con --strictPort, así que este mensaje solo sale
  // cuando otro proceso ya tiene el puerto. Con la identidad e2e compartida
  // entre worktrees —hoy la comparten cuatro— es lo que ve el segundo frente
  // que lanza la puerta. Se añadió el 5 de septiembre de 2026, después de que
  // el veredicto callara ante exactamente este fallo pudiendo hablar.
  [/is already used, make sure that nothing is running on the port/i,
    'el puerto del Vite ya estaba ocupado: otra worktree está corriendo la suite (docs/ENTORNO.md, hallazgo 5)'],
];

/**
 * **Las sondas no devuelven `null`, y ése es el arreglo.**
 *
 * Cada una responde una de tres cosas, nunca dos:
 *
 *   `{ visto: … }`     encontró lo que busca
 *   `{ limpio: true }` miró y no había nada
 *   `{ ciego: '…' }`   **no pudo mirar**, y dice por qué
 *
 * **Por qué importa lo suficiente para cambiar la forma de todas.** Antes las
 * cuatro devolvían `null` en los dos últimos casos, así que el aparato que
 * existe para decir por qué la puerta está rota era, por construcción,
 * indistinguible de estar averiado. No es teórico: el fallo del `toISOString()`
 * —preguntarle al diario por una ventana cinco horas en el futuro— se escondió
 * exactamente ahí. `journalctl` devolvía código 0 con salida vacía, `!r.stdout`,
 * `return null`, silencio. La detección muerta y la detección «sin diario que
 * consultar» producían la misma nada, y estuvo así hasta que alguien comparó la
 * cadena generada con `date`.
 *
 * Callar sobre la máquina está bien. Callar sobre la propia incapacidad de
 * mirar, no.
 */

/** Miró y no había nada. */
const LIMPIO = { limpio: true };

/**
 * ¿Estaba la máquina saturada al fallar?
 *
 * **De dónde sale.** El 5 de septiembre de 2026, una corrida sobre un árbol
 * limpio dio 4 fallos de 126 con la misma forma —«la aplicación no llegó a
 * pintar en 15 s», tiempos agotados, y el proxy de Vite soltando `ECONNRESET`
 * contra la API—. No había nada roto: había otra puerta corriendo a la vez en
 * otra worktree y un par de compilaciones encima, con la carga por las nubes. Es
 * exactamente lo que el pendiente §8 llama «falso hallazgo por ruido de
 * máquina», y es el caso que la división a dos frentes va a producir a menudo.
 *
 * **Por qué esto y no la firma del error.** `ECONNRESET` y `socket hang up` los
 * produce igual una API que se cae por un defecto de verdad. Atribuirlos al
 * entorno por la firma daría veredictos falsos, y un veredicto falso es peor que
 * ninguno. La carga es un hecho medible y ajeno al código: se informa como lo
 * que es, un indicio fuerte, sin decidir por quien lee.
 *
 * Los dos argumentos existen para poder provocarla: ver `autoprobarVeredicto`.
 */
function cargaExcesiva(carga = os.loadavg()[0], nucleos = os.availableParallelism?.() ?? os.cpus().length) {
  if (nucleos === 0) {
    return { ciego: 'no se pudo saber cuántos núcleos tiene esta máquina' };
  }

  // Cero en Windows: ahí `loadavg` no está implementado y devuelve [0,0,0]. No
  // es «carga cero», es «no hay carga que leer», y son cosas distintas.
  if (carga === 0) {
    return { ciego: `${process.platform} no publica carga media, así que no se comprobó` };
  }

  return carga > nucleos * 1.5
    ? { visto: { carga: carga.toFixed(1), nucleos } }
    : LIMPIO;
}

/**
 * La hora en el formato que `journalctl --since` entiende: **hora local**.
 *
 * No es un detalle de estilo. `toISOString()` da UTC, y `journalctl` lee una
 * fecha sin zona como local: en un equipo a UTC-5 eso pide el diario desde cinco
 * horas en el futuro, no vuelve nada nunca, y la detección de suspensión —que es
 * el motivo de existir de todo esto— queda muerta sin que nada lo delate. Se
 * cazó comparando la cadena generada con `date` antes de fiarse de ella.
 */
function comoLoLeeJournalctl(fecha) {
  const dosCifras = (n) => String(n).padStart(2, '0');

  return (
    `${fecha.getFullYear()}-${dosCifras(fecha.getMonth() + 1)}-${dosCifras(fecha.getDate())} ` +
    `${dosCifras(fecha.getHours())}:${dosCifras(fecha.getMinutes())}:${dosCifras(fecha.getSeconds())}`
  );
}

/** Busca la marca de suspensión en un texto de diario. Pura, para poder provocarla. */
function buscarSuspension(textoDelDiario) {
  const linea = textoDelDiario
    .split('\n')
    .find((l) => /will sleep now|PrepareForSleep/i.test(l));

  return linea ? { visto: linea.trim() } : LIMPIO;
}

/**
 * ¿Se suspendió el equipo durante la corrida?
 *
 * Es la única de las cuatro causas ambientales que **sobrevive a la
 * protección**, y la que más caro sale confundir con un defecto: la suite queda
 * en rojo por pruebas que no tienen nada que ver. El diario del sistema lo dice
 * sin ambigüedad, así que se le pregunta en vez de deducirlo.
 *
 * Los tres caminos por los que puede no saberse se nombran uno a uno, y ninguno
 * se confunde con «no se suspendió».
 */
function huboSuspension() {
  if (process.platform !== 'linux') {
    return { ciego: `no hay diario de systemd que consultar en ${process.platform}` };
  }

  const desde = comoLoLeeJournalctl(INICIO);
  const r = spawnSync('journalctl', ['--since', desde, '--no-pager', '-o', 'cat'], {
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
  });

  if (r.error) {
    return { ciego: `no se pudo ejecutar journalctl (${r.error.code ?? r.error.message})` };
  }

  if (r.status !== 0) {
    return { ciego: `journalctl terminó con código ${r.status}` };
  }

  if (!r.stdout) {
    // **Éste es el que escondía el fallo de la zona horaria.** Ahora lo dice, y
    // dice además desde qué momento preguntó, que es el dato con el que se ve
    // que la ventana estaba mal.
    return { ciego: `el diario no devolvió nada para la ventana pedida (desde ${desde})` };
  }

  return buscarSuspension(r.stdout);
}

/**
 * Qué toca esta rama que la integración no tenga.
 *
 * **Para qué sirve saberlo.** Con un solo frente, «la puerta es el criterio»
 * bastaba: si está roja, es tuya. Con dos frentes en paralelo un rojo ajeno
 * bloquea a los dos, y cada frente paga el tiempo de las pruebas del otro sin
 * poder hacer nada. Distinguir «esto lo rompí yo» de «esto venía roto» es lo que
 * permite devolverlo en vez de investigarlo.
 *
 * **Contra `origin/main`, y no contra `main`.** El `main` local de una worktree
 * recién estrenada puede estar atrasado o no existir siquiera; con la referencia
 * equivocada, `merge-base` falla y el ámbito de rama dejaba de opinar sin decir
 * nada. Se prueba `origin/main` primero, `main` como reserva, y **se dice cuál
 * se usó**: comparar contra una referencia vieja da una lista de ficheros que
 * parece buena y no lo es.
 *
 * No decide nada por su cuenta: devuelve la lista y el veredicto la usa como
 * indicio, diciendo siempre que es un indicio.
 */
function ficherosDeLaRama() {
  const candidatas = ['origin/main', 'main'];
  let referencia = null;
  let base = null;

  for (const candidata of candidatas) {
    const r = spawnSync('git', ['merge-base', 'HEAD', candidata], { cwd: RAIZ, encoding: 'utf8' });

    if (r.status === 0 && r.stdout.trim()) {
      referencia = candidata;
      base = r.stdout.trim();
      break;
    }
  }

  if (base === null) {
    return { ciego: `no hay ${candidatas.join(' ni ')} contra el que comparar` };
  }

  const diff = spawnSync('git', ['diff', '--name-only', `${base}...HEAD`], {
    cwd: RAIZ,
    encoding: 'utf8',
  });

  if (diff.status !== 0) {
    return { ciego: `git diff contra ${referencia} terminó con código ${diff.status}` };
  }

  const sucios = spawnSync('git', ['status', '--porcelain'], { cwd: RAIZ, encoding: 'utf8' });

  if (sucios.status !== 0) {
    // Sin esto, un fallo de `git status` haría pasar por «no tocado» un cambio
    // sin commitear, que es justo el que más probablemente rompió la etapa.
    return { ciego: 'git status falló, así que no se puede ver lo que está sin commitear' };
  }

  const sinCommitear = sucios.stdout.split('\n').map((l) => l.slice(3).trim()).filter(Boolean);
  const ficheros = [...new Set([...diff.stdout.split('\n').filter(Boolean), ...sinCommitear])];

  return { visto: { ficheros, referencia } };
}

/**
 * Qué carpeta mira cada etapa. La `suite e2e` no está: la rompe cualquier cosa,
 * así que sobre ella no se puede afirmar «no lo tocaste tú» y no se afirma.
 */
/** Las sondas de verdad. `veredicto` las recibe para poder sustituirlas al provocarlas. */
const SONDAS_REALES = {
  suspension: huboSuspension,
  carga: cargaExcesiva,
  ficheros: ficherosDeLaRama,
};

const AMBITO_DE_ETAPA = {
  'tipos del frontend': ['frontend/'],
  'tipos del arnés e2e': ['e2e/'],
  'compilación del backend': ['backend/'],
  'migraciones backend (BD efímera)': ['backend/'],
  'pruebas del backend': ['backend/'],
};

/**
 * Escribe de quién parece ser el rojo, con lo que lo sustenta.
 *
 * **Dice siempre en qué se basa, y dice cuándo no sabe.** Un veredicto sin
 * evidencia sería peor que ninguno: haría que se dejara de mirar.
 */
function veredicto(etapa, mensaje, sondas = SONDAS_REALES) {
  const lineas = [];

  // Lo que no se pudo comprobar se acumula y se dice al final, siempre. Un
  // veredicto que no menciona sus puntos ciegos invita a creerle más de lo que
  // sabe, y ése es el fallo que este bloque viene a cerrar.
  const ciegos = [];
  const mirar = (nombre, sonda) => {
    const r = sonda();
    if (r.ciego) {
      ciegos.push(`${nombre}: ${r.ciego}`);
    }
    return r;
  };

  const cerrar = (cuerpo) => {
    if (ciegos.length > 0) {
      cuerpo.push('');
      cuerpo.push(color.gris(`Lo que NO se pudo comprobar (${ciegos.length}):`));
      for (const c of ciegos) cuerpo.push(color.gris(`  - ${c}`));
    }
    return cuerpo;
  };

  const suspension = mirar('suspensión', sondas.suspension);
  if (suspension.visto) {
    lineas.push(color.amarillo('ES DEL ENTORNO — el equipo se suspendió durante la corrida.'));
    lineas.push(`  ${suspension.visto}`);
    lineas.push('  No toques el código. Vuelve a lanzarla; docs/ENTORNO.md, hallazgo 4.');
    return cerrar(lineas);
  }

  const saturada = mirar('carga de la máquina', sondas.carga);
  if (saturada.visto && /no llegó a pintar|Test timeout|ECONNRESET|socket hang up|ECONNREFUSED/i.test(mensaje)) {
    lineas.push(color.amarillo('ES DEL ENTORNO (probable) — la máquina estaba saturada al fallar.'));
    lineas.push(`  Carga ${saturada.visto.carga} sobre ${saturada.visto.nucleos} núcleos, y el fallo es de los que`);
    lineas.push('  produce la falta de máquina: tiempos agotados y conexiones cortadas.');
    lineas.push('  Comprueba si hay otra puerta corriendo en otra worktree y repite en frío.');
    return cerrar(lineas);
  }

  for (const [patron, explicacion, desmentido] of FIRMAS_DE_ENTORNO) {
    // **Una firma habla solo si su desmentido calla.**
    //
    // El 6 de septiembre de 2026 el veredicto dijo, con seguridad, «algo del
    // stack no llegó a levantarse» sobre una corrida en la que el stack se
    // levantó y la suite corrió entera: los `ECONNREFUSED` eran ruido del
    // proxy de Vite en el log DESPUÉS de la línea de resultado de Playwright.
    // La prueba de que la firma era imposible estaba dentro del mismo texto
    // que la firma estaba leyendo.
    //
    // Es la tercera forma de lo que la bitácora §4 ya describe: una barrera
    // que calla, una que se detiene en falso, y ésta, que **habla con
    // seguridad y se equivoca**. Es la peor de las tres, porque las otras dos
    // te dejan mirar y ésta dice «no mires el código» justo cuando el código
    // es lo único que hay que mirar.
    //
    // El desmentido no es un caso especial de esta firma: es un campo de la
    // tupla, porque cualquier firma que afirme algo sobre el entorno puede
    // toparse con la prueba de que no ocurrió.
    if (desmentido && desmentido.test(mensaje)) {
      continue;
    }

    if (patron.test(mensaje)) {
      lineas.push(color.amarillo(`ES DEL ENTORNO (probable) — ${explicacion}.`));
      lineas.push(`  Coincide con ${patron}. Antes de mirar el código, mira docs/ENTORNO.md.`);
      return cerrar(lineas);
    }
  }

  const ambito = AMBITO_DE_ETAPA[etapa];

  if (ambito) {
    const rama = mirar('ámbito de la rama', sondas.ficheros);

    if (rama.visto) {
      const { ficheros, referencia } = rama.visto;
      const tocados = ficheros.filter((f) => ambito.some((raiz) => f.startsWith(raiz)));

      if (tocados.length === 0) {
        lineas.push(color.amarillo('NO PARECE TUYO — esta rama no toca nada de la etapa que falló.'));
        lineas.push(`  La etapa mira ${ambito.join(', ')} y la rama no cambia nada ahí, medido contra ${referencia}.`);
        lineas.push('  Venía de la integración o de otro frente: devuélvelo en vez de investigarlo.');
        lineas.push(`  Comprobar:  git diff --stat $(git merge-base HEAD ${referencia})...HEAD -- ${ambito.join(' ')}`);
        return cerrar(lineas);
      }

      lineas.push(color.gris(`Esta rama toca ${tocados.length} fichero(s) del ámbito de la etapa, contra ${referencia}:`));
      for (const f of tocados.slice(0, 8)) lineas.push(color.gris(`  - ${f}`));
      if (tocados.length > 8) lineas.push(color.gris(`  ... y ${tocados.length - 8} más`));
      return cerrar(lineas);
    }
  }

  if (etapa === 'suite e2e') {
    lineas.push(color.gris('Sin veredicto: a la suite e2e la rompe cualquier capa, así que no se'));
    lineas.push(color.gris('afirma de quién es. Abre e2e/test-results/<prueba>/error-context.md,'));
    lineas.push(color.gris('que trae el DOM del fallo — docs/ENTORNO.md, hallazgo 9.'));
    return cerrar(lineas);
  }

  lineas.push(color.gris('Sin veredicto: ninguna señal permite atribuirlo automáticamente.'));
  return cerrar(lineas);
}

/** Sonda de mentira que dice «miré y no había nada». */
const limpia = () => LIMPIO;

/** Sonda de mentira que dice «no pude mirar, y por esto». */
const ciega = (motivo) => () => ({ ciego: motivo });

/**
 * **Las entradas con las que se provoca cada rama del veredicto.**
 *
 * Viven aquí arriba y no dentro de la autoprueba porque las usan dos: el
 * comando de diagnóstico y la comprobación previa de la propia puerta. Una sola
 * lista, para que no puedan discrepar.
 */
const PROVOCACIONES = [
  {
    nombre: 'suspensión del equipo',
    etapa: 'suite e2e',
    mensaje: 'da igual',
    sondas: {
      // La función real de búsqueda, con un diario sintético: lo único que se
      // sustituye es de dónde sale el texto, no quién decide.
      suspension: () => buscarSuspension(
        'kernel: algo irrelevante\nsystemd-logind[1]: The system will sleep now!\nkernel: más ruido',
      ),
      carga: limpia,
      ficheros: limpia,
    },
    espera: 'ES DEL ENTORNO — el equipo se suspendió',
  },
  {
    nombre: 'máquina saturada',
    etapa: 'suite e2e',
    mensaje: 'Se navegó a «/admin» y la aplicación no llegó a pintar en 15 s.',
    sondas: {
      suspension: limpia,
      carga: () => cargaExcesiva(24, 8),
      ficheros: limpia,
    },
    espera: 'la máquina estaba saturada',
  },
  {
    // La provocación al revés: aquí se exige que el veredicto NO atribuya al
    // entorno. Es la única del conjunto que se comprueba por ausencia, y por
    // eso lleva `noEspera` en vez de `espera`.
    nombre: 'ECONNREFUSED de ruido, con la suite ya corrida',
    etapa: 'suite e2e',
    mensaje:
      'Terminó con código 1.\n'
      + '  1 failed\n'
      + '    [chromium] › tests/recorrido.spec.ts:28:1 › El recorrido de la demostración\n'
      + '  126 passed (18.7m)\n'
      + '[WebServer] AggregateError [ECONNREFUSED]:\n'
      + '    at internalConnectMultiple (node:net:1122:18)',
    sondas: { suspension: limpia, carga: limpia, ficheros: limpia },
    noEspera: 'algo del stack no llegó a levantarse',
    // Y lo que sí debe decir: que no lo sabe. «Sin veredicto» es la respuesta
    // correcta aquí, y es mejor que la falsa: deja mirar el código en vez de
    // mandar a mirar el entorno.
    espera: 'Sin veredicto',
  },
  {
    nombre: 'firma de .NET: Npgsql no llegó a la base',
    etapa: 'migraciones backend (BD efímera)',
    mensaje:
      'Npgsql.NpgsqlException (0x80004005): Failed to connect to 127.0.0.1:55900\n'
      + ' ---> System.Net.Sockets.SocketException (111): Connection refused',
    sondas: { suspension: limpia, carga: limpia, ficheros: limpia },
    espera: 'algo del stack no llegó a levantarse',
  },
  {
    nombre: 'firma de entorno conocida',
    etapa: 'suite e2e',
    mensaje: 'Failed to load resource: net::ERR_NETWORK_CHANGED',
    sondas: { suspension: limpia, carga: limpia, ficheros: limpia },
    espera: 'la red cambió durante la corrida',
  },
  {
    nombre: 'la rama no toca el ámbito',
    etapa: 'pruebas del backend',
    mensaje: 'Terminó con código 1.',
    sondas: {
      suspension: limpia,
      carga: limpia,
      ficheros: () => ({ visto: { ficheros: ['docs/ENTORNO.md'], referencia: 'origin/main' } }),
    },
    espera: 'NO PARECE TUYO',
  },
  {
    nombre: 'la rama sí toca el ámbito',
    etapa: 'pruebas del backend',
    mensaje: 'Terminó con código 1.',
    sondas: {
      suspension: limpia,
      carga: limpia,
      ficheros: () => ({ visto: { ficheros: ['backend/Sillar.Core/Data/CoreDbContext.cs'], referencia: 'main' } }),
    },
    espera: 'toca 1 fichero(s) del ámbito',
    // Y la segunda dirección de la misma detección: que **no** devuelva el rojo
    // a otro frente cuando la rama sí toca lo que falló. Es lo que separa una
    // barrera provocada de una vista disparar.
    noEspera: 'NO PARECE TUYO',
  },
  // ---------------------------------------------------------------------
  // **Las que se comprueban por ausencia.**
  //
  // Una barrera vista disparar está a medias: falta saber que no dispara
  // cuando no debe. Es la segunda dirección del hábito de provocar, y se
  // descubrió con la guarda de `.media-e2e`, que **paraba en falso** —leía
  // «no soy el dueño» como «lo creó docker como root»— y bloqueaba al frente
  // de al lado. Una barrera que para en falso es la misma enfermedad que una
  // que calla: en las dos, lo que dice no depende de lo que pasa.
  //
  // Y aquí cuesta más caro que en una guarda, porque el veredicto **manda a
  // mirar a otro sitio**. Atribuir al entorno un rojo del código hace perder
  // la tarde en `docs/ENTORNO.md`; devolverle a otro frente un rojo que es
  // suyo se la hace perder a él.
  // ---------------------------------------------------------------------
  {
    nombre: 'el diario nombra sleep pero nadie se suspendió',
    etapa: 'suite e2e',
    mensaje: 'Terminó con código 1.',
    sondas: {
      // La función real, con un diario sintético que habla de «sleep» todo el
      // rato: son los propios inhibidores de la puerta. Si la detección mirara
      // la palabra y no el suceso, aquí es donde se vería.
      suspension: () => buscarSuspension(
        'systemd-inhibit[900]: --what=sleep:idle --mode=block --why=SILLAR: puerta canónica en curso sleep 7200\n'
        + 'systemd-logind[1]: Delay lock acquired (sleep)\n'
        + 'kernel: nada que ver aquí',
      ),
      carga: limpia,
      ficheros: limpia,
    },
    noEspera: 'el equipo se suspendió',
    espera: 'Sin veredicto',
  },
  {
    nombre: 'la máquina no estaba saturada',
    etapa: 'suite e2e',
    mensaje: 'Se navegó a «/admin» y la aplicación no llegó a pintar en 15 s.',
    // Carga 2 sobre 8 núcleos: la función real, con números normales. El mismo
    // mensaje que la provocación de «máquina saturada», a propósito: lo único
    // que cambia es el hecho medido, que es como debe ser.
    sondas: { suspension: limpia, carga: () => cargaExcesiva(2, 8), ficheros: limpia },
    noEspera: 'la máquina estaba saturada',
    espera: 'Sin veredicto',
  },
  {
    nombre: 'un fallo de aserción no es del entorno',
    etapa: 'suite e2e',
    mensaje:
      'Error: expect(locator).toBeHidden() failed\n'
      + "Locator:  getByRole('dialog')\n"
      + 'Expected: hidden\nReceived: visible\nTimeout:  10000ms',
    sondas: { suspension: limpia, carga: limpia, ficheros: limpia },
    noEspera: 'ES DEL ENTORNO',
    espera: 'Sin veredicto',
  },
  {
    nombre: 'las tres sondas ciegas se declaran',
    etapa: 'pruebas del backend',
    mensaje: 'Terminó con código 1.',
    sondas: {
      suspension: ciega('el diario no devolvió nada para la ventana pedida'),
      carga: ciega('win32 no publica carga media'),
      ficheros: ciega('no hay origin/main ni main contra el que comparar'),
    },
    espera: 'Lo que NO se pudo comprobar (3)',
  },
  {
    nombre: 'colisión de puerto entre worktrees',
    etapa: 'suite e2e',
    mensaje:
      'Error: http://localhost:55173 is already used, make sure that nothing is running on the port/url',
    sondas: { suspension: limpia, carga: limpia, ficheros: limpia },
    espera: 'otra worktree está corriendo la suite',
  },
  {
    nombre: 'sin señal, lo dice en vez de callar',
    etapa: 'pruebas del backend',
    mensaje: 'Terminó con código 1.',
    sondas: {
      suspension: limpia,
      carga: limpia,
      ficheros: () => ({ visto: { ficheros: [], referencia: 'origin/main' } }),
    },
    espera: 'NO PARECE TUYO',
  },
];

/**
 * Provoca las ramas y devuelve cuáles callaron. **No hace entrada ni salida**:
 * las sondas son de mentira y el veredicto es una función pura sobre ellas. Por
 * eso puede correr dentro de la puerta sin coste ni dependencia del entorno.
 */
function provocarLasRamas() {
  return PROVOCACIONES.map((caso) => {
    const salida = veredicto(caso.etapa, caso.mensaje, caso.sondas)
      .join('\n')
      // Sin colores: comparar texto con secuencias de escape dentro es frágil.
      .replace(/\x1b\[[0-9;]*m/g, '');

    const dijoLoQueDebe = salida.includes(caso.espera);
    const calloLoQueDebe = caso.noEspera === undefined || !salida.includes(caso.noEspera);

    return { ...caso, salida, disparó: dijoLoQueDebe && calloLoQueDebe };
  });
}

/**
 * ======================= LA IDENTIDAD NO SE ESCRIBE =======================
 *
 * **De dónde sale esta comprobación, que es lo que la justifica.**
 *
 * Al derivar la identidad de la worktree se quitó el nombre de la base e2e de
 * `e2e/setup/docker.ts`, que era donde se recordaba haberlo visto. Quedaban
 * cuatro sitios más —los seeds en `migrate.ts`, tres consultas en
 * `zz-instalacion.spec.ts` y el `stack:down` de `package.json`— y las dos
 * puertas de la medición concurrente murieron en el mismo punto:
 *
 *     FATAL: database "sillar_e2e" does not exist
 *
 * con los contenedores ya levantados y con el nombre correcto. El fallo no fue
 * el literal: fue **buscar donde uno recuerda en vez de enumerar**, que es
 * exactamente lo que la bitácora §4 llama fiarse del que filtra. Escrito por
 * quien lo escribió y repetido por quien lo escribió.
 *
 * Una lección aprendida que no se convierte en comando se vuelve a aprender.
 * Esto es el comando.
 *
 * **Lo que mira y lo que no.** Solo el código que habla con los dos stacks
 * —`e2e/` y `scripts/`—, y solo literales **entrecomillados**: un nombre de
 * base escrito en prosa no rompe nada. Las líneas de comentario se saltan, y
 * por eso el propio comentario de arriba puede citar el error sin disparar la
 * barrera. `scripts/identidad.mjs` queda fuera porque es donde esos nombres se
 * construyen: es la única definición legítima.
 */

/** Un nombre de base o de proyecto de los que se derivan, escrito a mano. */
const IDENTIDAD_A_MANO = /(['"`])(sillar(?:_[a-z0-9]+)*_(?:e2e|dev))\1/;

/** Comentario de línea, de bloque, o de shell: no es código que se ejecute. */
const ES_COMENTARIO = /^\s*(\/\/|\/?\*|#)/;

/**
 * La decisión, pura, para poder provocarla sin tocar el disco.
 *
 * Devuelve las líneas ofensivas de un texto. Vacío significa limpio.
 */
function identidadEscritaAMano(texto) {
  const encontradas = [];

  texto.split('\n').forEach((linea, i) => {
    if (ES_COMENTARIO.test(linea)) {
      return;
    }

    const m = IDENTIDAD_A_MANO.exec(linea);

    if (m) {
      encontradas.push({ linea: i + 1, nombre: m[2], texto: linea.trim() });
    }
  });

  return encontradas;
}

/** Los ficheros que hablan con los stacks, enumerados por git y no por memoria. */
function ficherosQueTocanLaIdentidad() {
  const r = spawnSync('git', ['ls-files', 'e2e', 'scripts'], { cwd: RAIZ, encoding: 'utf8' });

  if (r.status !== 0) {
    return { ciego: `git ls-files terminó con código ${r.status}` };
  }

  return {
    visto: r.stdout
      .split('\n')
      .filter((f) => /\.(ts|mjs|js|json)$/.test(f))
      .filter((f) => f !== 'scripts/identidad.mjs'),
  };
}

/**
 * Aborta la puerta si alguien volvió a escribir a mano un nombre que se deriva.
 *
 * Se niega también si **no puede mirar**, por la misma razón que el cerrojo: una
 * comprobación que no pudo hacerse no es una comprobación que salió bien.
 */
function comprobarQueNadieEscribeLaIdentidad() {
  const ficheros = ficherosQueTocanLaIdentidad();

  if (ficheros.ciego) {
    console.error(`\n${color.rojo('La puerta no arranca')}: no se pudo enumerar el código.`);
    console.error(`  ${ficheros.ciego}\n`);
    process.exit(1);
  }

  const malos = [];

  for (const f of ficheros.visto) {
    let texto;
    try {
      texto = readFileSync(path.join(RAIZ, f), 'utf8');
    } catch {
      continue;
    }
    for (const hallazgo of identidadEscritaAMano(texto)) {
      malos.push({ fichero: f, ...hallazgo });
    }
  }

  if (malos.length === 0) {
    return;
  }

  console.error(`\n${color.rojo('La identidad de la worktree está escrita a mano')} en ${malos.length} sitio(s).\n`);
  console.error('  Estos nombres se derivan del directorio del árbol. Escritos a mano apuntan');
  console.error('  a la worktree de otro, o a una base que ya no existe con ese nombre:\n');

  for (const m of malos) {
    console.error(`  ${m.fichero}:${m.linea}  «${m.nombre}»`);
    console.error(color.gris(`      ${m.texto}`));
  }

  console.error('\n  Sale de e2e/setup/env.ts (DB_NAME, PROJECT_NAME) o de scripts/identidad.mjs.\n');
  process.exit(1);
}

/**
 * **Provoca cada barrera del veredicto y comprueba que dispara.**
 *
 *     SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO=1 node scripts/verificar.mjs
 *
 * No lanza la puerta: alimenta el veredicto con sondas de mentira, enseña lo que
 * escribe cada rama, y además llama a las sondas de verdad para ver que
 * contestan. Termina en 0 si todo dispara, en 1 si algo calla.
 *
 * **Por qué existe.** Tres veces en este proyecto una barrera escrita resultó no
 * poder disparar nunca: el inhibidor con la receta que se tragaba el código de
 * salida, la detección de suspensión preguntando al diario cinco horas en el
 * futuro, y las dos pruebas de `ReactivacionRedSocialTests` que exigían una base
 * que en su etapa no existía. Ninguna de las tres fallaba: las tres callaban.
 *
 * Una barrera que calla no se distingue de una barrera que funciona. La pregunta
 * que lo reconoce es «¿alguna vez la he visto decir que no?», y si la respuesta
 * es no, lo que se sabe de ella es que compila.
 *
 * **Y de acordarse ya no depende:** las provocaciones sintéticas corren también
 * dentro de la puerta, antes de la etapa 1 — ver `comprobarQueElVeredictoHabla`.
 * Este comando existe para lo que allí no cabe: enseñar lo que escribe cada rama
 * y ejercitar las sondas reales, que sí dependen de la máquina.
 */
function autoprobarVeredicto() {
  console.log('Provocando las barreras del veredicto, una a una.\n');

  const resultados = provocarLasRamas();
  let fallos = 0;

  for (const r of resultados) {
    if (!r.disparó) fallos += 1;

    // **El rótulo dice qué quedó demostrado, y son dos cosas distintas.**
    // Toda provocación comprueba lo que el veredicto DICE. Las que además
    // llevan `noEspera` comprueban lo que CALLA, que es la segunda dirección y
    // la que faltaba: una barrera vista solo disparar está a medias.
    const rotulo = r.disparó
      ? color.verde(r.noEspera === undefined ? 'DICE      ' : 'DICE Y CALLA')
      : color.rojo(r.noEspera === undefined ? 'NO LO DICE' : 'NO CALLA    ');

    console.log(`${rotulo}  ${r.nombre}`);
    console.log(color.gris(`          espera: «${r.espera}»`));
    console.log(color.gris(r.salida.split('\n').map((l) => `          ${l}`).join('\n')));
    console.log('');
  }

  // --- Y las sondas de verdad, tal cual responden en esta máquina --------
  //
  // Lo de arriba prueba que las ramas del veredicto disparan. Esto prueba que
  // las sondas reales **contestan**, en la forma de tres estados y sin `null`
  // — que es donde estaba el fallo original. No se afirma qué deben responder:
  // eso depende de la máquina. Se afirma que responden algo nombrable.
  //
  // **Esta mitad no entra en la puerta, y es a propósito.** Al arrancar, `INICIO`
  // tiene segundos, así que el diario devuelve vacío y la sonda de suspensión
  // responde «no pude» con toda la razón. Meterla en el preflight imprimiría esa
  // alarma en cada corrida sana, y una alarma que suena siempre se deja de leer:
  // la misma enfermedad que esto viene a curar, por el otro extremo.
  console.log(color.gris('Y lo que responden hoy las sondas de verdad:\n'));

  for (const [nombre, sonda] of Object.entries(SONDAS_REALES)) {
    let r;
    try {
      r = sonda();
    } catch (error) {
      console.log(`${color.rojo('LANZA  ')}  ${nombre}: ${error.message}`);
      fallos += 1;
      continue;
    }

    if (r === null || r === undefined) {
      console.log(`${color.rojo('NULL   ')}  ${nombre} — es justo el fallo que esto viene a cerrar`);
      fallos += 1;
      continue;
    }

    const forma = r.visto ? 'vio algo' : r.limpio ? 'miró y no había nada' : r.ciego ? `no pudo: ${r.ciego}` : 'FORMA DESCONOCIDA';

    if (forma === 'FORMA DESCONOCIDA') {
      console.log(`${color.rojo('RARO   ')}  ${nombre} — ${JSON.stringify(r).slice(0, 120)}`);
      fallos += 1;
      continue;
    }

    console.log(`${color.verde('CONTESTA')}  ${nombre}: ${forma}`);
  }

  console.log('');

  if (fallos > 0) {
    console.error(color.rojo(`${fallos} comprobación(es) NO pasaron.`));
    return 1;
  }

  // **La barrera de la identidad escrita a mano, en las dos direcciones.**
  // Es la que faltaba el día que los seeds murieron: no basta con que encuentre
  // el literal, hace falta que NO lo encuentre donde no lo hay — si no, la
  // puerta no arrancaría nunca y se acabaría quitando.
  console.log('\nY la identidad escrita a mano:\n');

  // **Las cadenas de prueba se componen, no se escriben.**
  //
  // Escritas enteras, este mismo fichero contendría los literales que la
  // barrera busca, y la barrera se dispararía a sí misma: el árbol limpio
  // salía en rojo. Se vio provocándola, no razonándola.
  //
  // La salida fácil era exceptuar `scripts/verificar.mjs` del barrido. **No se
  // hizo, y es lo que hay que retener:** este fichero habla con los dos stacks,
  // así que exceptuarlo abriría exactamente el agujero por el que entró el
  // fallo. Una barrera que necesita eximir su propio banco de pruebas está
  // diciendo que su banco de pruebas no se parece a lo que vigila.
  const E2E = `sillar_${'e2e'}`;
  const FX_DEV = `sillar_fx_${'dev'}`;

  const casosDeIdentidad = [
    ['lo encuentra en código', `  await composeExec('db', ['psql', '-d', '${E2E}']);`, true],
    ['lo encuentra con sufijo', `const base = '${FX_DEV}';`, true],
    ['NO lo encuentra en un comentario', ` * murió con: database "${E2E}" does not exist`, false],
    ['NO lo encuentra en prosa sin comillas', `  const nombre = base + suffix; // ${E2E} era el viejo`, false],
    ['NO lo encuentra en código limpio', "  await psqlArchivo('/scripts/modules/core/02_seed.sql');", false],
  ];

  for (const [nombre, linea, debeEncontrar] of casosDeIdentidad) {
    const hallazgos = identidadEscritaAMano(linea);
    const bien = (hallazgos.length > 0) === debeEncontrar;

    if (!bien) fallos += 1;

    console.log(
      `${bien ? color.verde(debeEncontrar ? 'LA VE     ' : 'NO LA VE  ') : color.rojo('MAL       ')}  ${nombre}`,
    );
  }

  if (fallos > 0) {
    console.error(`\n${color.rojo(`${fallos} comprobación(es) NO pasaron.`)}\n`);
    return 1;
  }

  const porAusencia = resultados.filter((r) => r.noEspera !== undefined).length;

  console.log(
    color.verde(
      `Las ${resultados.length} barreras dicen lo que deben —${porAusencia} de ellas callan además `
      + `lo que no deben— y las ${Object.keys(SONDAS_REALES).length} sondas reales contestan.`,
    ),
  );
  return 0;
}

/**
 * **Comprueba, antes de la etapa 1, que el veredicto todavía habla.**
 *
 * El veredicto se lee exactamente cuando algo ya va mal y alguien tiene prisa.
 * Roto en ese momento no es que no ayude: **engaña** — un «NO PARECE TUYO» mal
 * calculado manda a devolver un rojo que sí era tuyo. Por eso se comprueba, y
 * por eso corta la puerta en vez de avisar: un aviso en el preflight se va por
 * arriba de un registro de treinta minutos sin que nadie lo vea.
 *
 * **Y por eso va aquí y no al final.** Si la maquinaria del veredicto está rota,
 * conviene saberlo antes de gastar media hora, no después.
 *
 * **Coste, medido y no supuesto:** el comando entero tarda unos 110 ms, de los
 * que 49 son arranque de Node —que la puerta ya paga— y unos 25 la llamada a
 * `journalctl`, que aquí no se hace. Lo que queda son las provocaciones
 * sintéticas, que no hacen entrada ni salida.
 *
 * **Y sí, esto es la puerta comprobando su propio instrumental y no el
 * producto.** Es deliberado y no es nuevo: `OMITIDAS_ESPERADAS` comprueba la
 * contabilidad de la propia puerta, y `SILLAR_VERIFY_FORCE_FAIL` existe para
 * provocar su propia limpieza. El veredicto es maquinaria de la puerta igual que
 * esas dos. Dejarlo fuera lo convertiría en una barrera que solo dispara cuando
 * alguien se acuerda de invocarla, que es justo lo que `BITACORA.md` §4 nombra.
 */
function comprobarQueElVeredictoHabla() {
  const callaron = provocarLasRamas().filter((r) => !r.disparó);

  if (callaron.length === 0) {
    return;
  }

  console.error(`\n${color.rojo('FALLÓ')} en la etapa: veredicto`);
  console.error(`  ${callaron.length} de ${PROVOCACIONES.length} ramas del veredicto no dispararon al provocarlas.`);
  console.error('  La puerta no arranca: el aparato que dice de quién es un rojo está roto,');
  console.error('  y un veredicto roto engaña más de lo que cuesta un rojo.\n');

  for (const r of callaron) {
    console.error(`  - ${r.nombre} — esperaba «${r.espera}» y escribió:`);
    console.error(r.salida.split('\n').map((l) => `      ${l}`).join('\n'));
  }

  console.error('\n  Para verlas todas:  SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO=1 node scripts/verificar.mjs');
  process.exit(1);
}

if (process.env.SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO === '1') {
  process.exit(autoprobarVeredicto());
}

comprobarQueElVeredictoHabla();
comprobarQueNadieEscribeLaIdentidad();


asegurarPathDeHerramientas();
const soltarInhibidores = tomarInhibidores();
// Red de seguridad: si algo termina el proceso por un camino que no pasa por el
// `finally`, los `sleep infinity` no deben sobrevivir a la puerta.
process.on('exit', () => soltarInhibidores());

/**
 * **Y `exit` no basta, que es lo que faltaba aquí.**
 *
 * `process.on('exit')` **no corre cuando a un proceso lo mata una señal**. Sin
 * lo de abajo, un `Ctrl-C` sobre la puerta —que es lo más normal del mundo—
 * dejaba dos `sleep 7200` vivos bloqueando la suspensión del equipo durante
 * dos horas, cada vez, en silencio. Se vio el 7 de septiembre de 2026 al
 * provocar el cerrojo: se mató una corrida a lo bruto y otra por tiempo, y las
 * dos dejaron su par de inhibidores detrás.
 *
 * Es el mismo defecto que ya se arregló una vez —el `sleep` huérfano por
 * corrida— reaparecido por la otra puerta. Contra `SIGKILL` no hay nada que
 * hacer y no se finge que lo haya: para eso el cerrojo guarda el PID y se
 * repara solo. Contra las señales que sí se pueden atender, se atienden.
 */
for (const senal of ['SIGINT', 'SIGTERM', 'SIGHUP']) {
  process.on(senal, () => {
    soltarInhibidores();
    soltarCerrojo();
    // Salir con el convenio de siempre: 128 + número de señal, para que quien
    // lanzó la puerta desde un guion lea lo que le pasó y no un 0.
    process.exit(senal === 'SIGINT' ? 130 : senal === 'SIGTERM' ? 143 : 129);
  });
}

// --- Las etapas, de barata a cara -----------------------------------------

const etapas = [
  {
    nombre: 'tipos del frontend',
    correr: () => correr('pnpm', ['typecheck'], { cwd: path.join(RAIZ, 'frontend') }),
  },
  {
    nombre: 'tipos del arnés e2e',
    correr: () => correr('pnpm', ['typecheck'], { cwd: path.join(RAIZ, 'e2e') }),
  },
  {
    nombre: 'compilación del backend',
    correr: () => correr('dotnet', ['build', 'backend/Sillar.sln', '--nologo', '-v', 'q']),
  },
  {
    nombre: 'migraciones backend (BD efímera)',
    correr: () => {
      const modulos = [
        'Sillar.Core',
        'Sillar.Modules.Catalog',
        'Sillar.Modules.Cms',
        'Sillar.Modules.Crm',
      ];
      let salida = '';
      for (const modulo of modulos) {
        const r = migrar(modulo);
        salida += `\n--- ${modulo} ---\n${r.salida}`;
        if (r.codigo !== 0) {
          return { codigo: r.codigo, salida, fallóAlLanzar: r.fallóAlLanzar };
        }
      }
      return { codigo: 0, salida, fallóAlLanzar: false };
    },
  },
  {
    nombre: 'pruebas del backend',
    correr: () => {
      // Autoprueba de la limpieza: si SILLAR_VERIFY_FORCE_FAIL === "1", se
      // provoca un fallo deliberado ANTES de lanzar dotnet test. No altera
      // tests ni código del producto. El finally de la puerta ejecuta igual
      // el DROP DATABASE, y el proceso termina con código != 0.
      if (FORCE_FAIL) {
        console.log(color.rojo('  [FORCE_FAIL] Fallo deliberado solicitado antes de dotnet test.'));
        return { codigo: 1, salida: 'SILLAR_VERIFY_FORCE_FAIL=1: fallo deliberado para probar la limpieza de la base efímera.', fallóAlLanzar: false };
      }
      return correr('dotnet', [
        'test', 'backend/Sillar.sln', '--nologo', '--no-build',
        '--logger', 'console;verbosity=normal',
      ], {
        env: {
          ConnectionStrings__Default: CADENA_EFÍMERA,
          SILLAR_VERIFY_DATABASE: NOMBRE_BASE,
        },
      });
    },
    // Además de pasar, la puerta canónica exige cero pruebas omitidas.
    revisar: (salida) => {
      const encontradas = [
        ...new Set(
          [...salida.matchAll(/^\s*(?:Omitido|Omitida|Omitidos|Omitidas|Skipped)\s+(\S+)/gm)].map((m) => m[1]),
        ),
      ].sort();

      // `dotnet test` localiza el resumen. En español actual imprime
      // `omitido: N`; en inglés, `Skipped: N`. El conjunto de nombres es
      // útil cuando el runner los enumera, pero el conteo impide que una
      // localización distinta convierta una omisión real en verde silencioso.
      const conteosOmitidas = [
        ...salida.matchAll(/\b(?:omitido|omitida|omitidos|omitidas|skipped)\s*:\s*(\d+)/gi),
      ].map((m) => Number.parseInt(m[1], 10));
      const totalOmitidasReportadas = conteosOmitidas.reduce((total, n) => total + n, 0);

      const esperadas = [...OMITIDAS_ESPERADAS].sort();

      if (esperadas.length === 0 && totalOmitidasReportadas > 0) {
        return `El runner reportó ${totalOmitidasReportadas} prueba(s) omitida(s), pero OMITIDAS_ESPERADAS está vacío.\n`
          + 'La puerta canónica exige skipped = 0; corrige la precondición o haz la prueba autocontenida.';
      }

      if (encontradas.join('\n') === esperadas.join('\n')) {
        return null;
      }

      // **Los nombres, no el número.** Con el conjunto delante se resuelve en
      // diez segundos; con «no cuadra el número», en veinte minutos.
      const listar = (xs) => (xs.length === 0 ? '    (ninguna)' : xs.map((x) => `    ${x}`).join('\n'));

      return 'El conjunto de pruebas omitidas no es el declarado.\n\n'
        + `  Esperadas (${esperadas.length}):\n${listar(esperadas)}\n\n`
        + `  Encontradas (${encontradas.length}):\n${listar(encontradas)}\n\n`
        + '  La puerta canónica exige cero omisiones: corrige la precondición o haz la prueba autocontenida.\n'
        + '  No se aceptan skips para poner verde la puerta.';
    },
  },
  {
    nombre: 'suite e2e',
    correr: () => correr('pnpm', ['exec', 'playwright', 'test'], { cwd: path.join(RAIZ, 'e2e') }),
  },
];

let etapaFallida = null;
let errorOriginal = null;
let baseCreada = false;

try {
  // **Todo lo que toca el servidor vive dentro del try.** El `finally` es el
  // árbitro único: reporta una sola vez y solo después de intentar limpiar.

  // Docker es requisito real de la puerta completa: administra PostgreSQL y
  // Playwright levanta su propio stack. Se falla aquí con un mensaje útil en
  // vez de dejar que una etapa posterior explote de forma opaca.
  const dockerVivo = correr('docker', ['info']);
  if (dockerVivo.codigo !== 0) {
    abortar(
      'entorno',
      'Docker no responde.',
      'Arranca Docker Desktop (Windows/WSL) o el servicio docker (Linux) y vuelve a intentarlo.',
    );
  }

  const baseViva = correr('docker', [
    'compose', 'exec', '-T', 'db',
    'pg_isready', '-U', POSTGRES_USER, '-d', 'postgres',
  ]);

  if (baseViva.codigo !== 0) {
    abortar(
      'entorno',
      'El servicio PostgreSQL `db` no responde.',
      'Levántalo con:  docker compose up -d db\n\n'
        + 'La puerta crea su propia base efímera, pero necesita el servidor PostgreSQL.',
    );
  }

  console.log(color.gris(`Base backend efímera: ${NOMBRE_BASE}`));

  // Barrido de huérfanas: SIGKILL, corte eléctrico o timeout externo no pasan
  // por finally. Antes de crear la base de esta corrida se eliminan las que
  // superan las 12 h; las recientes se conservan por si otra puerta corre.
  let barrido;

  try {
    barrido = barrerBasesHuerfanas();
  } catch (error) {
    abortar('barrido de huérfanas', error.message);
  }

  if (barrido.eliminadas.length > 0) {
    console.log(color.gris(`Barrido: eliminadas ${barrido.eliminadas.length} base(s) huérfana(s) > 12h:`));
    for (const n of barrido.eliminadas) console.log(color.gris(`  - ${n}`));
  } else {
    console.log(color.gris('Barrido: sin bases huérfanas antiguas.'));
  }
  if (barrido.conservadas.length > 0) {
    console.log(color.gris(`Barrido: conservadas ${barrido.conservadas.length} (recientes/no aptas):`));
    for (const n of barrido.conservadas) console.log(color.gris(`  + ${n}`));
  }

  // Crear la base dentro del try: si queda a medias, el finally la intenta
  // destruir igualmente.
  try {
    crearBase();
    baseCreada = true;
  } catch (error) {
    abortar('base efímera', error.message);
  }

  for (const [indice, etapa] of etapas.entries()) {
    const cuantas = `${indice + 1}/${etapas.length}`;
    console.log(color.gris(`[${cuantas}] ${etapa.nombre}...`));

    const { codigo, salida, fallóAlLanzar } = etapa.correr();

    if (fallóAlLanzar) {
      etapaFallida = etapa.nombre;
      abortar(etapa.nombre, 'No se pudo lanzar el comando. ¿Faltan pnpm o el SDK de .NET?', salida);
    }

    if (codigo !== 0) {
      etapaFallida = etapa.nombre;
      abortar(etapa.nombre, `Terminó con código ${codigo}.`, salida);
    }

    const reparo = etapa.revisar?.(salida);

    if (reparo) {
      etapaFallida = etapa.nombre;
      abortar(etapa.nombre, reparo);
    }
  }

  console.log(`\n${color.verde('TODO EN VERDE')} — las ${etapas.length} etapas pasaron.`);
} catch (error) {
  errorOriginal = error;
  if (error instanceof FalloEtapa) {
    etapaFallida = error.etapa;
  }
} finally {
  soltarInhibidores();
  soltarCerrojo();

  // **La limpieza es incondicional cuando hubo base.** Incluso si migración,
  // pruebas o revisión de skips fallan, la base efímera se intenta eliminar
  // antes de reportar. Si nunca llegó a crearse (ping o barrido fallaron), no
  // hay nada que limpiar y se reporta el fallo original sin ruido extra.
  if (baseCreada) {
    const cleanup = destruirBase();

    if (cleanup.codigo !== 0) {
      const msg = `La limpieza de la base efímera ${NOMBRE_BASE} falló:\n${cleanup.salida}`;

      if (errorOriginal) {
        console.error(`\n${color.rojo('FALLÓ')} en la etapa: ${etapaFallida ?? '(desconocida)'}\n  ${errorOriginal.message}`);
        console.error(`\n${veredicto(etapaFallida, errorOriginal.message).join('\n')}`);
        console.error(`\n${color.rojo('ADEMÁS')} la limpieza falló:\n${msg}`);
      } else {
        console.error(`\n${color.rojo('FALLÓ')} limpieza: ${msg}`);
      }
      process.exit(1);
    }
  }

  if (errorOriginal) {
    console.error(`\n${color.rojo('FALLÓ')} en la etapa: ${etapaFallida ?? '(desconocida)'}\n  ${errorOriginal.message}`);
    console.error(`\n${veredicto(etapaFallida, errorOriginal.message).join('\n')}`);
    process.exit(1);
  }
}
