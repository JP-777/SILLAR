import assert from 'node:assert/strict';
import test from 'node:test';

import { navegarUnaVezYEsperarVisibilidad } from '../fixtures/esperaSpa.ts';

test('la espera de la SPA navega una sola vez y termina después de la visibilidad', async () => {
  const pasos = [];
  let navegaciones = 0;

  await navegarUnaVezYEsperarVisibilidad(
    async () => {
      navegaciones += 1;
      pasos.push('navegación');
    },
    async () => {
      assert.equal(navegaciones, 1);
      await Promise.resolve();
      pasos.push('módulo visible');
    },
  );

  assert.equal(navegaciones, 1);
  assert.deepEqual(pasos, ['navegación', 'módulo visible']);
});

test('la espera de la SPA conserva el rechazo de la aserción de visibilidad', async () => {
  let navegaciones = 0;

  await assert.rejects(
    navegarUnaVezYEsperarVisibilidad(
      async () => {
        navegaciones += 1;
      },
      async () => {
        throw new Error('el módulo no llegó a ser visible');
      },
    ),
    /el módulo no llegó a ser visible/,
  );

  assert.equal(navegaciones, 1);
});
