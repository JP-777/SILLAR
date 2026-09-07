#!/usr/bin/env node
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { cadenaDeConexion, identidadDeLaWorktree, sufijoDeWorktree } from './identidad.mjs';

/**
 * Escribe el `.env` de esta worktree, con su identidad ya calculada.
 *
 *     node scripts/estrenar.mjs
 *
 * **Por qué existe.** El stack e2e no necesita ningún archivo: el arnés le
 * pasa la identidad a compose por el entorno. El de desarrollo sí, porque lo
 * levanta una persona escribiendo `docker compose up -d`, y ahí no hay nadie
 * en medio que pueda calcular nada. La asimetría es esa y no otra.
 *
 * Pero que el valor acabe en un archivo no significa que lo teclee alguien.
 * Antes `.env.example` mandaba copiar y editar «cuatro claves»; eran seis, y
 * una de ellas era un puerto repetido dentro de una cadena de conexión. El 6
 * de septiembre de 2026 una worktree hizo exactamente lo que el documento
 * decía y se llevó por delante el stack de otra. No falló la persona: falló
 * pedirle a una persona que copiara seis valores sin equivocarse, cada vez.
 *
 * Aquí se copia el ejemplo entero —comentarios incluidos, que son buenos— y se
 * sustituyen las siete líneas que identifican al árbol. Lo único que queda por
 * rellenar a mano es lo que ninguna máquina puede inventar: las contraseñas.
 */

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const EJEMPLO = path.join(RAIZ, '.env.example');
const DESTINO = path.join(RAIZ, '.env');

const forzar = process.argv.includes('--forzar');

/**
 * Las claves que este guion decide, y nadie más.
 *
 * `PGADMIN_PORT` y `API_PORT` están aquí porque **también** colisionan entre
 * árboles y la lista vieja no las nombraba. Se descubrió al buscarlas: el
 * contenedor `sillar_api` de una worktree ya borrada seguía ocupando el 5080.
 */
function identidadComoClaves(dir, contrasena) {
  const { dev } = identidadDeLaWorktree(dir);

  return {
    COMPOSE_PROJECT_NAME: dev.proyecto,
    POSTGRES_DB: dev.base,
    POSTGRES_PORT: String(dev.puertoDb),
    API_PORT: String(dev.puertoApi),
    PGADMIN_PORT: String(dev.puertoPgadmin),
    Sillar__Node__Code: dev.nodo,
    ConnectionStrings__Default: cadenaDeConexion({
      puerto: dev.puertoDb,
      base: dev.base,
      usuario: 'postgres',
      contrasena,
    }),
  };
}

/**
 * Sustituye el valor de una clave conservando la línea donde estaba, y por
 * tanto el comentario que la explica. Si la clave no aparece, la añade al
 * final: así un `.env.example` que gane una clave nueva no la pierde por el
 * camino en silencio.
 */
function conValores(texto, valores) {
  let salida = texto;
  const puestas = new Set();

  salida = salida
    .split('\n')
    .map((linea) => {
      const m = /^([A-Za-z_][A-Za-z0-9_]*)=/.exec(linea);
      if (!m || !(m[1] in valores)) {
        return linea;
      }
      puestas.add(m[1]);
      return `${m[1]}=${valores[m[1]]}`;
    })
    .join('\n');

  const faltan = Object.keys(valores).filter((k) => !puestas.has(k));
  if (faltan.length > 0) {
    salida += `\n# Añadidas por scripts/estrenar.mjs: no estaban en .env.example.\n`;
    for (const k of faltan) salida += `${k}=${valores[k]}\n`;
  }

  return salida;
}

function principal() {
  if (!existsSync(EJEMPLO)) {
    console.error(`No encuentro ${EJEMPLO}.`);
    process.exitCode = 1;
    return;
  }

  if (existsSync(DESTINO) && !forzar) {
    console.error(
      `Ya hay un .env en este árbol y no lo piso.\n` +
        `  Si quieres regenerarlo con la identidad derivada:  node scripts/estrenar.mjs --forzar\n` +
        `  Vuelve a poner la contraseña a mano después: no se conserva.`,
    );
    process.exitCode = 1;
    return;
  }

  const ejemplo = readFileSync(EJEMPLO, 'utf8');

  // La contraseña que traiga el ejemplo se respeta tal cual: este guion decide
  // la identidad, no los secretos.
  const contrasena = /^POSTGRES_PASSWORD=(.*)$/m.exec(ejemplo)?.[1]?.trim() ?? '';

  const valores = identidadComoClaves(RAIZ, contrasena);
  writeFileSync(DESTINO, conValores(ejemplo, valores), 'utf8');

  const sufijo = sufijoDeWorktree(RAIZ);
  console.log(`\n.env escrito para ${RAIZ}`);
  console.log(`  sufijo «${sufijo || '(ninguno: es el árbol base)'}»\n`);
  for (const [k, v] of Object.entries(valores)) {
    console.log(`  ${k.padEnd(28)} ${k === 'ConnectionStrings__Default' ? v.replace(/Password=[^;]*/, 'Password=…') : v}`);
  }
  console.log(
    `\nFalta lo único que no se puede derivar: pon las contraseñas reales.\n` +
      `Para ver esta identidad otra vez, o comprobar que no choca con otro árbol:\n` +
      `  node scripts/identidad.mjs\n`,
  );
}

principal();
