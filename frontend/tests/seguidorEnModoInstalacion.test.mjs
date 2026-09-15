import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import test from 'node:test';

/**
 * El seguidor de conexión, cuando el servidor está en MODO INSTALACIÓN.
 *
 * Tras un fallo de red, el cliente deja de enviar peticiones hasta que el
 * seguidor comprueba que el servidor ha vuelto (`shared/http/connection.ts`).
 * Esa comprobación sondea `/api/capabilities`, que en modo instalación **no
 * existe**: el servidor solo monta `/api/setup*`. Medido contra `b11cd8c`: el
 * seguidor sondea, recibe 404, y sigue esperando; el asistente de instalación
 * queda bloqueado —«El servidor se está reiniciando. La operación no se
 * envió.»— hasta recargar la página, con la API ya de vuelta.
 *
 * Las dos pruebas usan el código real del cliente, con `fetch` simulado. La
 * de modo normal es el contraste: si también fallara, el rojo de la otra
 * sería del arnés, no del seguidor.
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

const { http } = await import('../src/shared/http/client.ts');
const { connection } = await import('../src/shared/http/connection.ts');

/**
 * Un servidor simulado: caído hasta que se le levanta, y después respondiendo
 * como un host en el modo indicado. Devuelve el registro de lo que salió.
 */
function servidor(modo) {
  const salidas = [];
  let arriba = false;

  globalThis.fetch = async (url, init = {}) => {
    const ruta = new URL(String(url), 'http://x').pathname;
    salidas.push(`${init.method ?? 'GET'} ${ruta}`);

    if (!arriba) {
      throw new TypeError('fetch failed');
    }

    if (ruta === '/api/capabilities') {
      return modo === 'instalacion'
        ? new Response('', { status: 404 })
        : new Response('{"modules":[]}', { status: 200, headers: { 'Content-Type': 'application/json' } });
    }

    return new Response('{}', { status: 201, headers: { 'Content-Type': 'application/json' } });
  };

  return { salidas, levantar: () => { arriba = true; } };
}

/** Espera hasta `ms` a que el seguidor vuelva a `online`. */
async function esperarOnline(ms) {
  const hasta = Date.now() + ms;
  while (Date.now() < hasta && connection.current.state !== 'online') {
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  return connection.current.state;
}

async function unCorteYVuelta(modo) {
  connection.reset();
  const api = servidor(modo);

  await assert.rejects(http.post('/setup', {}), { kind: 'Network' });
  assert.equal(connection.current.state, 'reconnecting');

  api.levantar();
  const estado = await esperarOnline(8000);

  let segundo = null;
  try {
    await http.post('/setup', {});
    segundo = 'enviado';
  } catch (error) {
    segundo = `${error.kind}: ${error.message}`;
  } finally {
    connection.reset();
  }

  return { estado, segundo, posts: api.salidas.filter((s) => s === 'POST /api/setup').length };
}

test('contraste: en modo normal, tras un corte, el seguidor se recupera y la petición sale', async () => {
  const { estado, segundo, posts } = await unCorteYVuelta('normal');

  assert.equal(estado, 'online');
  assert.equal(segundo, 'enviado');
  assert.equal(posts, 2);
});

test('en modo instalación, tras un corte, en cuanto la API vuelve la petición sale', async () => {
  const { estado, segundo, posts } = await unCorteYVuelta('instalacion');

  assert.equal(estado, 'online', 'el seguidor sigue esperando a /api/capabilities, que en modo instalación no existe');
  assert.equal(segundo, 'enviado', `el segundo POST no salió: ${segundo}`);
  assert.equal(posts, 2);
});
