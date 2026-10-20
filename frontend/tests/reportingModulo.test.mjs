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

// 6 · Generador CSV (puro; no expuesto hasta la auditoría de exportación de CORE)

const { buildOrderIndicatorsCsv, csvCell, CSV_MIME_TYPE } = await import('../src/modules/reporting/services/orderIndicatorsCsv.ts');
const lineas = (csv) => csv.content.replace(/^﻿/, '').split('\r\n').filter((l) => l !== '');

test('19 · el CSV de una cohorte lleva BOM, CRLF, cabecera en español y las once filas de la vista', () => {
  const csv = buildOrderIndicatorsCsv({ zone: 'America/Lima', baseline: respuesta() });
  assert.equal(csv.kind, 'ready');
  assert.equal(csv.mimeType, CSV_MIME_TYPE);
  assert.ok(csv.content.startsWith('﻿'));
  assert.ok(csv.content.endsWith('\r\n'));
  assert.doesNotMatch(csv.content.replace(/\r\n/g, ''), /\n/);
  const filas = lineas(csv);
  assert.equal(filas[0], 'Cohorte,Inicio (incluido),Fin (excluido),Zona horaria,Observado,Fuente,Indicador,Pedidos');
  assert.equal(filas.length, 1 + 11);
  assert.equal(filas[1], 'A,2026-10-01 00:00,2026-10-08 00:00,America/Lima,2026-10-19 10:00,Módulo de ventas,Pedidos creados,5');
  assert.ok(filas.includes('A,2026-10-01 00:00,2026-10-08 00:00,America/Lima,2026-10-19 10:00,Módulo de ventas,Estado actual: Vencido,2'));
  assert.equal(csv.fileName, 'reportes-pedidos-202610010000-202610080000.csv');
});

test('20 · sin fuente o con datos incoherentes no hay archivo, nunca uno con ceros', () => {
  const ausente = respuesta({ status: 'source_unavailable', observedAtUtc: null, i01CreatedOrders: null,
    i03CurrentStatusCounts: null, i04CurrentlyExpiredOrders: null, activeOrders: null, inactiveOrders: null });
  assert.equal(buildOrderIndicatorsCsv({ zone: 'UTC', baseline: ausente }).kind, 'unavailable');
  assert.equal(buildOrderIndicatorsCsv({ zone: 'UTC', baseline: respuesta({ inactiveOrders: 2 }) }).kind, 'unavailable');
  assert.equal(buildOrderIndicatorsCsv({ zone: 'UTC', baseline: respuesta(), comparison: respuesta({ i04CurrentlyExpiredOrders: 9 }) }).kind, 'unavailable');
});

test('21 · una celda de texto que empieza por = + - @ o tabulador se neutraliza; los números no', () => {
  assert.equal(csvCell('=HYPERLINK("x")'), `"'=HYPERLINK(""x"")"`);
  assert.equal(csvCell('+1'), "'+1");
  assert.equal(csvCell('-2'), "'-2");
  assert.equal(csvCell('@SUMA'), "'@SUMA");
  assert.equal(csvCell('\tx'), "'\tx");
  assert.equal(csvCell(-3), '-3');
  assert.equal(csvCell('a,b'), '"a,b"');
  assert.equal(csvCell('línea\r\notra'), '"línea\r\notra"');
  assert.throws(() => csvCell(1.5), RangeError);
});

test('22 · con B contigua se añaden sus filas y la resta descriptiva en el orden de la vista', () => {
  const B = respuesta({ startInclusive: '2026-10-08T05:00:00+00:00', endExclusive: '2026-10-15T05:00:00+00:00',
    observedAtUtc: '2026-10-19T15:00:02+00:00', i01CreatedOrders: 7, activeOrders: 6,
    i03CurrentStatusCounts: ESTADOS.map((status) => ({ status, count: status === 'pending_payment' ? 5 : status === 'expired' ? 2 : 0 })) });
  const filas = lineas(buildOrderIndicatorsCsv({ zone: 'America/Lima', baseline: respuesta(), comparison: B }));
  assert.equal(filas.filter((f) => f.startsWith('B,')).length, 11);
  const restas = filas.filter((f) => f.startsWith('B − A'));
  assert.equal(restas.length, 11);
  assert.equal(restas[0], 'B − A (resta descriptiva),,,America/Lima,,Cálculo de reportes,Pedidos creados,2');
  assert.equal(restas[1], 'B − A (resta descriptiva),,,America/Lima,,Cálculo de reportes,Actualmente vencidos,0');
});

test('23 · con B solapada se exportan ambas cohortes pero ninguna resta', () => {
  const B = respuesta({ startInclusive: '2026-10-07T05:00:00+00:00', endExclusive: '2026-10-14T05:00:00+00:00' });
  const filas = lineas(buildOrderIndicatorsCsv({ zone: 'UTC', baseline: respuesta(), comparison: B }));
  assert.equal(filas.filter((f) => f.startsWith('B,')).length, 11);
  assert.equal(filas.filter((f) => f.startsWith('B − A')).length, 0);
});

test('24 · el CSV no lleva códigos internos, valores técnicos de estado ni importes', () => {
  const csv = buildOrderIndicatorsCsv({ zone: 'UTC', baseline: respuesta() }).content;
  assert.doesNotMatch(csv, /\b(I0[1-7]|M03|D\d{1,2})\b/);
  assert.doesNotMatch(csv, /pending_payment|ready_for_pickup|expired|cancelled/);
  assert.doesNotMatch(csv, /S\/|\bPEN\b|importe|ingresos/i);
});

test('25 · ni el generador ni la exportación preparada están expuestos: ninguna pantalla los importa (D-M10-05)', () => {
  for (const archivo of archivos().filter((a) => a.endsWith('.tsx'))) {
    assert.doesNotMatch(codigo(readFileSync(archivo, 'utf8')), /orderIndicatorsCsv|reportExport/, archivo);
  }
});

// 7 · Exportación preparada: solo con auditoría registrada (D-M10-05)

const { prepareOrderIndicatorsExport, exportAuditPort } = await import('../src/modules/reporting/services/reportExport.ts');
const auditoria = (falla = false) => {
  const registros = [];
  return { registros, port: { async recordExport(entry) { if (falla) throw new Error('caída'); registros.push(entry); } } };
};

test('26 · hoy no hay puerto de auditoría: la exportación queda bloqueada y no hay archivo', async () => {
  assert.equal(exportAuditPort, null);
  const resultado = await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta() });
  assert.equal(resultado.kind, 'blocked');
  assert.equal('content' in resultado, false);
});

test('27 · con auditoría, primero se registra el hecho agregado y después se entrega el archivo', async () => {
  const { registros, port } = auditoria();
  const B = respuesta({ startInclusive: '2026-10-08T05:00:00+00:00', endExclusive: '2026-10-15T05:00:00+00:00' });
  const resultado = await prepareOrderIndicatorsExport({ zone: 'America/Lima', baseline: respuesta(), comparison: B }, port);
  assert.equal(resultado.kind, 'ready');
  assert.equal(registros.length, 1);
  assert.deepEqual(registros[0], {
    report: 'pedidos', startInclusive: '2026-10-01T05:00:00+00:00', endExclusive: '2026-10-08T05:00:00+00:00',
    zone: 'America/Lima', includesComparison: true, dataRows: 33, fileName: resultado.fileName,
  });
});

test('28 · si la auditoría falla no se genera el archivo', async () => {
  const resultado = await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta() }, auditoria(true).port);
  assert.equal(resultado.kind, 'audit_failed');
  assert.equal('content' in resultado, false);
});

test('29 · con datos incoherentes o sin fuente no se exporta ni se audita nada', async () => {
  const { registros, port } = auditoria();
  assert.equal((await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta({ activeOrders: 9 }) }, port)).kind, 'unavailable');
  const ausente = respuesta({ status: 'source_unavailable', observedAtUtc: null, i01CreatedOrders: null,
    i03CurrentStatusCounts: null, i04CurrentlyExpiredOrders: null, activeOrders: null, inactiveOrders: null });
  assert.equal((await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: ausente }, port)).kind, 'unavailable');
  assert.equal(registros.length, 0);
});

// 8 · Combinaciones reales de filtros y zonas

const { quickPeriodRange } = await import('../src/modules/reporting/services/period.ts');

test('30 · los períodos rápidos usan el día de la zona elegida, no el del navegador', () => {
  const madrugadaUtc = new Date('2026-10-20T03:00:00Z'); // en Lima aún es 19 de octubre
  assert.deepEqual(quickPeriodRange('today', 'America/Lima', madrugadaUtc), { start: '2026-10-19T00:00', end: '2026-10-20T00:00' });
  assert.deepEqual(quickPeriodRange('today', 'UTC', madrugadaUtc), { start: '2026-10-20T00:00', end: '2026-10-21T00:00' });
  assert.deepEqual(quickPeriodRange('last7', 'America/Lima', madrugadaUtc), { start: '2026-10-13T00:00', end: '2026-10-20T00:00' });
});

test('31 · «este mes» respeta el cambio de mes y de año en la zona elegida', () => {
  const finDeMesEnLima = new Date('2026-11-01T03:00:00Z'); // 31/10 en Lima, 1/11 en UTC
  assert.deepEqual(quickPeriodRange('thisMonth', 'America/Lima', finDeMesEnLima), { start: '2026-10-01T00:00', end: '2026-11-01T00:00' });
  assert.deepEqual(quickPeriodRange('thisMonth', 'UTC', finDeMesEnLima), { start: '2026-11-01T00:00', end: '2026-12-01T00:00' });
  assert.deepEqual(quickPeriodRange('thisMonth', 'UTC', new Date('2026-12-15T12:00:00Z')), { start: '2026-12-01T00:00', end: '2027-01-01T00:00' });
});

test('32 · un período rápido produce siempre un intervalo válido para la consulta', () => {
  for (const zona of ['America/Lima', 'UTC']) {
    for (const periodo of ['today', 'last7', 'thisMonth']) {
      const { start, end } = quickPeriodRange(periodo, zona, new Date('2026-02-28T23:30:00Z'));
      assert.ok(Date.parse(asInstant(end, zona)) > Date.parse(asInstant(start, zona)), `${zona} ${periodo}`);
    }
  }
});

test('33 · una cohorte vacía real se exporta con ceros verdaderos y su zona en las fechas', () => {
  const vacia = respuesta({ status: 'empty', i01CreatedOrders: 0, i04CurrentlyExpiredOrders: 0, activeOrders: 0, inactiveOrders: 0,
    i03CurrentStatusCounts: ESTADOS.map((status) => ({ status, count: 0 })) });
  const lima = lineas(buildOrderIndicatorsCsv({ zone: 'America/Lima', baseline: vacia }));
  const utc = lineas(buildOrderIndicatorsCsv({ zone: 'UTC', baseline: vacia }));
  assert.equal(lima.length, 12);
  assert.ok(lima.slice(1).every((fila) => fila.endsWith(',0')));
  assert.ok(lima[1].startsWith('A,2026-10-01 00:00,2026-10-08 00:00,America/Lima,'));
  assert.ok(utc[1].startsWith('A,2026-10-01 05:00,2026-10-08 05:00,UTC,'));
});

// 9 · Zona IANA de la instalación

const { INSTALLATION_ZONE, REPORT_ZONES, isReportZone } = await import('../src/modules/reporting/services/period.ts');

test('34 · las fechas siguen el reloj IANA de la zona: la hora saltada o repetida se rechaza', () => {
  // Perú tuvo horario de verano en 1994: el 1 de enero saltó de 00:00 a 01:00
  // y el 1 de abril volvió de 00:00 a 23:00. Un desplazamiento fijo de −05:00
  // habría consultado una hora que no existió o una de las dos que se repitieron.
  assert.throws(() => asInstant('1994-01-01T00:30', 'America/Lima'), /no existe/);
  assert.throws(() => asInstant('1994-03-31T23:30', 'America/Lima'), /se repite/);
  assert.equal(asInstant('1994-01-01T01:30', 'America/Lima'), '1994-01-01T05:30:00.000Z');
  assert.equal(asInstant('1994-02-15T10:00', 'America/Lima'), '1994-02-15T14:00:00.000Z');
  assert.equal(asInstant('1994-03-31T22:30', 'America/Lima'), '1994-04-01T02:30:00.000Z');
  // El período anterior tampoco se propone si cae en la hora repetida.
  assert.equal(previousEqualWindow('1994-04-01T05:00:00Z', '1994-04-01T07:00:00Z', 'America/Lima').kind, 'unavailable');
});

test('35 · el calendario de la consulta acaba en el año 9999 también en UTC', () => {
  assert.equal(asInstant('9999-12-31T18:59', 'America/Lima'), '9999-12-31T23:59:00.000Z');
  assert.throws(() => asInstant('9999-12-31T19:00', 'America/Lima'), RangeError);
  assert.equal(asInstant('0001-01-01T00:00', 'UTC'), '0001-01-01T00:00:00.000Z');
});

test('36 · la zona de la instalación es la del negocio y una zona no admitida no convierte nada', () => {
  const ventas = fuente('../backend/Sillar.Shared.Data/Numbering/TransactionalSeriesAllocator.cs');
  assert.match(ventas, new RegExp(`BusinessTimeZone = "${INSTALLATION_ZONE}"`));
  assert.equal(REPORT_ZONES[0], INSTALLATION_ZONE);
  assert.ok(REPORT_ZONES.every((zona) => new Intl.DateTimeFormat('en-US', { timeZone: zona }).resolvedOptions().timeZone === zona));
  for (const ajena of ['Europe/Madrid', 'america/lima', '-05:00', '', null]) {
    assert.equal(isReportZone(ajena), false, String(ajena));
  }
  assert.throws(() => asInstant('2026-10-17T10:00', 'Europe/Madrid'), RangeError);
  assert.equal(buildOrderIndicatorsCsv({ zone: 'Europe/Madrid', baseline: respuesta() }).kind, 'unavailable');
  assert.equal(previousEqualWindow('2026-10-08T05:00:00Z', '2026-10-15T05:00:00Z', 'Europe/Madrid').kind, 'unavailable');
});

test('37 · ningún servicio convierte fechas con un desplazamiento escrito a mano', () => {
  for (const archivo of archivos().filter((ruta) => /\.(ts|tsx)$/.test(ruta))) {
    const texto = codigo(readFileSync(archivo, 'utf8'));
    assert.doesNotMatch(texto, /['"`][+-]0?5:00['"`]|-300\b|-5 \* 60|ZONE_OFFSET/, archivo);
  }
});

test('38 · cada fila del CSV dice de dónde sale la cifra y cuándo se observó', () => {
  const b = respuesta({ startInclusive: '2026-10-08T05:00:00+00:00', endExclusive: '2026-10-15T05:00:00+00:00' });
  const filas = lineas(buildOrderIndicatorsCsv({ zone: 'UTC', baseline: respuesta(), comparison: b })).slice(1).map((fila) => fila.split(','));
  for (const celdas of filas) {
    if (celdas[0] === 'B − A (resta descriptiva)') {
      assert.equal(celdas[5], 'Cálculo de reportes');
    } else {
      assert.equal(celdas[5], 'Módulo de ventas');
      assert.equal(celdas[4], '2026-10-19 15:00');
    }
  }
  assert.ok(filas.some((celdas) => celdas[5] === 'Cálculo de reportes'));
});
