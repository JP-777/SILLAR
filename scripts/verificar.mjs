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
 * no tiene por qué costar los diez minutos de la suite.
 *
 * Node y no bash ni PowerShell: el desarrollo alterna entre Windows y Arch
 * Linux (ADR-006), y esto tiene que servir en los dos.
 */

import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

/**
 * Las pruebas que **se espera** que se salten, por su nombre completo.
 *
 * **Se declara el conjunto, no el número.** Más, menos o distintas son las
 * tres un cambio, y las tres tienen que verse: una prueba que dejó de
 * saltarse cambió de condición, y enterarse de eso vale aunque sea buena
 * noticia.
 *
 * Hoy está vacío a propósito, y es un dato medido: hay doce `Assert.Skip` en
 * el árbol y **ninguno dispara** con la base levantada. Todos son la misma
 * salida —«no hay base contra la que comprobar»— y la puerta exige la base
 * antes de empezar, así que ninguno debería llegar a usarse.
 *
 * Si alguna vez hace falta añadir una, va con su porqué al lado. Una lista de
 * omitidas sin explicación es ruido tolerado; con ella es una declaración.
 */
const OMITIDAS_ESPERADAS = [];

/** Colores solo si la salida es una terminal. */
const color = process.stdout.isTTY
  ? { rojo: (t) => `\x1b[31m${t}\x1b[0m`, verde: (t) => `\x1b[32m${t}\x1b[0m`, gris: (t) => `\x1b[90m${t}\x1b[0m` }
  : { rojo: (t) => t, verde: (t) => t, gris: (t) => t };

/** Ejecuta y devuelve código de salida y salida completa. */
function correr(comando, args, opciones = {}) {
  const resultado = spawnSync(comando, args, {
    cwd: opciones.cwd ?? RAIZ,
    encoding: 'utf8',
    shell: process.platform === 'win32',
    env: { ...process.env, ...opciones.env },
  });

  return {
    codigo: resultado.status ?? 1,
    salida: `${resultado.stdout ?? ''}${resultado.stderr ?? ''}`,
    fallóAlLanzar: resultado.error != null,
  };
}

/** Termina nombrando la etapa. No basta con morir: hay que decir dónde. */
function abortar(etapa, motivo, detalle) {
  console.error(`\n${color.rojo('FALLÓ')} en la etapa: ${etapa}`);
  console.error(`  ${motivo}`);

  if (detalle) {
    console.error(`\n${detalle.trim()}`);
  }

  process.exit(1);
}

// --- Antes de empezar ------------------------------------------------------
//
// **Lo que la puerta necesita, comprobado por ella.** Dar por hecho que el
// entorno está levantado sería cambiar una cosa que hay que recordar por
// otra, que es el fallo que esto viene a cerrar. Y el mensaje nombra el
// servicio, el puerto y el comando: un fallo genérico de Playwright a los
// sesenta segundos manda a leer una traza y no dice nada.

console.log(color.gris('Comprobando el entorno...'));

if (correr('docker', ['info']).codigo !== 0) {
  abortar(
    'entorno',
    'Docker no responde. La suite e2e levanta su propio stack y las pruebas contra base lo necesitan.',
    'Arranca Docker Desktop (Windows) o el servicio docker (Linux) y vuelve a intentarlo.',
  );
}

const baseViva = correr('docker', [
  'compose', 'exec', '-T', 'db',
  'pg_isready', '-U', 'postgres', '-d', 'sillar_dev',
]);

if (baseViva.codigo !== 0) {
  abortar(
    'entorno',
    'La base de desarrollo no responde en el puerto 5432.',
    'Levántala con:  docker compose up -d db\n\n'
      + 'Sin ella, las pruebas que comprueban la traducción a SQL y los eventos **se saltan solas**,\n'
      + 'y la puerta se pondría verde sin haber comprobado nada de eso.',
  );
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
    nombre: 'pruebas del backend',
    correr: () => correr('dotnet', [
      'test', 'backend/Sillar.sln', '--nologo', '--no-build',
      '--logger', 'console;verbosity=normal',
    ]),
    // Además de pasar, ninguna puede haberse saltado sin declararlo.
    revisar: (salida) => {
      const encontradas = [
        ...new Set(
          [...salida.matchAll(/^\s*(?:Omitidas|Skipped)\s+(\S+)/gm)].map((m) => m[1]),
        ),
      ].sort();

      const esperadas = [...OMITIDAS_ESPERADAS].sort();

      if (encontradas.join('\n') === esperadas.join('\n')) {
        return null;
      }

      // **Los nombres, no el número.** Con el conjunto delante se resuelve en
      // diez segundos; con «no cuadra el número», en veinte minutos.
      const listar = (xs) => (xs.length === 0 ? '    (ninguna)' : xs.map((x) => `    ${x}`).join('\n'));

      return 'El conjunto de pruebas omitidas no es el declarado.\n\n'
        + `  Esperadas (${esperadas.length}):\n${listar(esperadas)}\n\n`
        + `  Encontradas (${encontradas.length}):\n${listar(encontradas)}\n\n`
        + '  Si el cambio es correcto, decláralo en OMITIDAS_ESPERADAS con su porqué.\n'
        + '  Que sean menos también falla: una prueba que dejó de saltarse cambió de condición.';
    },
  },
  {
    nombre: 'suite e2e',
    correr: () => correr('pnpm', ['exec', 'playwright', 'test'], { cwd: path.join(RAIZ, 'e2e') }),
  },
];

for (const [indice, etapa] of etapas.entries()) {
  const cuantas = `${indice + 1}/${etapas.length}`;
  console.log(color.gris(`[${cuantas}] ${etapa.nombre}...`));

  const { codigo, salida, fallóAlLanzar } = etapa.correr();

  if (fallóAlLanzar) {
    abortar(etapa.nombre, 'No se pudo lanzar el comando. ¿Faltan pnpm o el SDK de .NET?', salida);
  }

  if (codigo !== 0) {
    abortar(etapa.nombre, `Terminó con código ${codigo}.`, salida);
  }

  const reparo = etapa.revisar?.(salida);

  if (reparo) {
    abortar(etapa.nombre, reparo);
  }
}

console.log(`\n${color.verde('TODO EN VERDE')} — las ${etapas.length} etapas pasaron.`);
