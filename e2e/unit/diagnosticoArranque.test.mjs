import assert from 'node:assert/strict';
import test from 'node:test';

import {
  TITULO_ERROR_DE_PLATAFORMA,
  TITULO_MIGRACIONES,
  clasificarArranque,
} from '../fixtures/diagnosticoArranque.ts';

const base = {
  ruta: '/admin',
  indicadorDeCarga: false,
  tituloDeTarjeta: null,
  aviso: null,
  formularioDeInstalacion: false,
  raizVacia: false,
};

test('la página de error de plataforma no se confunde con un arranque colgado', () => {
  const d = clasificarArranque({ ...base, tituloDeTarjeta: TITULO_ERROR_DE_PLATAFORMA, aviso: '  No se pudo  consultar el estado. ' });
  assert.equal(d.estado, 'pagina-de-error');
  assert.equal(d.aviso, 'No se pudo consultar el estado.');
});

test('la pantalla de migraciones se reconoce por su título fijo', () => {
  assert.equal(clasificarArranque({ ...base, tituloDeTarjeta: TITULO_MIGRACIONES }).estado, 'migraciones-pendientes');
});

test('el asistente de instalación se reconoce por su formulario', () => {
  assert.equal(clasificarArranque({ ...base, formularioDeInstalacion: true }).estado, 'asistente-de-instalacion');
});

test('el indicador de carga sin nada más es un arranque sin terminar', () => {
  assert.equal(clasificarArranque({ ...base, indicadorDeCarga: true }).estado, 'arranque-sin-terminar');
});

test('una página de error pesa más que un indicador de carga residual', () => {
  const d = clasificarArranque({ ...base, indicadorDeCarga: true, tituloDeTarjeta: TITULO_ERROR_DE_PLATAFORMA });
  assert.equal(d.estado, 'pagina-de-error');
});

test('sin observación es un renderizador sin respuesta, con el motivo de la lectura', () => {
  const d = clasificarArranque(null, 'Target crashed');
  assert.equal(d.estado, 'renderizador-sin-respuesta');
  assert.equal(d.errorAlLeer, 'Target crashed');
  assert.equal(d.ruta, null);
});

test('una raíz vacía es «nada montado»; otra cosa pintada es «otro»', () => {
  assert.equal(clasificarArranque({ ...base, raizVacia: true }).estado, 'nada-montado');
  assert.equal(clasificarArranque({ ...base, tituloDeTarjeta: 'Otra tarjeta' }).estado, 'otro');
});

test('el aviso se recorta: no se copia texto largo al diagnóstico', () => {
  const d = clasificarArranque({ ...base, tituloDeTarjeta: TITULO_ERROR_DE_PLATAFORMA, aviso: 'x'.repeat(500) });
  assert.ok(d.aviso.length <= 201, `aviso de ${d.aviso.length} caracteres`);
});
