#!/usr/bin/env node
/**
 * Muestreo SOLO en focales de Infra QA. No modifica la puerta canónica.
 *
 *   node infra-qa/focal.mjs m04
 *   node infra-qa/focal.mjs m05a
 *   node infra-qa/focal.mjs catalogo
 *
 * Produce mediciones agregadas en os.tmpdir() (respeta TMPDIR) y ejecuta un caso Playwright
 * con los mismos criterios del arnés: un worker, cero reintentos.
 */
import { spawn } from 'node:child_process';
import { appendFileSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const E2E_DIR = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ESCENARIOS = {
  m04: ['tests/zz-z-m04-ciclo.spec.ts'],
  m05a: ['tests/zz-z-m05a-ciclo.spec.ts'],
  catalogo: ['tests/catalogo.spec.ts', '--grep', 'Con M01 desactivado no queda entrada de menú'],
};

function leer(ruta) {
  try { return readFileSync(ruta, 'utf8'); } catch { return ''; }
}

function numeroMemoria(meminfo, campo) {
  const match = meminfo.match(new RegExp(`^${campo}:\\s+(\\d+)`, 'm'));
  return match ? Number(match[1]) : null;
}

/** RSS suma memoria compartida repetidamente; PSS la reparte entre procesos. */
function memoriaProceso(pid) {
  return {
    rssKiB: numeroMemoria(leer(`/proc/${pid}/status`), 'VmRSS'),
    pssKiB: numeroMemoria(leer(`/proc/${pid}/smaps_rollup`), 'Pss'),
  };
}

function acumularMemoria(agregado, memoria) {
  if (memoria.rssKiB !== null) {
    agregado.rssKiB += memoria.rssKiB;
    agregado.rssProcesos += 1;
  }
  if (memoria.pssKiB !== null) {
    agregado.pssKiB += memoria.pssKiB;
    agregado.pssProcesos += 1;
  }
}

function cerrarMemoria(agregado) {
  // Si no se leyó algún proceso, NULL, nunca un total engañoso parcial.
  if (agregado.rssProcesos !== agregado.procesos) agregado.rssKiB = null;
  if (agregado.pssProcesos !== agregado.procesos) agregado.pssKiB = null;
}

/** Emparejar ejecutables/entrypoints, no un texto arbitrario de la línea de comandos. */
function esVite(argv) {
  return argv.some((arg) => arg === 'vite' ||
    arg.endsWith('/node_modules/.bin/vite') ||
    /\/node_modules\/vite\/bin\/vite\.js$/.test(arg));
}

function limiteArchivos(pid) {
  const limits = leer(`/proc/${pid}/limits`);
  const fila = limits.split('\n').find((x) => x.startsWith('Max open files'));
  const match = fila?.match(/^Max open files\s+(\S+)\s+(\S+)/);
  return { soft: match?.[1] ?? null, hard: match?.[2] ?? null };
}

function contarFd(pid) {
  try { return readdirSync(`/proc/${pid}/fd`).length; }
  catch { return null; }
}

function esChromium(comm) {
  // Los crashpad son manejadores auxiliares, no procesos Chromium a medir.
  return !/crashpad/i.test(comm) &&
    /^(chrome|chromium|headless_shell|chrome-headless)/.test(comm);
}

function muestra() {
  const memoria = leer('/proc/meminfo');
  const psi = leer('/proc/pressure/memory').trim().split('\n');
  const chromium = {
    procesos: 0, renderers: 0, rssKiB: 0, pssKiB: 0,
    rssProcesos: 0, pssProcesos: 0, fdMax: 0,
    pidFdMax: null, fdRendererMax: 0, pidRendererMax: null,
    limiteFdSoft: null, limiteFdHard: null,
  };
  const vite = { procesos: 0, rssKiB: 0, pssKiB: 0, rssProcesos: 0, pssProcesos: 0, fdMax: 0 };

  let pids = [];
  try { pids = readdirSync('/proc').filter((id) => /^\d+$/.test(id)); }
  catch { /* Fuera de Linux: se conserva la memoria aunque falte /proc. */ }

  for (const pid of pids) {
    const comm = leer(`/proc/${pid}/comm`).trim();
    if (!esChromium(comm) && comm !== 'node') continue;

    if (comm === 'node') {
      // Solo se inspecciona el comando: nunca se serializa, ni se registra
      // una contraseña o un argumento de otro proceso.
      let args = [];
      try {
        args = readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0').filter(Boolean);
      } catch {}
      if (!esVite(args)) continue;
      vite.procesos += 1;
      acumularMemoria(vite, memoriaProceso(pid));
      vite.fdMax = Math.max(vite.fdMax, contarFd(pid) ?? 0);
      continue;
    }

    chromium.procesos += 1;
    acumularMemoria(chromium, memoriaProceso(pid));
    const fd = contarFd(pid);
    if (fd !== null && fd > chromium.fdMax) {
      chromium.fdMax = fd;
      chromium.pidFdMax = Number(pid);
      const lim = limiteArchivos(pid);
      chromium.limiteFdSoft = lim.soft;
      chromium.limiteFdHard = lim.hard;
    }

    let args = '';
    try { args = readFileSync(`/proc/${pid}/cmdline`, 'utf8'); } catch {}
    if (args.includes('--type=renderer')) {
      chromium.renderers += 1;
      if (fd !== null && fd > chromium.fdRendererMax) {
        chromium.fdRendererMax = fd;
        chromium.pidRendererMax = Number(pid);
      }
    }
  }

  cerrarMemoria(chromium);
  cerrarMemoria(vite);

  return {
    tipo: 'muestra', hora: new Date().toISOString(),
    memAvailableKiB: numeroMemoria(memoria, 'MemAvailable'),
    swapFreeKiB: numeroMemoria(memoria, 'SwapFree'),
    psiMemoriaSomeAvg10: psi[0]?.match(/avg10=(\d+(?:\.\d+)?)/)
      ? Number(psi[0].match(/avg10=(\d+(?:\.\d+)?)/)[1]) : null,
    psiMemoriaFullAvg10: psi[1]?.match(/avg10=(\d+(?:\.\d+)?)/)
      ? Number(psi[1].match(/avg10=(\d+(?:\.\d+)?)/)[1]) : null,
    chromium, vite,
  };
}

function uso() {
  console.log('Uso: node infra-qa/focal.mjs m04|m05a|catalogo');
  console.log('Solo se admite una focal por ejecución; no corre la canónica.');
}

if (process.argv.length !== 3 || !Object.hasOwn(ESCENARIOS, process.argv[2])) {
  uso();
  process.exitCode = 2;
} else {
  const escenario = process.argv[2];
  const carpeta = mkdtempSync(path.join(tmpdir(), `sillar-infra-qa-${escenario}-`));
  const ruta = path.join(carpeta, 'recursos.jsonl');
  const guardar = (obj) => appendFileSync(ruta, `${JSON.stringify(obj)}\n`, 'utf8');
  const args = [
    'exec', 'playwright', 'test', ...ESCENARIOS[escenario],
    '--project=chromium', '--workers=1', '--retries=0',
  ];
  const intervaloMs = 3000;

  writeFileSync(path.join(carpeta, 'CONTEXTO.txt'), [
    'SILLAR Infra QA — muestras del host; no solo del árbol E2E.',
    `Escenario=${escenario}`,
    `Iniciado=${new Date().toISOString()}`,
    'Código y aserciones del producto intactos. Cero reintentos.',
    `DirectorioEvidencia=${carpeta} (os.tmpdir; respeta TMPDIR)`,
    'Se miden procesos host-wide: Chromium y Vite pueden incluir procesos ajenos a SILLAR.',
    'Se excluye chrome_crashpad del conteo Chromium.',
    'rssKiB suma VmRSS y sobrecuenta memoria compartida: es una cota superior, NO prueba saturación.',
    'pssKiB reparte memoria compartida (smaps_rollup); null significa lectura parcial/no disponible.',
    'MemAvailable y PSI de /proc/pressure/memory fundamentan la clasificación de presión.',
    'El adjunto HTTP nace al desmontar el fixture: si muere el worker por OOM puede faltar.',
    'Ausencia de adjunto HTTP NO implica ausencia de errores; el JSONL sobrevive mientras viva este ejecutor.',
    'El informe no contiene líneas de comando, cookies ni consultas de URL.',
  ].join('\n') + '\n');

  let minimoMemKiB = null;
  let maxFd = 0;
  let maxFdRenderer = 0;
  let muestras = 0;
  function tomar() {
    try {
      const actual = muestra();
      guardar(actual);
      muestras += 1;
      if (actual.memAvailableKiB !== null) {
        minimoMemKiB = minimoMemKiB === null
          ? actual.memAvailableKiB : Math.min(minimoMemKiB, actual.memAvailableKiB);
      }
      maxFd = Math.max(maxFd, actual.chromium.fdMax);
      maxFdRenderer = Math.max(maxFdRenderer, actual.chromium.fdRendererMax);
    } catch (error) {
      guardar({ tipo: 'error-muestreo', hora: new Date().toISOString(), error: String(error) });
    }
  }

  console.log(`[infra-qa] focal=${escenario} · muestras=${ruta}`);
  guardar({ tipo: 'inicio', hora: new Date().toISOString(), escenario });
  tomar();
  const intervalo = setInterval(tomar, intervaloMs);
  intervalo.unref();

  const child = spawn('pnpm', args, {
    cwd: E2E_DIR, stdio: 'inherit',
    env: { ...process.env, SILLAR_INFRA_QA: '1' },
  });
  for (const sig of ['SIGINT', 'SIGTERM']) {
    process.on(sig, () => { if (!child.killed) child.kill(sig); });
  }
  child.on('error', (error) => {
    console.error(`[infra-qa] fallo al lanzar Playwright: ${error.message}`);
  });
  child.on('close', (codigo, senal) => {
    clearInterval(intervalo);
    tomar();
    const resultado = {
      tipo: 'fin', hora: new Date().toISOString(),
      exitCode: codigo, signal: senal,
      muestras, minimoMemKiB, maxFd, maxFdRenderer,
    };
    guardar(resultado);
    console.log(`[infra-qa] evidencia=${carpeta}`);
    console.log(`[infra-qa] resumen=${JSON.stringify(resultado)}`);
    process.exitCode = codigo ?? 1;
  });
}
