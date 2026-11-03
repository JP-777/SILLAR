/**
 * PostgreSQL temporal y exclusivo para las pruebas físicas de CommerceBridge (P1-POST-G6-R10).
 *
 * **Por qué existe.** La etapa de pruebas del backend entrega a cada proyecto la base efímera
 * `sillar_verify_*` del servicio `db`. Las fixtures físicas de CommerceBridge (R7, R8, R8-01, R9)
 * necesitan otra cosa, y la piden con su barrera R8-01 (`AislamientoPostgresTemporal.cs`): una
 * cadena **administrativa** completa (`Database=postgres`, puerto explícito) de un servidor cuya
 * identidad (`system_identifier`, `cluster_name`, contenedor, puerto) llegue por un canal
 * independiente de la conexión, en el archivo al que apunta `PG_TEMPORAL_IDENTIDAD`. Con la cadena
 * de la base efímera, esa barrera **falla** —no omite—, y así tiene que seguir.
 *
 * **Qué hace.** Crea un contenedor `postgres:16` nuevo, propio de esta corrida de la puerta:
 *   - nombre `sillar_verify_fisico_<timestamp>_<pid>` y etiqueta `sillar.verify.fisico=<timestamp>_<pid>`;
 *   - datos en tmpfs, publicado solo en `127.0.0.1`, puerto que elige Docker;
 *   - usuario y contraseña aleatorios, generados aquí y entregados por `--env-file` temporal (0600);
 *   - la misma colación ICU es-PE que el clúster del proyecto, y el registro de sentencias que las
 *     fixtures leen (`log_statement=all`, `log/r8a.log`, prefijo con aplicación y base).
 * La identidad se lee del contenedor recién creado por `docker exec` y `docker port`, y se escribe
 * **sin contraseña** en un archivo temporal 0600. La cadena solo vive en memoria y en el entorno del
 * `dotnet test` de ese proyecto: nunca se imprime ni se escribe en disco.
 *
 * **Qué no hace.** No toca el servicio `db`, ni `.env`, ni ninguna base o contenedor ajeno: solo
 * destruye un contenedor cuyo nombre **y** etiqueta coinciden con los que creó. El barrido de
 * huérfanos (SIGKILL) solo elimina contenedores con esta etiqueta y más de 12 h, como el de las
 * bases `sillar_verify_*`.
 */

import { spawnSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { chmodSync, mkdirSync, mkdtempSync, rmSync, writeFileSync, existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';

/** Etiqueta que marca un contenedor como propio de esta precondición. */
export const ETIQUETA = 'sillar.verify.fisico';

/** Imagen local; `--pull never`: la puerta no descarga nada por su cuenta. */
export const IMAGEN = 'postgres:16';

/** Mismo TTL que el barrido de bases efímeras de `verificar.mjs`. */
export const TTL_HUERFANOS_MS = 12 * 60 * 60 * 1000;

/** Nombre de contenedor válido: `sillar_verify_fisico_<ms>_<pid>`. */
export function nombreValido(nombre) {
  return /^sillar_verify_fisico_[0-9]+_[0-9]+$/.test(nombre);
}

/** Identificador de la corrida a partir del nombre. */
export function idDeNombre(nombre) {
  return nombreValido(nombre) ? nombre.slice('sillar_verify_fisico_'.length) : null;
}

/** Credencial alfanumérica: la barrera R8-01 rechaza comillas, blancos y `;` en los valores. */
export function credencial(longitud, generador = randomBytes) {
  const alfabeto = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
  let salida = '';
  while (salida.length < longitud) {
    for (const b of generador(longitud * 2)) {
      if (b < 248 && salida.length < longitud) salida += alfabeto[b % 62];
    }
  }
  return salida;
}

/** La cadena administrativa que exige la barrera R8-01: completa, con puerto explícito y `Database=postgres`. */
export function cadenaAdministrativa({ puerto, usuario, clave }) {
  if (!Number.isInteger(puerto) || puerto < 1024 || puerto > 65535 || puerto === 5432) {
    throw new Error(`Puerto no admitido para la instancia física: ${puerto}`);
  }
  for (const [campo, valor] of [['usuario', usuario], ['clave', clave]]) {
    if (!/^[A-Za-z0-9_]+$/.test(valor ?? '')) throw new Error(`El ${campo} de la instancia física no es alfanumérico.`);
  }
  return `Host=127.0.0.1;Port=${puerto};Database=postgres;Username=${usuario};Password=${clave};Timeout=5`;
}

/** Contenido del archivo de identidad, en el formato que lee `AislamientoPostgresTemporal.LeerIdentidad`. */
export function textoDeIdentidad({ contenedor, systemIdentifier, clusterName, puerto }) {
  if (!/^[0-9a-f]{64}$/.test(contenedor ?? '')) throw new Error('Id de contenedor no válido.');
  if (!/^[0-9]+$/.test(systemIdentifier ?? '')) throw new Error('system_identifier no numérico.');
  if (!/^[a-z0-9_]+$/.test(clusterName ?? '')) throw new Error('cluster_name no válido.');
  if (!Number.isInteger(puerto)) throw new Error('Puerto no numérico.');
  return `contenedor=${contenedor}\nsystem_identifier=${systemIdentifier}\ncluster_name=${clusterName}\npuerto=${puerto}\n`;
}

function docker(args, opciones = {}) {
  const r = spawnSync('docker', args, { encoding: 'utf8', ...opciones });
  return { codigo: r.status ?? 1, salida: `${r.stdout ?? ''}${r.stderr ?? ''}`, stdout: (r.stdout ?? '').trim(), error: r.error };
}

function esperar(ms) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

/** ¿Es este contenedor nuestro? Nombre exacto y etiqueta con el id que se deriva del nombre. */
function esPropio(nombre) {
  const id = idDeNombre(nombre);
  if (!id) return false;
  const r = docker(['inspect', nombre, '--format', `{{index .Config.Labels "${ETIQUETA}"}}`]);
  return r.codigo === 0 && r.stdout === id;
}

/**
 * Barrido de huérfanos: contenedores con la etiqueta y nombre válidos, de más de 12 h. Los
 * recientes se conservan: podrían ser de otra puerta en marcha.
 */
export function barrerHuerfanos(ahora = Date.now()) {
  const r = docker(['ps', '-a', '--filter', `label=${ETIQUETA}`, '--format', '{{.Names}}']);
  if (r.codigo !== 0) throw new Error(`No se pudo listar contenedores físicos huérfanos:\n${r.salida}`);
  const eliminados = [];
  const conservados = [];
  for (const nombre of r.stdout.split(/\r?\n/).map((l) => l.trim()).filter(Boolean)) {
    const id = idDeNombre(nombre);
    if (!id || !esPropio(nombre)) {
      conservados.push(`${nombre} (no reconocido)`);
      continue;
    }
    const ms = Number(id.split('_')[0]);
    if (ahora - ms <= TTL_HUERFANOS_MS) {
      conservados.push(`${nombre} (reciente)`);
      continue;
    }
    const rm = docker(['rm', '-f', nombre]);
    (rm.codigo === 0 ? eliminados : conservados).push(rm.codigo === 0 ? nombre : `${nombre} (rm falló)`);
  }
  return { eliminados, conservados };
}

/** Instancias vivas de este proceso, para que el `finally` de la puerta pueda destruirlas. */
const vivas = new Map();

/**
 * Crea la instancia y devuelve lo que necesita el `dotnet test` del proyecto físico. Si algo falla
 * a medias, destruye lo que alcanzó a crear y lanza: la etapa queda roja, nunca omitida.
 *
 * @param {{ ejecucion: string, evidencias: string }} opciones
 */
export function crearInstancia({ ejecucion, evidencias }) {
  const id = `${Date.now()}_${process.pid}`;
  const nombre = `sillar_verify_fisico_${id}`;
  const usuario = `fisico_${credencial(8).toLowerCase()}`.replace(/[^a-z0-9_]/g, 'x');
  const clave = credencial(32);
  const clusterName = `verify_fisico_${process.pid}`;
  const temporal = mkdtempSync(path.join(os.tmpdir(), 'sillar-verify-fisico-'));
  chmodSync(temporal, 0o700);
  const envFile = path.join(temporal, 'contenedor.env');
  const identidad = path.join(temporal, 'identidad.txt');
  mkdirSync(evidencias, { recursive: true });

  const instancia = { id, nombre, ejecucion, evidencias, temporal, identidad, destruida: false };
  vivas.set(nombre, instancia);
  try {
    if (docker(['image', 'inspect', IMAGEN]).codigo !== 0) {
      throw new Error(`No está la imagen local ${IMAGEN}; la puerta no la descarga (--pull never).`);
    }
    writeFileSync(envFile, `POSTGRES_USER=${usuario}\nPOSTGRES_PASSWORD=${clave}\nPOSTGRES_DB=postgres\n`, { mode: 0o600 });
    const run = docker([
      'run', '-d', '--name', nombre, '--label', `${ETIQUETA}=${id}`, '--label', `sillar.verify.ejecucion=${ejecucion}`,
      '--pull', 'never', '--tmpfs', '/var/lib/postgresql/data:rw,size=512m',
      '--env-file', envFile,
      '-e', 'POSTGRES_INITDB_ARGS=--locale-provider=icu --icu-locale=es-PE --encoding=UTF8 --locale=C.UTF-8',
      '-p', '127.0.0.1::5432', IMAGEN, 'postgres',
      '-c', `cluster_name=${clusterName}`, '-c', 'logging_collector=on', '-c', 'log_directory=log',
      '-c', 'log_filename=r8a.log', '-c', 'log_rotation_age=0', '-c', 'log_rotation_size=0',
      '-c', 'log_statement=all', '-c', 'log_line_prefix=app=%a|db=%d|',
    ]);
    rmSync(envFile, { force: true });
    if (run.codigo !== 0) throw new Error(`docker run falló:\n${run.salida}`);

    // Listo de verdad: el arranque definitivo (tras el init) y tres pg_isready seguidos.
    let listos = 0;
    for (let i = 0; i < 120 && listos < 3; i++) {
      const init = docker(['logs', nombre]).salida.includes('PostgreSQL init process complete');
      const ok = init && docker(['exec', nombre, 'pg_isready', '-U', usuario, '-d', 'postgres']).codigo === 0;
      listos = ok ? listos + 1 : 0;
      if (listos < 3) esperar(1000);
    }
    if (listos < 3) throw new Error('La instancia física no quedó lista en 120 s.');

    const leer = (sql) => docker(['exec', nombre, 'psql', '-X', '-U', usuario, '-d', 'postgres', '-Atc', sql]);
    const contenedor = docker(['inspect', nombre, '--format', '{{.Id}}']).stdout;
    const systemIdentifier = leer('select system_identifier from pg_control_system()').stdout;
    const clusterLeido = leer("select current_setting('cluster_name')").stdout;
    const publicado = docker(['port', nombre, '5432/tcp']).stdout.split(/\r?\n/).map((l) => l.trim()).filter((l) => /^127\.0\.0\.1:[0-9]+$/.test(l));
    if (publicado.length !== 1) throw new Error(`Publicación inesperada del puerto: ${JSON.stringify(publicado)}`);
    const puerto = Number(publicado[0].split(':')[1]);
    if (clusterLeido !== clusterName) throw new Error('cluster_name leído distinto del configurado.');
    if (!esPropio(nombre)) throw new Error('El contenedor creado no lleva la etiqueta esperada.');

    writeFileSync(identidad, textoDeIdentidad({ contenedor, systemIdentifier, clusterName, puerto }), { mode: 0o600 });
    const bases = leer("select string_agg(datname, ',' order by datname) from pg_database").stdout;
    const fdw = leer("select count(*) from pg_extension where extname in ('postgres_fdw','dblink')").stdout;
    instancia.puerto = puerto;
    escribir(evidencias, 'postgres-instancia.txt', [
      `ejecucion=${ejecucion}`,
      `creada=${new Date().toISOString()} contenedor=${nombre} id=${contenedor} imagen=${IMAGEN}`,
      `etiqueta=${ETIQUETA}=${id} publicado=127.0.0.1:${puerto} datos=tmpfs`,
      `system_identifier=${systemIdentifier} cluster_name=${clusterName}`,
      `usuario=${usuario} contraseña=(aleatoria, no se registra)`,
      `bases_al_crear=${bases} extensiones_fdw=${fdw}`,
    ]);

    return {
      nombre,
      puerto,
      env: {
        ConnectionStrings__Default: cadenaAdministrativa({ puerto, usuario, clave }),
        PG_TEMPORAL_IDENTIDAD: identidad,
        R5_SALIDA: path.join(evidencias, 'salida'),
        SILLAR_EVIDENCIA_EJECUCION: ejecucion,
      },
      destruir: () => destruirInstancia(nombre),
    };
  } catch (error) {
    rmSync(envFile, { force: true });
    const limpieza = destruirInstancia(nombre);
    throw new Error(`${error.message}\n${limpieza.resumen}`);
  }
}

function escribir(directorio, archivo, lineas) {
  writeFileSync(path.join(directorio, archivo), `${lineas.join('\n')}\n`, { flag: 'a' });
}

/**
 * Conserva el registro de sentencias y las bases que quedaban, y destruye el contenedor solo si es
 * propio. Idempotente. Devuelve el resumen y si quedó algo.
 */
export function destruirInstancia(nombre) {
  const instancia = vivas.get(nombre);
  if (!instancia || instancia.destruida) {
    return { codigo: 0, resumen: `instancia física ${nombre}: nada que destruir` };
  }

  const lineas = [];
  let codigo = 0;
  const existe = docker(['inspect', nombre]).codigo === 0;
  if (existe && !esPropio(nombre)) {
    codigo = 1;
    lineas.push(`NO destruida: ${nombre} no lleva la etiqueta ${ETIQUETA} esperada`);
  } else if (existe) {
    const bases = docker(['exec', nombre, 'sh', '-c',
      'psql -X -U "$POSTGRES_USER" -d postgres -Atc "select string_agg(datname, \',\' order by datname) from pg_database"']);
    lineas.push(`bases_antes_de_destruir=${bases.stdout || '(no se pudo leer)'}`);
    const registro = docker(['exec', nombre, 'cat', '/var/lib/postgresql/data/log/r8a.log'], { maxBuffer: 512 * 1024 * 1024 });
    if (registro.codigo === 0) {
      writeFileSync(path.join(instancia.evidencias, 'postgres-registro.log'), registro.stdout + '\n');
      lineas.push(`registro_de_sentencias_conservado=postgres-registro.log (${registro.stdout.length} caracteres)`);
    } else {
      lineas.push('registro_de_sentencias_conservado=NO');
    }
    const rm = docker(['rm', '-f', nombre]);
    if (rm.codigo !== 0) {
      codigo = 1;
      lineas.push(`docker rm falló: ${rm.salida.trim()}`);
    }
  }

  rmSync(instancia.temporal, { recursive: true, force: true });
  const queda = docker(['ps', '-a', '--filter', `name=^${nombre}$`, '-q']).stdout;
  const puerto = instancia.puerto ? spawnSync('ss', ['-ltn'], { encoding: 'utf8' }).stdout?.includes(`127.0.0.1:${instancia.puerto} `) : false;
  lineas.push(`destruida=${new Date().toISOString()} contenedor_existe=${queda ? 1 : 0} puerto_escuchando=${puerto ? 1 : 0} identidad_temporal_existe=${existsSync(instancia.temporal) ? 1 : 0}`);
  if (queda) codigo = 1;
  instancia.destruida = codigo === 0;
  escribir(instancia.evidencias, 'postgres-instancia.txt', lineas);
  return { codigo, resumen: lineas.map((l) => `instancia física ${nombre}: ${l}`).join('\n') };
}

/** Para el `finally` de la puerta: destruye lo que este proceso creó y siga vivo. */
export function destruirTodas() {
  return [...vivas.keys()].map((nombre) => destruirInstancia(nombre)).filter((r) => r.codigo !== 0 || !r.resumen.includes('nada que destruir'));
}
