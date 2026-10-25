import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const E2E = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const TESTS = path.join(E2E, 'tests');

test('cada spec de ciclo fuerza un worker de navegador propio', async () => {
  const archivos = (await readdir(TESTS))
    .filter((archivo) => archivo.startsWith('zz-') && archivo.endsWith('.spec.ts'))
    .sort();

  assert.ok(archivos.length > 0, 'el detector no encontró specs de ciclo');

  for (const archivo of archivos) {
    const fuente = await readFile(path.join(TESTS, archivo), 'utf8');
    assert.match(
      fuente,
      /from ['"]\.\.\/fixtures\/ciclo\.js['"]/,
      `${archivo} debe usar el fixture que separa el proceso Chromium`,
    );
    assert.match(
      fuente,
      /test\.use\(\{ aislamientoDeNavegador: import\.meta\.url \}\);/,
      `${archivo} debe aportar una identidad de worker distinta`,
    );
  }
});

test('el aislamiento se implementa como opción worker-scoped', async () => {
  const fuente = await readFile(path.join(E2E, 'fixtures', 'ciclo.ts'), 'utf8');

  assert.match(fuente, /aislamientoDeNavegador:/);
  assert.match(fuente, /scope: ['"]worker['"]/);
  assert.match(fuente, /option: true/);
});
