import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  METODOS_DE_PAGO,
  accionesDeCotizacion,
  errorDePago,
  presentacionDeEstado,
  textoMayorista,
} from '../src/modules/b2b/logica/cotizacion.ts';
import { erroresDeLineas, lineaLibre, moverLinea, totalCobrado } from '../src/modules/b2b/logica/lineas.ts';
import { esFinal, estadosSiguientes } from '../src/modules/b2b/logica/solicitudes.ts';
import { b2bNavigation } from '../src/modules/b2b/navegacion.ts';

/**
 * Focales de M07 en el frontend (Paso 4, tramo 1). La lógica que decide qué
 * ve cada rol vive en `logica/` y se prueba aquí sin React; lo que solo existe
 * como JSX (menú, rutas) se afirma sobre el código fuente.
 */

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const fuente = (ruta) => readFileSync(resolve(frontend, ruta), 'utf8');
const modulo = resolve(frontend, 'src/modules/b2b');
const archivosDelModulo = (dir = modulo) =>
  readdirSync(dir).flatMap((n) => (statSync(join(dir, n)).isDirectory() ? archivosDelModulo(join(dir, n)) : [join(dir, n)]));

/** El código sin comentarios: las pruebas miran lo que se ejecuta, no lo que se explica. */
const codigo = (texto) => texto.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\{\/\*[\s\S]*?\*\/\}/g, '').replace(/^\s*\/\/.*$/gm, '');

const vigente = { status: 'enviada', invalidatedAt: null, isActive: true };
const cot = (status, extra = {}) => ({ status, invalidatedAt: null, isActive: true, ...extra });

// 1 · 2 — Navegación y rutas según la capacidad b2b

test('1 · con b2b inactivo no hay menú ni rutas: todo cuelga de la capacidad b2b', () => {
  assert.equal(b2bNavigation.moduleCode, 'b2b');
  const navegacion = fuente('src/layout/navigation.ts');
  assert.match(navegacion, /\bb2bNavigation,/);
  assert.match(navegacion, /MODULE_NAVIGATION\.filter\(\(entry\) => isActive\(entry\.moduleCode\)\)/);
  const rutas = fuente('src/app/routes.tsx');
  assert.match(rutas, /\{has\('b2b'\) && b2bRoutes\}/);
  assert.equal((rutas.match(/b2bRoutes/g) ?? []).length, 2, 'b2bRoutes se importa y se monta una sola vez, condicionado');
});

test('2 · con b2b activo, el grupo Solicitudes lleva a rutas que existen', () => {
  assert.equal(b2bNavigation.group, 'Solicitudes');
  const rutas = fuente('src/modules/b2b/routes.tsx');
  for (const item of b2bNavigation.items) {
    assert.equal(item.minimumRole, 'editor');
    assert.ok(rutas.includes(`path="${item.to.replace('/admin/', '')}"`), `${item.to} no está montada`);
  }
});

// 3 · 4 — Lo de admin no se enseña al editor

test('3 · el editor no ve pago ni bajas en ningún estado', () => {
  for (const estado of ['borrador', 'enviada', 'aprobada', 'pagada', 'anulada']) {
    const a = accionesDeCotizacion(cot(estado), 1, false);
    assert.equal(a.registrarPago, false, estado);
    assert.equal(a.darDeBaja, false, estado);
  }
});

test('4 · el admin ve el pago solo en aprobada, y la baja en borrador, enviada, aprobada y pagada', () => {
  assert.equal(accionesDeCotizacion(cot('aprobada'), 1, true).registrarPago, true);
  assert.equal(accionesDeCotizacion(cot('enviada'), 1, true).registrarPago, false);
  for (const estado of ['borrador', 'enviada', 'aprobada', 'pagada']) assert.equal(accionesDeCotizacion(cot(estado), 1, true).darDeBaja, true, estado);
  assert.equal(accionesDeCotizacion(cot('anulada'), 1, true).darDeBaja, false);
});

// 5 · 6 — Invalidación y ausencia de Anular

test('5 · una enviada invalidada sigue diciendo Enviada, con «Ya no es válida» aparte, y no se aprueba', () => {
  const invalidada = cot('enviada', { invalidatedAt: '2026-09-30T10:00:00Z' });
  assert.deepEqual(presentacionDeEstado(invalidada), { etiqueta: 'Enviada', condicion: 'Ya no es válida' });
  assert.equal(accionesDeCotizacion(invalidada, 1, true).aprobar, false);
  assert.equal(accionesDeCotizacion(invalidada, 1, true).crearNuevaDesdeSolicitud, true);
  assert.deepEqual(presentacionDeEstado(vigente), { etiqueta: 'Enviada', condicion: null });
});

test('6 · no existe la acción Anular, ni en la lógica ni en ninguna pantalla', () => {
  assert.ok(!Object.keys(accionesDeCotizacion(cot('enviada'), 1, true)).some((k) => /anul/i.test(k)));
  for (const f of archivosDelModulo().filter((f) => f.endsWith('.tsx') || f.endsWith('.ts'))) {
    assert.doesNotMatch(codigo(readFileSync(f, 'utf8')), />\s*Anular|['"]Anular['"]|\/annul|\/cancel/, relative(frontend, f));
  }
});

// 7 · 8 · 9 — Pago

test('7 · Yape exige código de operación', () => {
  assert.equal(errorDePago('yape', '  '), 'Un pago con Yape necesita el código de operación.');
  assert.equal(errorDePago('yape', 'OP-1'), null);
});

test('8 · efectivo no exige referencia', () => assert.equal(errorDePago('efectivo', ''), null));

test('9 · tarjeta no aparece como opción', () => {
  assert.deepEqual(METODOS_DE_PAGO.map((m) => m.valor), ['yape', 'efectivo']);
  assert.doesNotMatch(codigo(fuente('src/modules/b2b/pages/CotizacionPage.tsx')), /tarjeta/i);
});

// 10 · 11 — Editor de borrador

test('10 · el borrador se edita, y Subir/Bajar reordena sin salirse de la lista', () => {
  assert.equal(accionesDeCotizacion(cot('borrador'), 0, false).editarLineas, true);
  const [a, b, c] = [lineaLibre(), lineaLibre(), lineaLibre()];
  assert.deepEqual(moverLinea([a, b, c], 1, -1).map((l) => l.clave), [b.clave, a.clave, c.clave]);
  assert.deepEqual(moverLinea([a, b, c], 1, 1).map((l) => l.clave), [a.clave, c.clave, b.clave]);
  assert.deepEqual(moverLinea([a, b, c], 0, -1).map((l) => l.clave), [a.clave, b.clave, c.clave]);
  assert.deepEqual(moverLinea([a, b, c], 2, 1).map((l) => l.clave), [a.clave, b.clave, c.clave]);
});

test('10b · cero líneas es válido; una libre sin descripción, una cantidad 0 o un precio negativo no', () => {
  assert.equal(erroresDeLineas([]).size, 0);
  assert.match(erroresDeLineas([{ ...lineaLibre(), descripcion: '' }]).values().next().value, /escribe qué es/);
  assert.match(erroresDeLineas([{ ...lineaLibre(), descripcion: 'x', cantidad: '0' }]).values().next().value, /mayor que cero/);
  assert.match(erroresDeLineas([{ ...lineaLibre(), descripcion: 'x', precioUnitario: '-1' }]).values().next().value, /cero o más/);
});

test('11 · una enviada, aprobada o pagada no muestra edición de líneas; enviar exige al menos una', () => {
  for (const estado of ['enviada', 'aprobada', 'pagada', 'anulada']) assert.equal(accionesDeCotizacion(cot(estado), 3, true).editarLineas, false, estado);
  assert.equal(accionesDeCotizacion(cot('borrador'), 0, true).enviar, false);
  assert.equal(accionesDeCotizacion(cot('borrador'), 1, true).enviar, true);
});

// 12 — El umbral informa, no descuenta

test('12 · no_evaluable y configuracion_pendiente no aplican descuento: el total es cantidad × precio', () => {
  for (const estado of ['no_evaluable', 'configuracion_pendiente']) {
    assert.match(textoMayorista({ estado, umbral: null, importeDeLista: null, motivo: '' }).texto, /decide con tu criterio/);
  }
  const l = { ...lineaLibre(), descripcion: 'Cordón', cantidad: '100', precioUnitario: '0.8' };
  assert.equal(totalCobrado([l]), 80);
  assert.doesNotMatch(codigo(fuente('src/modules/b2b/logica/lineas.ts')), /umbral|mayorista|descuento\s*=/i);
});

// 13 · 14 — Fronteras e identidad

test('13 · ningún archivo de M07 importa de otro módulo', () => {
  for (const f of archivosDelModulo().filter((f) => /\.(tsx?|css)$/.test(f))) {
    for (const [, destino] of readFileSync(f, 'utf8').matchAll(/from\s+'([^']+)'|import\s+'([^']+)'/g)) {
      if (!destino?.startsWith('.')) continue;
      const absoluto = resolve(dirname(f), destino);
      const enModulos = relative(resolve(frontend, 'src/modules'), absoluto);
      assert.ok(enModulos.startsWith('..') || enModulos.startsWith('b2b'), `${relative(frontend, f)} importa ${destino}`);
    }
  }
});

test('14 · ningún customerId se pinta: las pantallas no lo leen', () => {
  for (const f of archivosDelModulo().filter((f) => f.endsWith('.tsx'))) {
    assert.doesNotMatch(codigo(readFileSync(f, 'utf8')), /customerId/, relative(frontend, f));
  }
});

test('las solicitudes cerrada y rechazada son finales: no se ofrece reabrirlas', () => {
  assert.equal(esFinal('cerrada'), true);
  assert.equal(esFinal('rechazada'), true);
  assert.deepEqual(estadosSiguientes('cerrada'), []);
  assert.deepEqual([...estadosSiguientes('recibida')], ['en_revision', 'rechazada']);
});
