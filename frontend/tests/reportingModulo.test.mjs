import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { asInstant, showCount } from '../src/modules/reporting/services/period.ts';
import {
  InconsistentIndicatorsError,
  verifyOrderIndicators,
} from '../src/modules/reporting/services/verifyOrderIndicators.ts';
import { decodeReportLink, encodeReportLink } from '../src/modules/reporting/services/reportLinks.ts';
import { explainComparison } from '../src/modules/reporting/services/explainComparison.ts';
import { assessReportComparison } from '../src/modules/reporting/services/reportComparisonGate.ts';
import { compareCreationWindowRates } from '../src/modules/reporting/services/creationWindowRate.ts';
import { previousEqualWindow } from '../src/modules/reporting/services/previousEqualWindow.ts';
import { reportingNavigation } from '../src/modules/reporting/navegacion.ts';

/**
 * Focales de M10 Reportes en el frontend. La lógica que decide qué cifra se
 * publica vive en `services/` y se prueba aquí sin React; lo que solo existe
 * como JSX (rutas, menú, textos visibles) se afirma sobre el código fuente.
 */

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const fuente = (ruta) => readFileSync(resolve(frontend, ruta), 'utf8');
const modulo = resolve(frontend, 'src/modules/reporting');
const archivos = (dir = modulo) =>
  readdirSync(dir).flatMap((n) => (statSync(join(dir, n)).isDirectory() ? archivos(join(dir, n)) : [join(dir, n)]));
/** El código sin comentarios: se prueba lo que se ejecuta, no lo que se explica. */
const codigo = (texto) => texto.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\{\/\*[\s\S]*?\*\/\}/g, '').replace(/^\s*\/\/.*$/gm, '');

const ESTADOS = ['pending_payment', 'payment_to_verify', 'preparing', 'ready_for_pickup', 'delivered', 'expired', 'cancelled'];
const pedido = { start: '2026-10-01T05:00:00.000Z', end: '2026-10-08T05:00:00.000Z' };
const respuesta = (extra = {}) => ({
  status: 'with_data',
  startInclusive: '2026-10-01T05:00:00+00:00',
  endExclusive: '2026-10-08T05:00:00+00:00',
  observedAtUtc: '2026-10-19T15:00:00.1234567+00:00',
  i01CreatedOrders: 5,
  i03CurrentStatusCounts: ESTADOS.map((status) => ({ status, count: status === 'pending_payment' ? 3 : status === 'expired' ? 2 : 0 })),
  i04CurrentlyExpiredOrders: 2,
  activeOrders: 4,
  inactiveOrders: 1,
  ...extra,
});
const incoherente = (fn) => assert.throws(fn, InconsistentIndicatorsError);

// 1 · Fechas del formulario

test('1 · el período se convierte con la zona elegida y admite segundos', () => {
  assert.equal(asInstant('2026-10-17T23:30', 'America/Lima'), '2026-10-18T04:30:00.000Z');
  assert.equal(asInstant('2026-10-17T10:00:30', 'America/Lima'), '2026-10-17T15:00:30.000Z');
  assert.equal(asInstant('2026-10-17T10:00', 'UTC'), '2026-10-17T10:00:00.000Z');
});

test('2 · una fecha imposible o el año 0000 se rechazan en vez de consultar otro día', () => {
  for (const malo of ['2026-02-30T10:00', '2026-02-29T10:00', '2026-10-17T24:00', '0000-01-01T00:00', '2026-10-17 10:00', '']) {
    assert.throws(() => asInstant(malo, 'UTC'), RangeError, malo);
  }
  assert.equal(asInstant('2028-02-29T10:00', 'UTC'), '2028-02-29T10:00:00.000Z');
});

test('3 · un recuento ausente se muestra como guion, nunca como cero', () => {
  assert.equal(showCount(null), '—');
  assert.equal(showCount(0), '0');
});

// 2 · Coherencia de la respuesta antes de publicarla

test('4 · una respuesta coherente del período pedido se publica tal cual', () => {
  const datos = respuesta();
  assert.equal(verifyOrderIndicators(datos, pedido), datos);
});

test('5 · fuente ausente sin métricas es válida; con un cero inventado no lo es', () => {
  const ausente = respuesta({ status: 'source_unavailable', observedAtUtc: null, i01CreatedOrders: null,
    i03CurrentStatusCounts: null, i04CurrentlyExpiredOrders: null, activeOrders: null, inactiveOrders: null });
  assert.equal(verifyOrderIndicators(ausente, pedido).status, 'source_unavailable');
  incoherente(() => verifyOrderIndicators({ ...ausente, i01CreatedOrders: 0 }, pedido));
});

test('6 · recuentos que no cuadran entre sí no se dibujan', () => {
  incoherente(() => verifyOrderIndicators(respuesta({ i01CreatedOrders: 6, activeOrders: 5 }), pedido));
  incoherente(() => verifyOrderIndicators(respuesta({ inactiveOrders: 2 }), pedido));
  incoherente(() => verifyOrderIndicators(respuesta({ i04CurrentlyExpiredOrders: 1 }), pedido));
  incoherente(() => verifyOrderIndicators(respuesta({ status: 'empty' }), pedido));
});

test('7 · una respuesta de otro período se descarta aunque sea coherente', () => {
  incoherente(() => verifyOrderIndicators(respuesta(), { start: '2026-09-24T05:00:00.000Z', end: pedido.start }));
});

// 3 · Enlaces compartibles

test('8 · el enlace conserva A, B y zona, y solo lleva parámetros de reportes', () => {
  const filtros = { zone: 'America/Lima',
    primary: { startLocal: '2026-10-01T00:00', endLocal: '2026-10-08T00:00' },
    comparison: { startLocal: '2026-09-24T00:00', endLocal: '2026-10-01T00:00' } };
  const enlace = encodeReportLink(filtros);
  assert.match(enlace, /^\/admin\/reportes\?rp_v=1&rp_z=America%2FLima&rp_a_i=[^&]+&rp_a_f=[^&]+&rp_b_i=[^&]+&rp_b_f=[^&]+$/);
  const leido = decodeReportLink(enlace.slice(enlace.indexOf('?')));
  assert.equal(leido.kind, 'valid');
  assert.deepEqual(leido.filters, filtros);
});

test('9 · un enlace malformado no aplica ningún filtro', () => {
  for (const malo of [
    '?rp_v=2&rp_z=UTC&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00',
    '?rp_v=1&rp_z=Europe/Madrid&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00',
    '?rp_v=1&rp_z=UTC&rp_z=UTC&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00',
    '?rp_v=1&rp_z=UTC&rp_a_i=2026-10-02T00:00&rp_a_f=2026-10-01T00:00',
    '?rp_v=1&rp_z=UTC&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00&rp_b_i=2026-09-01T00:00',
  ]) {
    assert.equal(decodeReportLink(malo).kind, 'invalid', malo);
  }
  assert.equal(decodeReportLink('').kind, 'none');
});

// 4 · Comparación A/B

const ventana = (s, e, o = '2026-10-19T15:00:00+00:00') => ({ startInclusive: s, endExclusive: e, observedAtUtc: o });
const A = ventana('2026-10-01T05:00:00+00:00', '2026-10-08T05:00:00+00:00');
const foto = (window, metrics, state = 'with_data') => ({ window, state, validated: true, metrics });

test('10 · la relación entre cohortes se clasifica sin leerla como historia', () => {
  assert.equal(explainComparison(A, A).relation, 'identical');
  assert.equal(explainComparison(A, ventana('2026-10-07T05:00:00+00:00', '2026-10-10T05:00:00+00:00')).relation, 'overlapping');
  assert.equal(explainComparison(A, ventana('2026-10-08T05:00:00+00:00', '2026-10-15T05:00:00+00:00')).relation, 'adjacent');
  assert.equal(explainComparison(A, ventana('2026-10-10T05:00:00+00:00', '2026-10-11T05:00:00+00:00')).relation, 'disjoint');
  assert.equal(explainComparison(A, A).mayPresentAsTimeEvolution, false);
});

test('11 · dos consultas independientes permiten una resta descriptiva rotulada como tal', () => {
  const B = ventana('2026-10-08T05:00:00+00:00', '2026-10-15T05:00:00+00:00', '2026-10-19T15:00:02.5+00:00');
  const decision = assessReportComparison(foto(A, { i01: 10 }), foto(B, { i01: 13 }));
  assert.equal(decision.decision, 'comparable');
  assert.equal(decision.differenceBasis, 'independent_snapshots');
  assert.equal(decision.provesSameSnapshot, false);
  assert.equal(decision.metrics[0].absoluteDifference, 3);
});

test('12 · cohortes solapadas, fuente ausente o métricas distintas retienen las diferencias', () => {
  const solapada = ventana('2026-10-07T05:00:00+00:00', '2026-10-09T05:00:00+00:00');
  const contigua = ventana('2026-10-08T05:00:00+00:00', '2026-10-15T05:00:00+00:00');
  assert.equal(assessReportComparison(foto(A, { i01: 1 }), foto(solapada, { i01: 1 })).decision, 'withheld');
  assert.equal(assessReportComparison(foto(A, { i01: 1 }), foto(contigua, { i01: null }, 'source_unavailable')).decision, 'withheld');
  assert.equal(assessReportComparison(foto(A, { i01: 1 }), foto(contigua, { otra: 1 })).decision, 'withheld');
  assert.equal(assessReportComparison(foto(A, { i01: 1 }), foto(contigua, { i01: 1 })).reasons.some((r) => /\bD\d+\b/.test(r)), false);
});

test('13 · los pedidos por cada 24 h solo se calculan si la compuerta lo permite', () => {
  const B = ventana('2026-10-08T05:00:00+00:00', '2026-10-11T05:00:00+00:00');
  const a = foto(A, { i01: 14 });
  const b = foto(B, { i01: 9 });
  const tasa = compareCreationWindowRates(a, b, assessReportComparison(a, b));
  assert.equal(tasa.kind, 'available');
  assert.equal(tasa.averagePer24hA, 2);
  assert.equal(tasa.averagePer24hB, 3);
  const solapada = foto(ventana('2026-10-07T05:00:00+00:00', '2026-10-09T05:00:00+00:00'), { i01: 1 });
  assert.equal(compareCreationWindowRates(a, solapada, assessReportComparison(a, solapada)).kind, 'withheld');
});

test('14 · el período anterior equivalente es contiguo y de igual duración', () => {
  const previo = previousEqualWindow('2026-10-08T05:00:00+00:00', '2026-10-15T05:00:00+00:00', 'America/Lima');
  assert.equal(previo.kind, 'ready');
  assert.equal(previo.startLocal, '2026-10-01T00:00');
  assert.equal(previo.endLocal, '2026-10-08T00:00');
  assert.equal(previousEqualWindow('2026-10-08T05:00:30+00:00', '2026-10-15T05:00:00+00:00', 'UTC').kind, 'unavailable');
});

// 5 · Permisos, frontera y presentación (sobre el código fuente)

test('15 · menú y ruta exigen administración y cuelgan de la capacidad reporting', () => {
  assert.equal(reportingNavigation.moduleCode, 'reporting');
  assert.ok(reportingNavigation.items.every((item) => item.minimumRole === 'admin'));
  assert.match(fuente('src/modules/reporting/routes.tsx'), /<RequireRole minimum="admin" \/>/);
  assert.match(fuente('src/app/routes.tsx'), /has\('reporting'\) && reportingRoutes/);
});

test('16 · ninguna pantalla consulta por su cuenta: todo pasa por la capa de servicios', () => {
  for (const archivo of archivos().filter((a) => /\.(tsx?)$/.test(a))) {
    const texto = codigo(readFileSync(archivo, 'utf8'));
    assert.doesNotMatch(texto, /\bfetch\(|localStorage|sessionStorage/, archivo);
    if (archivo.includes('/pages/') || archivo.includes('/components/')) {
      assert.doesNotMatch(texto, /\bhttp\.(get|post|put|delete)\(/, archivo);
    }
  }
});

test('17 · el usuario no ve códigos internos ni la palabra «ingresos»', () => {
  for (const archivo of archivos().filter((a) => a.endsWith('.tsx'))) {
    const texto = codigo(readFileSync(archivo, 'utf8'));
    const visibles = [...texto.matchAll(/>([^<>{}]+)</g), ...texto.matchAll(/'([^'\n]{8,})'/g)].map((m) => m[1]);
    for (const frase of visibles) {
      assert.doesNotMatch(frase, /\b(I0[1-7]|M03|D\d{1,2})\b/, `${archivo}: ${frase}`);
      assert.doesNotMatch(frase, /\b(ingresos|facturación|ventas netas)\b/i, `${archivo}: ${frase}`);
    }
  }
});

test('18 · los anuncios de estado salen de regiones persistentes, no de nodos que aparecen con su texto', () => {
  for (const ruta of ['src/modules/reporting/pages/ReportsPage.tsx', 'src/modules/reporting/components/OrderPeriodComparison.tsx']) {
    const texto = codigo(fuente(ruta));
    assert.match(texto, /className="sr-only" role="status" aria-live="polite" aria-atomic="true"/, ruta);
    assert.doesNotMatch(texto, /&& \(?\s*<(p|section|div)[^>]*role="status"/, ruta);
    assert.match(texto, /aria-busy=\{/, ruta);
  }
});
