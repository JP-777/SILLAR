import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import test from 'node:test';

/**
 * H03 y H22: describir un fallo sin afirmar una causa que no se conoce.
 *
 * **No se fija ninguna redacción.** Se exigen propiedades: que situaciones
 * distintas no se cuenten igual, que la explicación del servidor se use cuando
 * la hay, y que ninguna descripción atribuya el fallo a algo que no se sabe.
 */

// Como Vite: un import relativo sin extensión es el .ts del mismo nombre.
registerHooks({
  resolve(specifier, context, next) {
    if ((specifier.startsWith('./') || specifier.startsWith('../')) && !/\.[cm]?[jt]sx?$/.test(specifier)) {
      try { return next(`${specifier}.ts`, context); } catch { /* sigue con el nombre tal cual */ }
    }
    return next(specifier, context);
  },
});

const { ApiError } = await import('../src/shared/http/errors.ts');
const { describirFalloDeInstalacion, describirFalloDeArranque } = await import('../src/platform/fallos.ts');

// Formas reales. El 503 explicado es el del instalador cuando el destino no es seguro (H-27).
const explicado503 = new ApiError('Unexpected', 503,
  'No se instala en la base sillar_dev (localhost:5432): tiene cosas que no son de SILLAR.',
  'No se ha aplicado ninguna migración ni se ha modificado nada.', null, null, true);
const mudo503 = new ApiError('Unexpected', 503, 'No se pudo completar la operación (503).', null, null, null, false);
const red = new ApiError('Network', 0, 'No se pudo contactar con el servidor.');
const framework500 = new ApiError('Unexpected', 500, 'An error occurred while processing your request.', null, null, null, true);

// --- H03 -------------------------------------------------------------------

test('H03: un 503 explicado, un 503 sin explicación y un corte de red no comparten título', () => {
  const titulos = [explicado503, mudo503, red].map((e) => describirFalloDeInstalacion(e).titulo);
  assert.equal(new Set(titulos).size, 3, `títulos repetidos: ${JSON.stringify(titulos)}`);
});

test('H03: si el servidor explicó el 503, su título y su detalle son los del aviso', () => {
  const d = describirFalloDeInstalacion(explicado503);
  assert.equal(d.titulo, explicado503.message);
  assert.equal(d.mensaje, explicado503.detail);
});

test('H03: un 5xx sin explicación no se presenta como un fallo del instalador', () => {
  const instalador = describirFalloDeInstalacion(new ApiError('ValidationFailed', 400, 'Datos no válidos.', null, { a: ['x'] }, null, true)).titulo;
  assert.notEqual(describirFalloDeInstalacion(mudo503).titulo, instalador);
  assert.notEqual(describirFalloDeInstalacion(red).titulo, instalador);
});

test('H03: el 500 del framework no se enseña aunque traiga título (H-01)', () => {
  const d = describirFalloDeInstalacion(framework500);
  assert.ok(!`${d.titulo} ${d.mensaje}`.includes('An error occurred'));
});

// --- H22 -------------------------------------------------------------------

const texto = (d) => `${d.que} ${d.como ?? ''} ${d.pista ?? ''}`;

test('H22: un fallo del estado del servidor no se atribuye a los módulos', () => {
  for (const error of [red, mudo503, framework500]) {
    assert.ok(!texto(describirFalloDeArranque('estado', error)).includes('módulos'),
      `atribuido a los módulos: ${texto(describirFalloDeArranque('estado', error))}`);
  }
});

test('H22: ninguna descripción afirma que alguien activó un módulo', () => {
  for (const paso of ['estado', 'capacidades', 'sesion']) {
    for (const error of [red, mudo503, framework500, new Error('x')]) {
      assert.ok(!/activ(ar|aste|ado) un módulo/i.test(texto(describirFalloDeArranque(paso, error))));
    }
  }
});

test('H22: cada paso dice qué pregunta falló, y son distintas', () => {
  const ques = ['estado', 'capacidades', 'sesion'].map((p) => describirFalloDeArranque(p, red).que);
  assert.equal(new Set(ques).size, 3);
});

test('H22 (contraste): si fallan las capacidades, sí se dice que eran los módulos', () => {
  assert.ok(describirFalloDeArranque('capacidades', framework500).que.includes('módulos'));
});
