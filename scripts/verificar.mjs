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
 * **Antes de lanzarla en Arch, leer `docs/ENTORNO.md`.** Son veinte minutos sin
 * que nadie toque el teclado, así que la máquina puede suspenderse a mitad y
 * dejar la suite en rojo por pruebas que no tienen nada que ver. Envolver con
 * `systemd-inhibit` **no basta**: en este equipo los eventos de energía los
 * gestiona KDE PowerDevil, que suspende sin pasar por ahí. La forma verificada
 * añade `kde-inhibit --power` delante, y `dotnet ef` necesita además que
 * `~/.dotnet/tools` esté en el PATH, que ningún perfil añade:
 *
 *     kde-inhibit --power systemd-inhibit --what=sleep:idle --why="SILLAR canonical gate" \
 *       env PATH="$PATH:$HOME/.dotnet/tools" node scripts/verificar.mjs
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

import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ENV_FILE = path.join(RAIZ, '.env');

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
  ? { rojo: (t) => `\x1b[31m${t}\x1b[0m`, verde: (t) => `\x1b[32m${t}\x1b[0m`, gris: (t) => `\x1b[90m${t}\x1b[0m` }
  : { rojo: (t) => t, verde: (t) => t, gris: (t) => t };

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

// --- Cadena de conexión y BD efímera ---------------------------------------

/**
 * Toma `ConnectionStrings__Default` del entorno (prioridad) o, si no existe,
 * del `.env` de la raíz. Solo sustituye `Database=...` por el nombre efímero.
 * No imprime la cadena completa: contiene contraseña.
 */
function cadenaEfímera(nombreBase) {
  const original = env('ConnectionStrings__Default');
  if (!original) {
    throw new Error('Falta ConnectionStrings__Default en el entorno o en .env');
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
const CADENA_EFÍMERA = cadenaEfímera(NOMBRE_BASE);

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

console.log(color.gris('Comprobando el entorno...'));

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
        console.error(`\n${color.rojo('ADEMÁS')} la limpieza falló:\n${msg}`);
      } else {
        console.error(`\n${color.rojo('FALLÓ')} limpieza: ${msg}`);
      }
      process.exit(1);
    }
  }

  if (errorOriginal) {
    console.error(`\n${color.rojo('FALLÓ')} en la etapa: ${etapaFallida ?? '(desconocida)'}\n  ${errorOriginal.message}`);
    process.exit(1);
  }
}
