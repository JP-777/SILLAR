/**
 * Composición EXCLUSIVA DE PRUEBAS, opt-in: conecta el `httpExportAuditPort`
 * real —el cliente HTTP de verdad, con un `fetch` falso— a la exportación.
 * No la importa ningún archivo de `src/`, y sin `SILLAR_COMPOSICION_PRUEBAS=1`
 * no arranca. En la aplicación `exportAuditPort` sigue en null.
 *
 * Entrada: ESCENARIO (lo que responde el servidor) e INSTALACION (la zona).
 * Salida: una línea JSON con el resultado y las peticiones que salieron.
 */
import { register } from 'node:module';

if (process.env.SILLAR_COMPOSICION_PRUEBAS !== '1') {
  console.error('Composición de pruebas: actívala con SILLAR_COMPOSICION_PRUEBAS=1.');
  process.exit(2);
}
register('./resolverSinExtension.mjs', import.meta.url);

const json = (status, body) => new Response(JSON.stringify(body), {
  status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
});
const RESPUESTAS = {
  conflicto: () => json(409, { status: 409, title: 'Para exportar, la zona horaria de la instalación tiene que estar verificada, y todavía no lo está.' }),
  no_disponible: () => json(503, { status: 503, title: 'No se pudo registrar la exportación en la auditoría, así que no se generó el archivo.' }),
  denegada: () => json(200, { decision: 'denied', exportId: null }),
  sin_identificador: () => json(200, { decision: 'recorded', exportId: null }),
  red_caida: () => { throw new TypeError('fetch failed'); },
  registrada: () => json(200, { decision: 'recorded', exportId: '0199a6f0-0000-7000-8000-000000000001' }),
};
const peticiones = [];
globalThis.fetch = async (url, init) => {
  peticiones.push({ url, method: init.method, csrf: init.headers.get('X-CSRF-Token'), body: init.body ? JSON.parse(init.body) : null });
  return RESPUESTAS[process.env.ESCENARIO]();
};

const servicios = (nombre) => new URL(`../../src/modules/reporting/services/${nombre}`, import.meta.url).href;
const { http } = await import(new URL('../../src/shared/http/client.ts', import.meta.url).href);
const { httpExportAuditPort } = await import(servicios('reporting.ts'));
const { prepareOrderIndicatorsExport } = await import(servicios('reportExport.ts'));
const { resolveInstallationZone } = await import(servicios('installationZone.ts'));
const { INSTALLATION } = await import(servicios('period.ts'));

http.setCsrfToken('token-de-prueba');
const INSTALACIONES = {
  hoy: INSTALLATION,
  inicial: resolveInstallationZone({ kind: 'approved_initial', zone: 'America/Lima' }),
  configurada: resolveInstallationZone({ kind: 'runtime_resolved', original: 'America/Lima', runtimeId: 'America/Lima' }),
  // Fijación de prueba: CORE no produce ninguna verificada. Solo sirve para llegar al puerto.
  verificada_de_prueba: resolveInstallationZone({ kind: 'verified', original: 'America/Lima', canonical: 'America/Lima' }),
};
const baseline = {
  status: 'with_data', startInclusive: '2026-10-01T05:00:00.000Z', endExclusive: '2026-10-08T05:00:00.000Z',
  observedAtUtc: '2026-10-08T06:00:00.000Z', i01CreatedOrders: 3,
  i03CurrentStatusCounts: [{ status: 'pending_payment', count: 2 }, { status: 'delivered', count: 1 }],
  i04CurrentlyExpiredOrders: 0, activeOrders: 3, inactiveOrders: 0,
};
const resultado = await prepareOrderIndicatorsExport(
  { zone: 'America/Lima', baseline }, httpExportAuditPort, 2_000, INSTALACIONES[process.env.INSTALACION]);
console.log(JSON.stringify({ kind: resultado.kind, conArchivo: 'content' in resultado, peticiones }));
// Con la red caída, el cliente arranca el sondeo de reconexión: se termina aquí.
process.exit(0);
