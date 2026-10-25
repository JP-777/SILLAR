import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
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

const { prepareOrderIndicatorsExport: exportarConZona, exportAuditPort } = await import('../src/modules/reporting/services/reportExport.ts');
const { resolveInstallationZone } = await import('../src/modules/reporting/services/installationZone.ts');
/** Instalación con zona verificada: estas pruebas tratan de la auditoría, no de la zona. */
const INSTALACION_VERIFICADA = resolveInstallationZone({ kind: 'verified', original: 'America/Lima', canonical: 'America/Lima' });
const prepareOrderIndicatorsExport = (pedido, puerto, plazo, instalacion = INSTALACION_VERIFICADA) =>
  exportarConZona(pedido, puerto, plazo, instalacion);
/** Puerto falso: registra lo que recibe y responde lo que diga `decidir`. */
const auditoria = (decidir = () => 'recorded') => {
  const registros = [];
  return { registros, port: { async recordExport(entry) { registros.push(entry); return decidir(entry); } } };
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
    comparisonStartInclusive: '2026-10-08T05:00:00+00:00', comparisonEndExclusive: '2026-10-15T05:00:00+00:00',
  });
});

test('28 · si la auditoría falla no se genera el archivo', async () => {
  const resultado = await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta() }, auditoria(() => { throw new Error('caída'); }).port);
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

const { INSTALLATION_ZONE, REPORT_ZONES, isReportZone, reportZonesFor } = await import('../src/modules/reporting/services/period.ts');

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

// 10 · Estados del panel, auditoría que niega y zona por instalación

test('39 · sin la confirmación exacta de la auditoría no sale ningún byte', async () => {
  const pedir = (port) => prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta() }, port);
  const casos = [
    ['ausente', null, 'blocked'],
    ['falla', auditoria(() => { throw new Error('caída'); }).port, 'audit_failed'],
    ['rechazo asíncrono', auditoria(() => Promise.reject(new Error('no'))).port, 'audit_failed'],
    ['niega', auditoria(() => 'denied').port, 'audit_denied'],
    ['sin respuesta', auditoria(() => undefined).port, 'audit_failed'],
    ['respuesta parecida', auditoria(() => 'Recorded').port, 'audit_failed'],
    ['objeto', auditoria(() => ({ recorded: true })).port, 'audit_failed'],
    ['verdadero', auditoria(() => true).port, 'audit_failed'],
  ];
  for (const [nombre, port, esperado] of casos) {
    const resultado = await pedir(port);
    assert.equal(resultado.kind, esperado, nombre);
    assert.equal('content' in resultado, false, nombre);
    assert.equal('fileName' in resultado, false, nombre);
    assert.doesNotMatch(JSON.stringify(resultado), /Pedidos creados|\uFEFF|﻿/, nombre);
  }
});

test('40 · la auditoría recibe un registro congelado y sin cifras, sin comparación con sus límites nulos', async () => {
  const { registros, port } = auditoria((entry) => {
    assert.throws(() => { entry.dataRows = 0; }, TypeError);
    return 'recorded';
  });
  const resultado = await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta() }, port);
  assert.equal(resultado.kind, 'ready');
  assert.ok(Object.isFrozen(registros[0]));
  assert.equal(registros[0].comparisonStartInclusive, null);
  assert.equal(registros[0].comparisonEndExclusive, null);
  assert.deepEqual(Object.keys(registros[0]).sort(),
    ['comparisonEndExclusive', 'comparisonStartInclusive', 'dataRows', 'endExclusive', 'fileName', 'includesComparison', 'report', 'startInclusive', 'zone']);
});

test('41 · se neutraliza la fórmula escondida tras espacios o con signos de ancho completo', () => {
  for (const peligrosa of [' =1+1', '  +1', '\u00A0-1', '\u3000@x', '＝1+1', '＋1', '－1', '＠x', ' ＝x', '\n=1', '\r=1']) {
    const celda = csvCell(peligrosa);
    assert.match(celda, /^"?'/, JSON.stringify(peligrosa));
  }
  for (const inocente of [' a', 'Pedidos creados', 'B − A (resta descriptiva)', '2026-10-01 00:00', 'America/Lima']) {
    assert.ok(!csvCell(inocente).includes("'"), inocente);
  }
});

const { assessFreshness, describeAge, STALE_AFTER_MINUTES } = await import('../src/modules/reporting/services/freshness.ts');

test('42 · la antigüedad de lo observado distingue al día, desactualizado y desconocido', () => {
  const observado = Date.parse('2026-10-19T15:00:00Z');
  const tras = (min) => observado + min * 60_000;
  assert.equal(STALE_AFTER_MINUTES, 15);
  assert.deepEqual(assessFreshness('2026-10-19T15:00:00Z', tras(0)), { kind: 'fresh', minutes: 0 });
  assert.deepEqual(assessFreshness('2026-10-19T15:00:00Z', tras(14.99)), { kind: 'fresh', minutes: 14 });
  assert.deepEqual(assessFreshness('2026-10-19T15:00:00Z', tras(15)), { kind: 'stale', minutes: 15 });
  assert.deepEqual(assessFreshness('2026-10-19T15:00:00.1234567+00:00', tras(125)), { kind: 'stale', minutes: 124 });
  // Reloj del equipo algo adelantado al del servidor: se tolera hasta 2 min.
  assert.deepEqual(assessFreshness('2026-10-19T15:00:00Z', tras(-1.5)), { kind: 'fresh', minutes: 0 });
  assert.equal(assessFreshness('2026-10-19T15:00:00Z', tras(-3)).kind, 'unknown');
  assert.equal(assessFreshness(null, tras(0)).kind, 'unknown');
  assert.equal(assessFreshness('no es fecha', tras(0)).kind, 'unknown');
  assert.equal(describeAge(0), 'hace menos de un minuto');
  assert.equal(describeAge(15), 'hace 15 min');
  assert.equal(describeAge(120), 'hace 2 h');
  assert.equal(describeAge(125), 'hace 2 h 5 min');
});

const { ApiError } = await import('../src/shared/http/errors.ts');
const { isSourceFailure } = await import('../src/modules/reporting/services/sourceFailure.ts');

test('43 · ventas fallando es un 503 explicado; ni un 500 ni un 503 sin cuerpo pasan por él', () => {
  assert.equal(isSourceFailure(new ApiError('Unexpected', 503, 'El módulo de ventas no respondió…', 'Esto no significa cero pedidos.', null, null, true)), true);
  assert.equal(isSourceFailure(new ApiError('Unexpected', 503, 'Servicio no disponible', null, null, null, false)), false);
  assert.equal(isSourceFailure(new ApiError('Unexpected', 500, 'Internal Server Error', null, null, null, true)), false);
  assert.equal(isSourceFailure(new Error('503')), false);
  // La pantalla enseña la frase del servidor y ninguna cifra (sin estado «ready»).
  const servicio = codigo(fuente('src/modules/reporting/services/reporting.ts'));
  assert.match(servicio, /isSourceFailure\(error\)\) \{\s*return \{ kind: 'inline', message: error\.displayMessage/);
});

test('44 · el panel dice cuándo se observó y avisa cuando los recuentos se han quedado viejos', () => {
  const pagina = codigo(fuente('src/modules/reporting/pages/ReportsPage.tsx'));
  assert.match(pagina, /assessFreshness\(data\.observedAtUtc, now\)/);
  assert.match(pagina, /freshness\?\.kind === 'stale' && \(/);
  assert.match(pagina, /freshness\?\.kind === 'unknown' && /);
  // Sin consulta automática: el reloj solo vuelve a pintar, no llama a la red.
  assert.match(pagina, /setInterval\(\(\) => setNow\(Date\.now\(\)\), 60_000\)/);
  assert.doesNotMatch(pagina, /setInterval\([^)]*loadOrderIndicators/);
});

const zonas = await import('../src/modules/reporting/services/installationZone.ts');
/** Lo que CORE publicaría para una zona verificada, en la forma que consume M10. */
const verificada = (zona) => ({ kind: 'verified', original: zona, canonical: zona });

test('45 · la zona de la instalación solo la publica CORE; sin fuente, la del negocio', () => {
  assert.equal(zonas.installationZoneObservation, null);
  assert.deepEqual(zonas.resolveInstallationZone(null).zone, 'America/Lima');
  assert.equal(zonas.resolveInstallationZone(null).origin, 'historical_fallback');
  assert.deepEqual(zonas.resolveInstallationZone(verificada('America/Bogota')), { zone: 'America/Bogota', origin: 'installation', original: 'America/Bogota', alias: false });
  assert.equal(INSTALLATION_ZONE, zonas.BUSINESS_DEFAULT_ZONE);
  // La URL elige entre las admitidas; nunca añade una.
  assert.deepEqual([...REPORT_ZONES], ['America/Lima', 'UTC']);
  assert.equal(decodeReportLink('?rp_v=1&rp_z=America%2FBogota&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00').kind, 'invalid');
  // Y ninguna pantalla decide la zona por su cuenta: se elige de la lista.
  const pagina = codigo(fuente('src/modules/reporting/pages/ReportsPage.tsx'));
  assert.match(pagina, /REPORT_ZONES\.map\(/);
  assert.doesNotMatch(pagina, /Intl\.DateTimeFormat\(\)\.resolvedOptions|getTimezoneOffset/);
});

const ZONAS_PUBLICADAS_MALAS = ['-05:00', 'america/lima', 'America/Lima ', 'Marte/Olimpo', '', null, 5, '../etc', 'America/Lima?rp_z=UTC', { zone: 'America/Lima' }];

test('46 · una zona publicada pero inválida no cae en la del negocio: bloquea los reportes', () => {
  // Fuente ausente y fuente mala son dos estados distintos.
  assert.equal(zonas.resolveInstallationZone(null).origin, 'historical_fallback');
  for (const mala of ZONAS_PUBLICADAS_MALAS) {
    const resuelta = zonas.resolveInstallationZone(verificada(mala));
    assert.equal(resuelta.origin, 'unavailable', String(mala));
    assert.equal(resuelta.zone, null, String(mala));
    assert.match(resuelta.reason, /no es válida|no reconoce/);
  }
  // Sin zona no queda ninguna, tampoco UTC.
  assert.deepEqual(reportZonesFor(zonas.resolveInstallationZone(verificada('Marte/Olimpo'))), []);
  assert.deepEqual([...reportZonesFor(zonas.resolveInstallationZone(null))], ['America/Lima', 'UTC']);
  // La pantalla no monta el formulario sin zona y no la sustituye por ninguna.
  const pagina = codigo(fuente('src/modules/reporting/pages/ReportsPage.tsx'));
  assert.match(pagina, /if \(INSTALLATION\.zone === null\) \{[\s\S]*?role="alert">\{INSTALLATION\.reason\}<\/p>[\s\S]*?\}\s*return <ReportsView/);
  assert.doesNotMatch(pagina, /INSTALLATION_ZONE|BUSINESS_DEFAULT_ZONE|'America\/Lima'/);
  // La exportación real sigue sin conectar.
  assert.match(codigo(fuente('src/modules/reporting/services/reportExport.ts')), /exportAuditPort[^=;]*= null;/);
});

test('47 · con la zona publicada inválida, ningún período, enlace, comparación ni CSV se forma', () => {
  // Los servicios reales, con la fuente de zona sustituida en un proceso aparte:
  // la lista de zonas se fija al cargar el módulo.
  const servicios = (nombre) => new URL(`../src/modules/reporting/services/${nombre}`, import.meta.url).href;
  const guion = `
    import { mock } from 'node:test';
    const real = await import(${JSON.stringify(servicios('installationZone.ts'))});
    mock.module(${JSON.stringify(servicios('installationZone.ts'))}, { namedExports: {
      ...real, resolveInstallationZone: (fuente = { kind: 'verified', original: process.env.ZONA, canonical: process.env.ZONA }) => real.resolveInstallationZone(fuente),
    } });
    const p = await import(${JSON.stringify(servicios('period.ts'))});
    const l = await import(${JSON.stringify(servicios('reportLinks.ts'))});
    const w = await import(${JSON.stringify(servicios('previousEqualWindow.ts'))});
    const c = await import(${JSON.stringify(servicios('orderIndicatorsCsv.ts'))});
    const lanza = (f) => { try { f(); return 'sin error'; } catch (e) { return e.constructor.name; } };
    console.log(JSON.stringify({
      origen: p.INSTALLATION.origin, zona: p.INSTALLATION_ZONE, zonas: p.REPORT_ZONES,
      admitidas: ['America/Lima', 'UTC', process.env.ZONA].map(p.isReportZone),
      lima: lanza(() => p.asInstant('2026-10-17T10:00', 'America/Lima')),
      utc: lanza(() => p.asInstant('2026-10-17T10:00', 'UTC')),
      enlace: l.decodeReportLink('?rp_v=1&rp_z=America%2FLima&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00').kind,
      compartir: l.encodeReportLink({ zone: 'UTC', primary: { startLocal: '2026-10-01T00:00', endLocal: '2026-10-02T00:00' }, comparison: null }),
      anterior: w.previousEqualWindow('2026-10-01T05:00:00.000Z', '2026-10-02T05:00:00.000Z', 'America/Lima').kind,
      csv: c.buildOrderIndicatorsCsv({ zone: 'America/Lima', baseline: null }).kind,
    }));`;
  for (const zona of ['Marte/Olimpo', 'america/lima', '-05:00']) {
    const salida = spawnSync(process.execPath, ['--experimental-test-module-mocks', '--no-warnings', '--input-type=module', '-e', guion], {
      env: { ...process.env, ZONA: zona }, encoding: 'utf8',
    });
    assert.equal(salida.status, 0, salida.stderr);
    assert.deepEqual(JSON.parse(salida.stdout), {
      origen: 'unavailable', zona: null, zonas: [], admitidas: [false, false, false],
      lima: 'RangeError', utc: 'RangeError', enlace: 'invalid', compartir: null,
      anterior: 'unavailable', csv: 'unavailable',
    }, zona);
  }
});

test('48 · una auditoría que no contesta a tiempo no deja pasar el archivo, aunque confirme tarde', async () => {
  const { EXPORT_AUDIT_TIMEOUT_MS } = await import('../src/modules/reporting/services/reportExport.ts');
  assert.ok(EXPORT_AUDIT_TIMEOUT_MS > 0 && EXPORT_AUDIT_TIMEOUT_MS <= 30_000);
  let confirmar;
  const muda = { recordExport: () => new Promise((resolve) => { confirmar = resolve; }) };
  const resultado = await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta() }, muda, 20);
  assert.equal(resultado.kind, 'audit_failed');
  assert.equal('content' in resultado, false);
  confirmar('recorded');
  await new Promise((r) => setImmediate(r));
  assert.equal(resultado.kind, 'audit_failed');
  // La que contesta dentro del plazo sigue entregando.
  const lenta = { recordExport: () => new Promise((resolve) => setTimeout(resolve, 5, 'recorded')) };
  assert.equal((await prepareOrderIndicatorsExport({ zone: 'UTC', baseline: respuesta() }, lenta, 200)).kind, 'ready');
});

test('49 · una zona IANA vigente que el motor renombra vale de punta a punta; otra grafía, no', () => {
  // ICU contesta con nombres antiguos; el vigente no puede quedar bloqueado.
  for (const vigente of ['America/Argentina/Buenos_Aires', 'Asia/Kolkata', 'Europe/Kyiv', 'America/Nuuk', 'Asia/Kathmandu', 'Etc/UTC']) {
    assert.deepEqual(zonas.resolveInstallationZone(verificada(vigente)), { zone: vigente, origin: 'installation', original: vigente, alias: false }, vigente);
  }
  // Todas las que conoce el motor siguen valiendo con su propio nombre.
  assert.deepEqual(Intl.supportedValuesOf('timeZone').filter((zona) => zonas.canonicalIanaZone(zona) !== zona), []);
  // Cambiar mayúsculas no es otra zona: bloquea, también en nombres renombrados.
  for (const grafia of ['AMERICA/LIMA', 'America/LIMA', 'America/LIma', 'AmErica/Lima', 'America/BogotA', 'asia/kolkata', 'ASIA/KOLKATA', 'Asia/KOLKATA', 'utc', 'Etc/utc', '+05:30']) {
    assert.equal(zonas.resolveInstallationZone(verificada(grafia)).origin, 'unavailable', grafia);
  }
  // Período, enlace, comparación y CSV usan la misma zona publicada, tal cual.
  const servicios = (nombre) => new URL(`../src/modules/reporting/services/${nombre}`, import.meta.url).href;
  const guion = `
    import { mock } from 'node:test';
    const real = await import(${JSON.stringify(servicios('installationZone.ts'))});
    mock.module(${JSON.stringify(servicios('installationZone.ts'))}, { namedExports: {
      ...real, resolveInstallationZone: (fuente = { kind: 'verified', original: process.env.ZONA, canonical: process.env.ZONA }) => real.resolveInstallationZone(fuente),
    } });
    const p = await import(${JSON.stringify(servicios('period.ts'))});
    const l = await import(${JSON.stringify(servicios('reportLinks.ts'))});
    const w = await import(${JSON.stringify(servicios('previousEqualWindow.ts'))});
    const c = await import(${JSON.stringify(servicios('orderIndicatorsCsv.ts'))});
    const Z = process.env.ZONA;
    const vista = { ...JSON.parse(process.env.VISTA), startInclusive: p.asInstant('2026-10-01T00:00', Z), endExclusive: p.asInstant('2026-10-02T00:00', Z) };
    const csv = c.buildOrderIndicatorsCsv({ zone: Z, baseline: vista });
    console.log(JSON.stringify({
      zonas: p.REPORT_ZONES, inicio: vista.startInclusive,
      enlace: l.decodeReportLink('?rp_v=1&rp_z=' + encodeURIComponent(Z) + '&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00').kind,
      lima: l.decodeReportLink('?rp_v=1&rp_z=America%2FLima&rp_a_i=2026-10-01T00:00&rp_a_f=2026-10-02T00:00').kind,
      anterior: w.previousEqualWindow(vista.startInclusive, vista.endExclusive, Z).kind,
      csv: csv.kind, fila: csv.content?.split('\\r\\n')[1].split(',').slice(1, 4),
      csvLima: c.buildOrderIndicatorsCsv({ zone: 'America/Lima', baseline: vista }).kind,
    }));`;
  const salida = spawnSync(process.execPath, ['--experimental-test-module-mocks', '--no-warnings', '--input-type=module', '-e', guion], {
    env: { ...process.env, ZONA: 'America/Argentina/Buenos_Aires', VISTA: JSON.stringify(respuesta()) }, encoding: 'utf8',
  });
  assert.equal(salida.status, 0, salida.stderr);
  const visto = JSON.parse(salida.stdout);
  assert.deepEqual(visto.zonas, ['America/Argentina/Buenos_Aires', 'UTC']);
  assert.equal(visto.inicio, '2026-10-01T03:00:00.000Z');
  assert.equal(visto.enlace, 'valid');
  assert.equal(visto.lima, 'invalid');
  assert.equal(visto.anterior, 'ready');
  assert.equal(visto.csv, 'ready');
  assert.deepEqual(visto.fila.map((celda) => celda.replace(/^"|"$/g, '')), ['2026-10-01 00:00', '2026-10-02 00:00', 'America/Argentina/Buenos_Aires']);
  assert.equal(visto.csvLima, 'unavailable');
});

const { readZoneObservation, consumeZoneObservation } = await import('../src/modules/reporting/services/zoneObservation.ts');

test('50 · solo una observación verificada habilita la zona; conserva el original y distingue el alias', () => {
  const consumir = (entrada) => consumeZoneObservation(readZoneObservation(entrada));
  // Verificada: la zona es la canónica y el original viaja intacto.
  assert.deepEqual(consumir(verificada('America/Lima')), { kind: 'enabled', zone: 'America/Lima', original: 'America/Lima', alias: false });
  assert.deepEqual(consumir({ kind: 'verified', original: 'Asia/Calcutta', canonical: 'Asia/Kolkata' }),
    { kind: 'enabled', zone: 'Asia/Kolkata', original: 'Asia/Calcutta', alias: true });
  // Lo que el motor resuelve no es autoridad, ni siquiera para la zona del negocio.
  for (const [original, runtimeId] of [['Asia/Kolkata', 'Asia/Calcutta'], ['America/Lima', 'America/Lima']]) {
    const bloqueada = consumir({ kind: 'runtime_resolved', original, runtimeId });
    assert.equal(bloqueada.kind, 'blocked');
    assert.equal(bloqueada.cause, 'not_authoritative');
    assert.equal(bloqueada.original, original);
  }
  const causa = (entrada) => consumir(entrada).cause;
  assert.equal(causa({ kind: 'absent' }), 'absent');
  assert.equal(consumir({ kind: 'invalid', original: ' lima ' }).original, ' lima ');
  // Verificada, pero el canónico no sirve aquí o no cuadra con el original.
  assert.equal(causa(verificada('-05:00')), 'invalid');
  assert.equal(causa(verificada('America/LIma')), 'invalid');
  assert.equal(causa(verificada('Marte/Olimpo')), 'unsupported');
  assert.equal(causa({ kind: 'verified', original: 'America/Lima', canonical: 'Europe/Madrid' }), 'inconsistent');
  // Lo que no tiene exactamente la forma candidata es inválido, nunca una zona.
  for (const rara of [null, undefined, 'America/Lima', 5, [], ['verified'], {}, { zone: 'America/Lima' },
    { kind: 'Verified', original: 'America/Lima', canonical: 'America/Lima' },
    { kind: 'verified', original: 'America/Lima' }, { kind: 'verified', original: '', canonical: 'America/Lima' },
    { kind: 'verified', original: 'America/Lima', canonical: 5 }, { kind: 'runtime_resolved', original: 'America/Lima' }]) {
    assert.equal(causa(rara), 'invalid', JSON.stringify(rara));
  }
});

test('51 · ausente, inválida o sin verificar bloquean: ni Lima ni la clave candidata de CORE', () => {
  for (const entrada of [{ kind: 'absent' }, { kind: 'invalid', original: 'x' },
    { kind: 'runtime_resolved', original: 'America/Lima', runtimeId: 'America/Lima' }, { zone: 'America/Lima' }]) {
    const resuelta = zonas.resolveInstallationZone(entrada);
    assert.equal(resuelta.origin, 'unavailable', JSON.stringify(entrada));
    assert.equal(resuelta.zone, null);
    assert.ok(resuelta.reason.length > 0);
    assert.deepEqual(reportZonesFor(resuelta), []);
  }
  // El respaldo del negocio solo existe con la costura sin conectar.
  assert.equal(zonas.installationZoneObservation, null);
  assert.equal(zonas.resolveInstallationZone().origin, 'historical_fallback');
  const consumidor = codigo(fuente('src/modules/reporting/services/zoneObservation.ts'));
  assert.doesNotMatch(consumidor, /America\/Lima|BUSINESS_DEFAULT_ZONE/);
  for (const archivo of archivos()) {
    assert.doesNotMatch(codigo(readFileSync(archivo, 'utf8')), /timezone_iana/, archivo);
  }
});

test('52 · sin conexión con CORE o con la zona ausente, nada nuevo que exija zona verificada se habilita', async () => {
  const sinConexion = zonas.resolveInstallationZone(null);
  const ausente = zonas.resolveInstallationZone({ kind: 'absent' });
  const sinVerificar = zonas.resolveInstallationZone({ kind: 'runtime_resolved', original: 'America/Lima', runtimeId: 'America/Lima' });
  // Sin conexión, Lima es un respaldo histórico: sirve a lo que ya existía, no prueba autoridad.
  assert.equal(sinConexion.origin, 'historical_fallback');
  assert.equal(sinConexion.zone, 'America/Lima');
  assert.deepEqual([...reportZonesFor(sinConexion)], ['America/Lima', 'UTC']);
  // Ninguno de los tres es una zona verificada; ausente y sin verificar, ni siquiera zona.
  for (const instalacion of [sinConexion, ausente, sinVerificar]) {
    assert.equal(zonas.verifiedInstallationZone(instalacion), null, instalacion.origin);
  }
  assert.equal(ausente.zone, null);
  assert.equal(zonas.verifiedInstallationZone(INSTALACION_VERIFICADA), 'America/Lima');
  // La exportación es nueva: con auditoría que confirma, sigue sin archivo y sin auditar.
  for (const instalacion of [sinConexion, ausente, sinVerificar]) {
    const { registros, port } = auditoria();
    const resultado = await exportarConZona({ zone: 'America/Lima', baseline: respuesta() }, port, undefined, instalacion);
    assert.equal(resultado.kind, 'blocked', instalacion.origin);
    assert.match(resultado.reason, /zona horaria de la instalación tiene que estar verificada/);
    assert.equal('content' in resultado, false);
    assert.equal(registros.length, 0);
  }
  // Hoy la instalación es la del respaldo: la exportación por defecto tampoco tendría zona.
  const { INSTALLATION } = await import('../src/modules/reporting/services/period.ts');
  assert.equal(zonas.verifiedInstallationZone(INSTALLATION), null);
  assert.equal((await exportarConZona({ zone: 'America/Lima', baseline: respuesta() }, auditoria().port)).kind, 'blocked');
});

test('53 · la zona del archivo es la de la instalación verificada que autoriza la exportación, o UTC', async () => {
  // Verificada Bogotá: Lima sigue en la lista calculada al cargar, pero no es de esta instalación.
  const bogota = zonas.resolveInstallationZone({ kind: 'verified', original: 'America/Bogota', canonical: 'America/Bogota' });
  for (const zona of ['America/Lima', 'Europe/Madrid', 'america/bogota', '', null]) {
    const { registros, port } = auditoria();
    const resultado = await exportarConZona({ zone: zona, baseline: respuesta() }, port, undefined, bogota);
    assert.equal(resultado.kind, 'blocked', String(zona));
    assert.match(resultado.reason, /no es la de la instalación ni UTC/);
    assert.equal('content' in resultado, false);
    assert.equal(registros.length, 0);
  }
  // UTC sí pertenece a la instalación verificada, y lo auditado es esa zona.
  const { registros, port } = auditoria();
  const utc = await exportarConZona({ zone: 'UTC', baseline: respuesta() }, port, undefined, bogota);
  assert.equal(utc.kind, 'ready');
  assert.equal(registros.length, 1);
  assert.equal(registros[0].zone, 'UTC');
});

test('54 · con una zona verificada que no es la calculada al cargar, el archivo sale en esa zona y no en Lima', async () => {
  const bogota = zonas.resolveInstallationZone({ kind: 'verified', original: 'America/Bogota', canonical: 'America/Bogota' });
  // El generador por su cuenta sigue cerrado: Bogotá no está en la lista calculada al cargar.
  assert.equal(buildOrderIndicatorsCsv({ zone: 'America/Bogota', baseline: respuesta() }).kind, 'unavailable');
  assert.equal(buildOrderIndicatorsCsv({ zone: 'America/Lima', baseline: respuesta() }, ['America/Bogota', 'UTC']).kind, 'unavailable');
  const { registros, port } = auditoria();
  const resultado = await exportarConZona({ zone: 'America/Bogota', baseline: respuesta() }, port, undefined, bogota);
  assert.equal(resultado.kind, 'ready');
  const filas = resultado.content.replace(/^﻿/, '').split('\r\n').filter(Boolean).slice(1);
  assert.ok(filas.length > 0);
  for (const fila of filas) assert.equal(fila.split(',')[3], 'America/Bogota');
  assert.equal(registros.length, 1);
  assert.equal(registros[0].zone, 'America/Bogota');
  // Sin puerto de auditoría, ni con la zona verificada sale archivo.
  assert.equal((await exportarConZona({ zone: 'America/Bogota', baseline: respuesta() }, null, undefined, bogota)).kind, 'blocked');
});

const costura = await import('../src/modules/reporting/services/coreSeam.ts');

test('55 · la zona inicial aprobada de CORE sirve para consultar, pero no habilita la exportación', async () => {
  // «Sin conexión» es el mismo null de hoy; una lectura que falla bloquea, no supone Lima.
  assert.equal(costura.installationZoneFromResponse({ kind: 'unconnected', original: null }), null);
  const fallida = zonas.resolveInstallationZone(await costura.readInstallationZone(async () => { throw new Error('503'); }));
  assert.equal(fallida.origin, 'unavailable');
  assert.equal(fallida.zone, null);
  // La respuesta del servidor tal cual: inicial aprobada, con los campos de los demás estados nulos.
  const inicial = zonas.resolveInstallationZone(costura.installationZoneFromResponse(
    { kind: 'approved_initial', original: null, canonical: null, runtimeId: null, zone: 'America/Lima' }));
  assert.deepEqual(inicial, { zone: 'America/Lima', origin: 'approved_initial' });
  assert.deepEqual([...reportZonesFor(inicial)], ['America/Lima', 'UTC']);
  assert.equal(zonas.verifiedInstallationZone(inicial), null);
  const { registros, port } = auditoria();
  const resultado = await exportarConZona({ zone: 'America/Lima', baseline: respuesta() }, port, undefined, inicial);
  assert.equal(resultado.kind, 'blocked');
  assert.equal(registros.length, 0);
  // Mal escrita, desconocida o sin texto: la inicial bloquea como cualquier otra.
  for (const zone of ['america/lima', 'Mars/Olympus_Mons', '-05:00', '', null]) {
    assert.equal(zonas.resolveInstallationZone({ kind: 'approved_initial', zone }).zone, null, String(zone));
  }
});

test('56 · el puerto de auditoría por HTTP solo confirma con recorded y el identificador de CORE', async () => {
  const llamadas = [];
  const puerto = (respuesta) => costura.exportAuditPortOver(async (call) => { llamadas.push(call); return respuesta(); });
  const bogota = zonas.resolveInstallationZone({ kind: 'verified', original: 'America/Bogota', canonical: 'America/Bogota' });
  const exportar = (respuestaServidor) => exportarConZona({ zone: 'UTC', baseline: respuesta() }, puerto(respuestaServidor), 1_000, bogota);

  const listo = await exportar(() => ({ decision: 'recorded', exportId: '0199a6f0-0000-7000-8000-000000000001' }));
  assert.equal(listo.kind, 'ready');
  // A CORE solo viajan la zona y las filas: ni períodos, ni nombre de archivo, ni contenido.
  assert.deepEqual(llamadas.at(-1), { zone: 'UTC', dataRows: 11 });
  assert.equal((await exportar(() => ({ decision: 'denied', exportId: null }))).kind, 'audit_denied');
  const casiConfirmaciones = [
    () => ({ decision: 'recorded' }), () => ({ decision: 'recorded', exportId: '' }), () => ({ decision: 'Recorded', exportId: 'x' }),
    () => 'recorded', () => null, () => { throw new Error('409 zona sin verificar'); }, () => { throw new Error('503 auditoría'); },
  ];
  for (const casi of casiConfirmaciones) {
    const resultado = await exportar(casi);
    assert.equal(resultado.kind, 'audit_failed', String(casi));
    assert.equal('content' in resultado, false);
  }
  // Los adaptadores existen, pero los puntos de conexión siguen sin conectar.
  assert.equal(exportAuditPort, null);
  assert.equal(zonas.installationZoneObservation, null);
  const servicio = codigo(fuente('src/modules/reporting/services/reporting.ts'));
  assert.match(servicio, /'\/admin\/reporting\/installation-zone'/);
  assert.match(servicio, /'\/admin\/reporting\/orders\/export-audit'/);
});
