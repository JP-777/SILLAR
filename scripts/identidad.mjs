import path from 'node:path';

/**
 * **La identidad de una worktree se deriva de su directorio. No se escribe.**
 *
 * De dónde sale esto. Durante semanas la identidad fueron cuatro claves que
 * `.env.example` mandaba editar a mano al estrenar un árbol, y el 6 de
 * septiembre de 2026 quedó claro que el planteamiento estaba mal de raíz, no
 * mal ejecutado. Dos señales, y la segunda es la que decide:
 *
 *  1. La lista decía «cuatro claves» y eran seis: `API_PORT` y `PGADMIN_PORT`
 *     colisionan igual entre árboles y no estaban nombradas. Una worktree que
 *     seguía la lista al pie de la letra seguía chocando.
 *
 *  2. **La clave que siempre se olvidaba no era una quinta clave: era el
 *     puerto otra vez**, duplicado dentro de `ConnectionStrings__Default`. Que
 *     fuese justo ésa la olvidada no era mala suerte. Era la señal de que no
 *     debía escribirse: un valor que aparece en dos sitios se olvida en uno.
 *
 * Y el caso que lo demostró sin que nadie fallara: `sillar-demo` copió
 * `.env.example` y arrancó, exactamente como el documento mandaba, y se llevó
 * por delante el stack de desarrollo de otro árbol. No hizo nada mal. Mientras
 * la identidad se escriba a mano, cada worktree nueva es una bomba.
 *
 * Aquí no se escribe ninguna: se calcula. El único dato de entrada es el
 * directorio del árbol, que ya es único por definición del sistema de
 * archivos.
 *
 * ---
 *
 * **El mapa de puertos es regular a propósito.** Antes no lo era —el
 * PostgreSQL de desarrollo en 55430 y el de e2e en 55432, dos de distancia— y
 * con dos de margen ningún desplazamiento derivado cabe sin solaparse. Cada
 * papel tiene ahora su bloque de cien, así que el offset de un árbol es el
 * mismo número en los seis puertos y se lee de un vistazo: si el e2e de este
 * árbol está en 55907, su API de desarrollo está en 55707.
 *
 * Cien caben porque el offset va de 0 a 99. El árbol base —el que se llama
 * `SILLAR` a secas— se queda con el 0, que es lo que hace que sus puertos se
 * puedan citar en la documentación sin mentir.
 */

const BLOQUES = {
  devDb: 55600,
  devApi: 55700,
  devPgadmin: 55800,
  e2eDb: 55900,
  e2eApi: 56000,
  e2eFrontend: 56100,
};

/** Cuántos árboles caben antes de que dos compartan offset. */
export const ARBOLES_POSIBLES = 100;

/**
 * El sufijo que distingue a este árbol, derivado del nombre de su directorio.
 *
 * `SILLAR` → `''` (el árbol base, sin sufijo)
 * `sillar-fx` → `'fx'`, `sillar-m02` → `'m02'`, `sillar-footer` → `'footer'`
 *
 * Se le quita el `sillar` de delante porque el prefijo lo pone después cada
 * nombre —`sillar_fx_e2e`, no `sillar_sillar-fx_e2e`—. Un directorio que no
 * empiece por `sillar` se usa entero: es un árbol igual de válido.
 */
export function sufijoDeWorktree(dir) {
  const base = path.basename(path.resolve(dir)).toLowerCase();
  const sinPrefijo = base.replace(/^sillar[-_. ]*/, '');
  return sinPrefijo.replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '');
}

/**
 * El desplazamiento de puertos, derivado del sufijo.
 *
 * FNV-1a de 32 bits porque hace falta que sea **estable entre máquinas y entre
 * versiones de Node**: `String.prototype.hashCode` no existe y cualquier hash
 * de la plataforma podría cambiar. Trece líneas propias valen más que esa
 * duda, porque un offset que cambiara de valor movería los puertos de un árbol
 * sin que nadie tocara nada.
 *
 * El sufijo vacío da 0 sin pasar por el hash: el árbol base se queda con los
 * puertos que la documentación cita.
 */
export function offsetDeSufijo(sufijo) {
  if (sufijo === '') {
    return 0;
  }

  let h = 0x811c9dc5;
  for (let i = 0; i < sufijo.length; i += 1) {
    h ^= sufijo.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }

  // 1..99: el 0 está reservado para el árbol base, así que un sufijo nunca lo
  // toma aunque su hash caiga ahí.
  return (h % (ARBOLES_POSIBLES - 1)) + 1;
}

/**
 * Todo lo que identifica a un árbol, calculado de su directorio.
 *
 * Devuelve los dos stacks juntos —desarrollo y e2e— porque son la misma
 * decisión tomada dos veces, y tenerlos en la misma tabla es lo que hace
 * evidente que no se pisan.
 */
export function identidadDeLaWorktree(dir) {
  const sufijo = sufijoDeWorktree(dir);
  const offset = offsetDeSufijo(sufijo);
  const conSufijo = (nombre) => (sufijo === '' ? nombre : `sillar_${sufijo}_${nombre.replace(/^sillar_?/, '')}`);

  return {
    dir: path.resolve(dir),
    sufijo,
    offset,

    dev: {
      proyecto: sufijo === '' ? 'sillar' : `sillar_${sufijo}`,
      base: conSufijo('sillar_dev'),
      nodo: sufijo === '' ? 'principal' : sufijo,
      puertoDb: BLOQUES.devDb + offset,
      puertoApi: BLOQUES.devApi + offset,
      puertoPgadmin: BLOQUES.devPgadmin + offset,
    },

    e2e: {
      proyecto: conSufijo('sillar_e2e'),
      base: conSufijo('sillar_e2e'),
      puertoDb: BLOQUES.e2eDb + offset,
      puertoApi: BLOQUES.e2eApi + offset,
      puertoFrontend: BLOQUES.e2eFrontend + offset,
    },
  };
}

/**
 * La cadena de conexión de desarrollo, compuesta a partir del puerto ya
 * resuelto.
 *
 * **Esta función es el arreglo entero.** El puerto no vuelve a escribirse: se
 * pasa. No hay ningún sitio donde pueda quedarse desactualizado respecto al
 * `POSTGRES_PORT` de al lado, porque no hay dos sitios.
 */
export function cadenaDeConexion({ puerto, base, usuario, contrasena }) {
  return `Host=localhost;Port=${puerto};Database=${base};Username=${usuario};Password=${contrasena}`;
}

/**
 * **Ejecutar este archivo enseña la tabla y busca choques.**
 *
 * Un offset sale de un hash, así que dos sufijos distintos pueden caer en el
 * mismo número. Con seis árboles y noventa y nueve ranuras pasa una de cada
 * siete veces, más o menos: poco, pero no nunca — y un choque de offset se
 * manifiesta como un `docker` que no enlaza el puerto, a mitad de una corrida,
 * lejos de su causa.
 *
 * Que sea improbable no basta. Lo que hace falta es que sea **mirable**:
 *
 *     node scripts/identidad.mjs
 *
 * enseña la identidad de este árbol y la de todos sus hermanos, y dice en la
 * cara si dos comparten offset. Es la comprobación que se hace al estrenar un
 * árbol, cuando arreglarlo cuesta cambiarle el nombre a un directorio vacío.
 */
async function principal() {
  const { execFileSync } = await import('node:child_process');

  let arboles = [];
  try {
    const salida = execFileSync('git', ['worktree', 'list', '--porcelain'], { encoding: 'utf8' });
    arboles = salida.split('\n').filter((l) => l.startsWith('worktree ')).map((l) => l.slice(9));
  } catch {
    arboles = [process.cwd()];
  }

  const filas = arboles.map((d) => identidadDeLaWorktree(d));
  const porOffset = new Map();
  for (const f of filas) {
    porOffset.set(f.offset, [...(porOffset.get(f.offset) ?? []), f]);
  }

  const aqui = identidadDeLaWorktree(process.cwd());
  console.log(`\nEste árbol: ${aqui.dir}`);
  console.log(`  sufijo «${aqui.sufijo || '(ninguno: es el árbol base)'}», offset ${aqui.offset}\n`);
  console.log('  desarrollo   db ' + aqui.dev.puertoDb + '   api ' + aqui.dev.puertoApi + '   pgadmin ' + aqui.dev.puertoPgadmin);
  console.log('  e2e          db ' + aqui.e2e.puertoDb + '   api ' + aqui.e2e.puertoApi + '   frontend ' + aqui.e2e.puertoFrontend);

  console.log('\nTodos los árboles de este repositorio:\n');
  for (const f of filas) {
    console.log(`  ${String(f.offset).padStart(3)}  ${(f.sufijo || '(base)').padEnd(12)} ${f.dir}`);
  }

  const choques = [...porOffset.values()].filter((g) => g.length > 1);
  if (choques.length === 0) {
    console.log('\nSin choques: los ' + filas.length + ' árboles tienen offsets distintos.\n');
    return;
  }

  console.error('\nCHOQUE DE OFFSET. Estos árboles comparten puertos y se destrozarán entre sí:\n');
  for (const grupo of choques) {
    console.error(`  offset ${grupo[0].offset}:`);
    for (const f of grupo) console.error(`    ${f.dir}`);
  }
  console.error('\nSe arregla renombrando el directorio de uno de ellos: el offset sale del nombre.\n');
  process.exitCode = 1;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  await principal();
}
