import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { asZonedInstant, wallClock } from '../src/shared/zonedDate.ts';
import { asDateTimeLocal, displayDate, toIsoOrNull } from '../src/modules/service-orders/format.ts';
import { formatDateTime, fromDateTimeLocal, toDateTimeLocal } from '../src/modules/tracking/logica/board.ts';

test('M05b y M06 convierten la hora del negocio con el reloj de Lima', () => {
  for (const convert of [toIsoOrNull, fromDateTimeLocal]) {
    assert.equal(convert('2026-11-26T23:45'), '2026-11-27T04:45:00.000Z');
    assert.equal(convert('2028-02-29T10:15:30.123'), '2028-02-29T15:15:30.123Z');
    assert.equal(convert(''), null);
  }
  for (const convert of [asDateTimeLocal, toDateTimeLocal]) {
    assert.equal(convert('2026-11-27T04:45:00Z'), '2026-11-26T23:45');
    assert.equal(convert(null), '');
    assert.equal(convert('no es una fecha'), '');
  }
});

test('una fecha imposible nunca se normaliza ni se envía como otro día', () => {
  for (const value of ['2026-02-29T10:00', '2026-02-30T10:00', '2026-11-26T24:00', '0000-01-01T00:00', '2026-11-26', '9999-12-31T23:59']) {
    assert.equal(toIsoOrNull(value), null, value);
    assert.throws(() => fromDateTimeLocal(value), RangeError, value);
  }
});

test('la conversión IANA rechaza saltos y horas ambiguas sin adivinar', () => {
  assert.throws(() => asZonedInstant('2026-03-08T02:30', 'America/New_York'), /no existe/);
  assert.throws(() => asZonedInstant('2026-11-01T01:30', 'America/New_York'), /se repite/);
  assert.equal(asZonedInstant('2026-03-08T03:30', 'America/New_York'), '2026-03-08T07:30:00.000Z');
  assert.equal(wallClock(Date.parse('2026-03-08T07:30:00Z'), 'America/New_York'), '2026-03-08T03:30:00');
});

test('la presentación distingue fecha ausente de fecha ilegible', () => {
  for (const show of [displayDate, formatDateTime]) {
    assert.equal(show(null), 'Sin fecha');
    assert.equal(show('incorrecta'), 'Fecha no disponible');
  }
});

test('un operador en Tokio y otro en UTC guardan y ven el mismo compromiso', () => {
  const cwd = fileURLToPath(new URL('..', import.meta.url));
  const code = `
    import { toIsoOrNull, asDateTimeLocal, displayDate } from './src/modules/service-orders/format.ts';
    import { fromDateTimeLocal, toDateTimeLocal, formatDateTime } from './src/modules/tracking/logica/board.ts';
    const instant = '2026-11-27T04:45:00Z';
    console.log(JSON.stringify([toIsoOrNull('2026-11-26T23:45'), fromDateTimeLocal('2026-11-26T23:45'),
      asDateTimeLocal(instant), toDateTimeLocal(instant), displayDate(instant), formatDateTime(instant)]));
  `;
  const outputs = ['UTC', 'Asia/Tokyo', 'America/Lima'].map(TZ => {
    const result = spawnSync(process.execPath, ['--input-type=module', '-e', code], { cwd, env: { ...process.env, TZ }, encoding: 'utf8' });
    assert.equal(result.status, 0, result.stderr);
    return JSON.parse(result.stdout);
  });
  assert.deepEqual(outputs[0], outputs[1]);
  assert.deepEqual(outputs[0], outputs[2]);
  assert.equal(outputs[0][0], '2026-11-27T04:45:00.000Z');
});
