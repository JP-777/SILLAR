/** Composición aislada del cliente HTTP real: prueba la descarga servidor y sus rechazos. */
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
  conflicto: () => json(409, { status: 409, title: 'Zona sin verificar.' }),
  no_disponible: () => json(503, { status: 503, title: 'Auditoría no disponible.' }),
  denegada: () => json(403, { status: 403, title: 'Sin permiso para exportar.' }),
  recibo_sin_archivo: () => json(200, { decision: 'recorded', exportId: '0199a6f0-0000-7000-8000-000000000001' }),
  texto_ajeno: () => new Response('<html>error</html>', { status: 200 }),
  red_caida: () => { throw new TypeError('fetch failed'); },
  registrada: () => new Response('\uFEFFCohorte,Inicio\r\nA,2026-10-01\r\n', { status: 200, headers: { 'Content-Type': 'text/csv;charset=utf-8' } }),
};
const peticiones = [];
globalThis.fetch = async (url, init) => {
  peticiones.push({ url, method: init.method, csrf: init.headers.get('X-CSRF-Token'), body: init.body ? JSON.parse(init.body) : null });
  return RESPUESTAS[process.env.ESCENARIO]();
};

const servicios = (nombre) => new URL(`../../src/modules/reporting/services/${nombre}`, import.meta.url).href;
const { http } = await import(new URL('../../src/shared/http/client.ts', import.meta.url).href);
const { downloadOrderIndicatorsCsv } = await import(servicios('reporting.ts'));
http.setCsrfToken('token-de-prueba');
let resultado;
try {
  const content = await downloadOrderIndicatorsCsv('2026-10-01T05:00:00.000Z', '2026-10-08T05:00:00.000Z', 'America/Lima');
  resultado = { kind: 'ready', conArchivo: typeof content === 'string' };
} catch { resultado = { kind: 'failed', conArchivo: false }; }
console.log(JSON.stringify({ ...resultado, peticiones }));
// La red caída inicia sondeo de reconexión; el proceso aislado ya acreditó su resultado.
process.exit(0);
