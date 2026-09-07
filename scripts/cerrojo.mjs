import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const SIN_COLOR = { rojo: (t) => t, verde: (t) => t, gris: (t) => t, amarillo: (t) => t };

/**
 * ============================ EL CERROJO ============================
 *
 * **Dos puertas a la vez en la misma máquina no se estorban: se destrozan.**
 * Comparten el demonio de Docker, los puertos y —hasta que la identidad se
 * derivó— la base efímera. El 5 de septiembre de 2026 dos frentes lanzaron con
 * dieciséis segundos de diferencia y el segundo murió con `is already used`;
 * la parte peligrosa, que el segundo destruyera el stack del primero a mitad
 * de suite, no llegó a ocurrir por esos segundos.
 *
 * La regla «lanzad de uno en uno» existía y se cumplía. El problema es otro:
 *
 * > **Una regla cuyo incumplimiento es invisible no protege, tranquiliza.**
 *
 * Quien la incumple no se entera, y quien la respeta tampoco puede comprobar
 * que el otro la respeta. Así que deja de ser una regla y pasa a ser un
 * comando: o pasa o no pasa.
 *
 * ---
 *
 * **Por qué guarda PID y hora de arranque, y no una marca de «ocupado».**
 *
 * Un cerrojo que solo dice «ocupado» tiene un modo de fallo peor que no tener
 * cerrojo: la corrida que lo tomó muere de un `SIGKILL` —o de que se apague la
 * máquina, que aquí ha pasado— y no ejecuta ningún `finally`, así que el
 * archivo se queda. A partir de ahí la puerta no arranca nunca más y nadie
 * sabe por qué. El remedio sería borrarlo a mano, es decir, enseñar a la gente
 * a saltarse el cerrojo, que es la forma más rápida de que un cerrojo deje de
 * servir.
 *
 * Con el PID dentro se puede **preguntar si aquel proceso sigue vivo**. Si no
 * lo está, el cerrojo es basura y se rompe diciéndolo. La hora de arranque va
 * porque los PID se reutilizan: sin ella, un proceso cualquiera que herede el
 * número mantendría el cerrojo en pie para siempre.
 *
 * **Y si no se puede preguntar, no se rompe.** La sonda de vida contesta una
 * de tres cosas —vivo, muerto, o no pude mirar— igual que las del veredicto, y
 * por el mismo motivo: romper un cerrojo ajeno por no haber podido comprobarlo
 * es exactamente el fallo que el cerrojo existe para evitar.
 */

/**
 * Dónde vive el cerrojo.
 *
 * **Fuera del repositorio, a propósito.** Cada worktree tiene su propio árbol,
 * así que un archivo dentro no serviría de nada: lo que hay que serializar es
 * el uso de la *máquina*, no el de un directorio. Va al temporal del sistema,
 * con una huella del `.git` común —el que comparten todas las worktrees del
 * mismo repositorio— para que dos clones distintos no se bloqueen entre sí.
 */
export function rutaDelCerrojo(raiz) {
  const comun = spawnSync('git', ['rev-parse', '--path-format=absolute', '--git-common-dir'], {
    cwd: raiz,
    encoding: 'utf8',
  });

  const semilla = comun.status === 0 ? comun.stdout.trim() : raiz;
  const huella = createHash('sha1').update(semilla).digest('hex').slice(0, 12);

  return path.join(os.tmpdir(), `sillar-puerta-${huella}.lock`);
}


/**
 * La hora de arranque de un proceso, como cadena comparable, o `null` si en
 * esta plataforma no se puede saber.
 *
 * En Linux es el campo 22 de `/proc/<pid>/stat`, en ticks desde el arranque
 * del sistema. En Windows no hay equivalente barato: se devuelve `null` y el
 * cerrojo se queda solo con la prueba de vida, que allí basta para lo normal y
 * falla al lado seguro —no romper— en el caso raro de un PID reutilizado.
 *
 * El nombre del ejecutable puede llevar espacios y paréntesis, así que se
 * corta por el ÚLTIMO `)` y no por el primero: partir por espacios es el error
 * clásico al leer este archivo.
 */
export function arranqueDelProceso(pid) {
  try {
    const stat = readFileSync(`/proc/${pid}/stat`, 'utf8');
    const campos = stat.slice(stat.lastIndexOf(')') + 2).split(' ');
    return campos[19] ?? null;
  } catch {
    return null;
  }
}

/**
 * ¿Sigue vivo el proceso que tomó el cerrojo?
 *
 * Tres respuestas, nunca dos, por el mismo motivo que las sondas del
 * veredicto: «no pude mirar» no puede confundirse con «no está».
 *
 *   `{ vivo: true }`     está corriendo, y es el mismo de entonces
 *   `{ muerto: motivo }` no está, o el PID lo heredó otro proceso
 *   `{ ciego: motivo }`  no se pudo averiguar
 */
export function sondaDeVida(pid, arranqueGuardado) {
  if (!Number.isInteger(pid) || pid <= 0) {
    return { muerto: 'el cerrojo no trae un PID válido' };
  }

  try {
    process.kill(pid, 0);
  } catch (e) {
    if (e.code === 'ESRCH') {
      return { muerto: `el proceso ${pid} ya no existe` };
    }
    if (e.code === 'EPERM') {
      // Existe pero es de otro usuario. Está vivo; no es nuestro para juzgarlo.
      return { vivo: true };
    }
    return { ciego: `no se pudo preguntar por el proceso ${pid}: ${e.code ?? e.message}` };
  }

  const ahora = arranqueDelProceso(pid);

  if (arranqueGuardado && ahora && ahora !== arranqueGuardado) {
    return { muerto: `el PID ${pid} existe pero es otro proceso: arrancó después` };
  }

  return { vivo: true };
}

/**
 * **La decisión, separada de todo lo que la rodea para poder provocarla.**
 *
 * Recibe lo que había en el archivo y una sonda de vida, y devuelve qué hacer.
 * No lee, no escribe y no imprime: por eso se le pueden pasar los cuatro casos
 * en un segundo, que es lo que la convierte en una barrera puesta y no en una
 * barrera escrita.
 */
export function decidirSobreCerrojo(datos, sonda) {
  if (datos === null) {
    return { tomar: true };
  }

  if (typeof datos !== 'object' || typeof datos.pid !== 'number') {
    return { romper: 'el cerrojo estaba corrupto o incompleto' };
  }

  const vida = sonda(datos.pid, datos.arranque ?? null);

  if (vida.vivo) {
    return { rendirse: datos };
  }

  if (vida.ciego) {
    return { rendirse: datos, dudoso: vida.ciego };
  }

  return { romper: vida.muerto, datos };
}

/** «hace 3 min», para que el mensaje diga desde cuándo sin hacer restar a nadie. */
export function haceCuanto(desdeIso) {
  const ms = Date.now() - Date.parse(desdeIso);

  if (!Number.isFinite(ms) || ms < 0) {
    return 'hace un rato';
  }
  if (ms < 90_000) {
    return `hace ${Math.round(ms / 1000)} s`;
  }
  if (ms < 5_400_000) {
    return `hace ${Math.round(ms / 60_000)} min`;
  }
  return `hace ${(ms / 3_600_000).toFixed(1)} h`;
}

export function leerCerrojo(CERROJO) {
  try {
    return JSON.parse(readFileSync(CERROJO, 'utf8'));
  } catch (e) {
    return existsSync(CERROJO) ? { corrupto: true } : null;
  }
}

function escribirCerrojo(CERROJO, raiz) {
  writeFileSync(
    CERROJO,
    JSON.stringify({
      pid: process.pid,
      arranque: arranqueDelProceso(process.pid),
      worktree: raiz,
      desde: new Date().toISOString(),
      rama: ramaActual(raiz),
    }),
    { encoding: 'utf8', flag: 'wx' },
  );
}

function ramaActual(raiz) {
  const r = spawnSync('git', ['rev-parse', '--abbrev-ref', 'HEAD'], { cwd: raiz, encoding: 'utf8' });
  return r.status === 0 ? r.stdout.trim() : '(rama desconocida)';
}

/**
 * Toma el cerrojo o se niega a arrancar. Devuelve la función que lo suelta.
 *
 * El `flag: 'wx'` es lo que hace exclusiva la toma: crear-si-no-existe es una
 * sola operación del sistema de archivos, así que dos puertas lanzadas en el
 * mismo instante no pueden ganar las dos.
 */
export function tomarCerrojo({ raiz, color = SIN_COLOR, sonda = sondaDeVida } = {}) {
  const CERROJO = rutaDelCerrojo(raiz);

  for (let intento = 0; intento < 2; intento += 1) {
    const decision = decidirSobreCerrojo(leerCerrojo(CERROJO), sonda);

    if (decision.rendirse) {
      const d = decision.rendirse;
      console.error(`\n${color.rojo('La puerta ya está corriendo en esta máquina.')} No lanzo una segunda.\n`);
      console.error(`  La tiene el proceso ${d.pid}, desde ${d.worktree ?? '(worktree desconocida)'}`);
      console.error(`  en la rama ${d.rama ?? '(desconocida)'}, ${haceCuanto(d.desde)} (${d.desde}).\n`);

      if (decision.dudoso) {
        console.error(`  ${color.amarillo('Aviso:')} ${decision.dudoso}.`);
        console.error('  No se rompe un cerrojo que no se ha podido comprobar.\n');
      }

      console.error('  Dos corridas a la vez comparten Docker, puertos y carga: la segunda no');
      console.error('  falla sola, estropea a la primera. Espera a que termine.\n');
      console.error(`  Si estás seguro de que esa corrida ya no existe:  rm ${CERROJO}\n`);
      process.exit(1);
    }

    if (decision.romper) {
      const d = decision.datos;
      console.log(`${color.amarillo('Cerrojo huérfano roto:')} ${decision.romper}.`);
      if (d) {
        console.log(`  Era de ${d.worktree ?? '(worktree desconocida)'}, ${haceCuanto(d.desde)}.`);
      }
      console.log('  Sigo. Si la corrida anterior murió a lo bruto, mira que no dejara restos.');
      rmSync(CERROJO, { force: true });
    }

    try {
      escribirCerrojo(CERROJO, raiz);
    } catch (e) {
      if (e.code === 'EEXIST') {
        // Alguien lo tomó entre la lectura y la escritura. Se vuelve a decidir
        // una vez: la segunda vuelta ya lo encontrará vivo y se rendirá.
        continue;
      }
      throw e;
    }

    let soltado = false;
    return () => {
      if (soltado) return;
      soltado = true;

      // Solo se suelta el propio: si alguien rompió el nuestro por huérfano y
      // tomó el suyo, borrarlo aquí dejaría la máquina sin cerrojo.
      const actual = leerCerrojo(CERROJO);
      if (actual && actual.pid === process.pid) {
        rmSync(CERROJO, { force: true });
      }
    };
  }

  console.error('\nNo se pudo tomar el cerrojo: otra puerta lo gana cada vez. Reintenta.\n');
  process.exit(1);
}

/**
 * **La provocación, ejecutable.**
 *
 *     node scripts/cerrojo.mjs --provocar
 *
 * Recorre los cinco estados que el cerrojo puede encontrarse y enseña qué
 * decide en cada uno. No toca el cerrojo real: le pasa a la decisión los datos
 * y la sonda a mano, que es todo lo que necesita.
 *
 * Está aquí y no en la puerta porque una barrera que solo se puede provocar
 * lanzando lo que protege no se provoca nunca.
 */
function provocar() {
  const vivo = () => ({ vivo: true });
  const muerto = () => ({ muerto: 'el proceso 999999 ya no existe' });
  const reusado = () => ({ muerto: 'el PID 4242 existe pero es otro proceso: arrancó después' });
  const ciego = () => ({ ciego: 'no se pudo preguntar por el proceso 4242: EIO' });

  const datos = { pid: 4242, arranque: '77777', worktree: '/home/x/sillar-otra', desde: new Date(Date.now() - 400_000).toISOString(), rama: 'una/rama' };

  const casos = [
    ['no hay cerrojo', null, vivo],
    ['lo tiene una corrida viva', datos, vivo],
    ['el proceso ya no existe', datos, muerto],
    ['el PID se reutilizó', datos, reusado],
    ['no se pudo comprobar', datos, ciego],
    ['el archivo está corrupto', { corrupto: true }, vivo],
  ];

  let mal = 0;
  const esperado = ['tomar', 'rendirse', 'romper', 'romper', 'rendirse', 'romper'];

  casos.forEach(([nombre, d, sonda], i) => {
    const r = decidirSobreCerrojo(d, sonda);
    const decidio = r.tomar ? 'tomar' : r.rendirse ? 'rendirse' : 'romper';
    const bien = decidio === esperado[i];
    if (!bien) mal += 1;
    const detalle = r.romper ?? r.dudoso ?? (r.rendirse ? `lo tiene ${r.rendirse.pid}, ${haceCuanto(r.rendirse.desde)}` : '');
    console.log(`  ${bien ? '·' : '✗'} ${nombre.padEnd(28)} → ${decidio.padEnd(9)} ${detalle}`);
  });

  console.log(mal === 0 ? '\nLas seis vías deciden lo que deben.\n' : `\n${mal} vía(s) deciden mal.\n`);
  return mal === 0 ? 0 : 1;
}

if (process.argv.includes('--provocar')) {
  process.exit(provocar());
}
