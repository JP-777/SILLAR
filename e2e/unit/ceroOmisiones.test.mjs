import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const require = createRequire(import.meta.url);
const e2e = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const paquete = require.resolve('@playwright/test');
const cli = path.join(path.dirname(paquete), 'cli.js');
const reporter = path.join(e2e, 'reporters/cero-omisiones.ts');

function correr(cuerpo) {
  const carpeta = mkdtempSync(path.join(tmpdir(), 'sillar-omisiones-'));
  try {
    writeFileSync(path.join(carpeta, 'control.spec.cjs'), `const {test}=require(${JSON.stringify(paquete)});\n${cuerpo}\n`);
    writeFileSync(path.join(carpeta, 'playwright.config.cjs'), `module.exports=${JSON.stringify({
      testDir: carpeta, workers: 1, retries: 0, reporter: [[reporter]],
    })};`);
    return spawnSync(process.execPath, [cli, 'test', '--config', path.join(carpeta, 'playwright.config.cjs')], {
      cwd: e2e, encoding: 'utf8', timeout: 60_000,
    });
  } finally {
    rmSync(carpeta, { recursive: true, force: true });
  }
}

test('la puerta E2E permite una prueba realmente ejecutada', () => {
  const r = correr("test('ejecutada', () => {});");
  assert.ifError(r.error);
  assert.equal(r.status, 0, r.stderr + r.stdout);
});

test('una única omisión vuelve roja la ejecución real de Playwright', () => {
  const r = correr("test('ejecutada', () => {}); test.skip('omitida', () => {});");
  assert.ifError(r.error);
  assert.equal(r.status, 1, r.stderr + r.stdout);
  assert.match(r.stderr + r.stdout, /cero omisiones: 1 omitidas de 2 pruebas/);
  assert.match(r.stderr + r.stdout, /Sin acreditar:.*omitida/);
});

test('la barrera de omisiones conserva un fallo ordinario', () => {
  const r = correr("test('fallida', () => { throw new Error('Fallo deliberado'); });");
  assert.ifError(r.error);
  assert.equal(r.status, 1, r.stderr + r.stdout);
});
