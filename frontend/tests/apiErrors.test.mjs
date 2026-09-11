import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { ApiError } from '../src/shared/http/errors.ts';

/**
 * H-01: el `detail` útil de los errores del API llega a la pantalla, y lo que
 * no debe llegar no llega.
 *
 * Los casos no son inventados: cada `title` y cada `detail` de abajo es el que
 * escribe hoy un productor del backend, citado al lado. Si un productor cambia
 * su redacción, estas pruebas siguen valiendo —comprueban la composición, no
 * la frase—, pero la cita dice de dónde salió cada forma.
 */

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = (path) => readFileSync(resolve(frontend, path), 'utf8');

const GENERICA =
  'El servidor no pudo completar la operación. Si vuelve a pasar, revisa el registro del servidor.';

// --- La precedencia -------------------------------------------------------

test('la validación manda: el primer error de campo gana al título y al detalle', () => {
  const error = new ApiError(
    'ValidationFailed',
    400,
    'Los datos de la marca no son válidos.',
    'Un detalle que no debe tapar al campo.',
    { name: ['El nombre es obligatorio.'], slug: ['El slug ya existe.'] },
  );

  assert.equal(error.displayMessage, 'El nombre es obligatorio.');
});

test('sin errores de campo, una validación cae a la frase del servidor', () => {
  const error = new ApiError('ValidationFailed', 400, 'Los datos no son válidos.', null, {});

  assert.equal(error.displayMessage, 'Los datos no son válidos.');
});

// --- El detail llega ------------------------------------------------------

test('423: el detalle dice cuándo se puede volver a intentar, y llega', () => {
  // AuthEndpoints.cs: title + detail «Vuelve a intentarlo a partir de HH:mm.»
  const error = new ApiError(
    'Locked',
    423,
    'La cuenta está bloqueada temporalmente por varios intentos fallidos.',
    'Vuelve a intentarlo a partir de 14:35.',
  );

  assert.equal(
    error.displayMessage,
    'La cuenta está bloqueada temporalmente por varios intentos fallidos. '
      + 'Vuelve a intentarlo a partir de 14:35.',
  );
});

test('503 de la instalación: llega el detalle entero, con sus saltos de línea', () => {
  // SetupEndpoints.cs, SetupOutcome.MigrationsPending. El caso por el que
  // existe H-01: el título solo decía «No existe la tabla» y se quedaba fuera
  // la advertencia de comprobar la conexión ANTES de migrar.
  const detalle =
    "La aplicación consultó core.installation en la base 'sillar_x' del servidor db:5432, y esa tabla no está ahí.\n"
    + '\n'
    + 'Hay dos explicaciones y llevan a sitios distintos:\n'
    + '  1. Faltan las migraciones en esa base.\n'
    + '  2. La conexión apunta a una base distinta de la que esperabas.\n'
    + '\n'
    + 'Comprueba PRIMERO la conexión.';

  const error = new ApiError(
    'Unexpected',
    503,
    'No existe core.installation en la base sillar_x (db:5432).',
    detalle,
  );

  assert.ok(error.displayMessage.startsWith('No existe core.installation'));
  assert.ok(error.displayMessage.includes('Comprueba PRIMERO la conexión.'));
  assert.ok(error.displayMessage.includes('\n  1. Faltan las migraciones'), 'se perdieron los saltos de línea');
});

test('403 CSRF: título y detalle juntos, como ya hacía describe()', () => {
  // CsrfEndpointFilter.cs
  const error = new ApiError(
    'Forbidden',
    403,
    'Falta el token CSRF o no es válido.',
    'Envía la cabecera X-CSRF-Token con el token que devolvió el inicio de sesión.',
  );

  assert.equal(
    error.displayMessage,
    'Falta el token CSRF o no es válido. '
      + 'Envía la cabecera X-CSRF-Token con el token que devolvió el inicio de sesión.',
  );
});

test('sin detalle, la frase es solo el título: no se añade nada', () => {
  // BrandEndpoints.cs: los 409 llevan la frase en title y ningún detail.
  const error = new ApiError('Conflict', 409, 'Ya existe una marca con ese nombre.');

  assert.equal(error.displayMessage, 'Ya existe una marca con ese nombre.');
});

// --- Lo que no debe llegar ------------------------------------------------

test('413: el título llega y el detalle de Kestrel no', () => {
  // MediaEndpoints.cs: TooLarge(maxUploadBytes: exception.Message), el Message
  // de BadHttpRequestException. Texto del framework, en inglés.
  const error = new ApiError(
    'PayloadTooLarge',
    413,
    'El archivo pasa del tamaño máximo permitido.',
    'Request body too large. The max request body size is 10485760 bytes.',
  );

  assert.equal(error.displayMessage, 'El archivo pasa del tamaño máximo permitido.');
  assert.equal(error.displayMessage.includes('Request body'), false);
});

test('500 de producción: ni el título en inglés ni nada del servidor', () => {
  // UseExceptionHandler + AddProblemDetails: título genérico del framework.
  const error = new ApiError(
    'Unexpected',
    500,
    'An error occurred while processing your request.',
  );

  assert.equal(error.serverMessage, null);
  assert.equal(error.displayMessage, GENERICA);
});

test('500 con el mensaje de una excepción en el detalle: no llega nada de él', () => {
  // La forma que tiene un 500 fuera de producción: el tipo y el mensaje de la
  // excepción. Aquí con lo peor que podría traer: servidor, base y usuario.
  const error = new ApiError(
    'Unexpected',
    500,
    'Npgsql.PostgresException',
    '28P01: password authentication failed for user "sillar" (Host=db;Port=5432;Database=sillar_dev)',
  );

  assert.equal(error.displayMessage, GENERICA);

  for (const fuga of ['Npgsql', '28P01', 'password', 'Host=', 'sillar_dev']) {
    assert.equal(error.displayMessage.includes(fuga), false, `se filtró «${fuga}»`);
  }
});

test('el fallo de red conserva la frase que escribe el propio cliente', () => {
  // client.ts: new ApiError('Network', 0, 'No se pudo contactar con el servidor.')
  const error = new ApiError('Network', 0, 'No se pudo contactar con el servidor.');

  assert.equal(error.displayMessage, 'No se pudo contactar con el servidor.');
});

test('displayMessage nunca devuelve vacío ni «Ha ocurrido un error»', () => {
  const casos = [
    new ApiError('Unexpected', 500, ''),
    new ApiError('Unexpected', 500, 'x', 'y'),
    new ApiError('ValidationFailed', 400, 'T', null, { a: ['Campo mal.'] }),
    new ApiError('Conflict', 409, 'Choca.', 'Porque sí.'),
    new ApiError('Locked', 423, 'Bloqueada.', 'Hasta las 10:00.'),
    new ApiError('PayloadTooLarge', 413, 'Grande.', 'Request body too large.'),
  ];

  for (const error of casos) {
    assert.notEqual(error.displayMessage.trim(), '', `vacío para ${error.status}`);
    assert.equal(/ha ocurrido un error/i.test(error.displayMessage), false);
  }
});

// --- Un solo sitio compone la frase --------------------------------------

test('describe() pide la frase a displayMessage y no pega el detalle a mano', () => {
  const messages = source('src/shared/errors/messages.ts');

  // Antes cada rama elegía por su cuenta y el detail se perdía según la rama.
  assert.equal(
    /error\.detail/.test(messages),
    false,
    'messages.ts vuelve a leer error.detail por su cuenta',
  );

  for (const rama of ['Forbidden', 'ValidationFailed', 'Conflict', 'Locked', 'UnsupportedMediaType']) {
    const cuerpo = new RegExp(`case '${rama}':[\\s\\S]*?return \\{[\\s\\S]*?\\};`).exec(messages);
    assert.ok(cuerpo, `no encontré la rama ${rama}`);
    assert.match(cuerpo[0], /error\.displayMessage/, `la rama ${rama} no usa displayMessage`);
  }
});

test('describe() no enseña texto del servidor en 404 ni en el caso genérico', () => {
  const messages = source('src/shared/errors/messages.ts');

  const notFound = /case 'NotFound':[\s\S]*?return \{[\s\S]*?\};/.exec(messages);
  assert.ok(notFound);
  assert.equal(/error\.(message|displayMessage|serverMessage)/.test(notFound[0]), false);

  const generico = /default:[\s\S]*?return \{[\s\S]*?\};/.exec(messages);
  assert.ok(generico);
  assert.equal(/error\.(message|displayMessage|serverMessage)/.test(generico[0]), false);
});

test('la instalación conserva los saltos de línea del detalle', () => {
  const page = source('src/platform/SetupPage.tsx');
  const css = source('src/platform/platform.css');

  assert.match(page, /className="pf-server-message">\{error\}/);
  assert.match(css, /\.pf-server-message\s*\{[^}]*white-space:\s*pre-line/);
});

test('package.json expone la focal como comando permanente', () => {
  const pkg = JSON.parse(source('package.json'));

  assert.equal(pkg.scripts['test:api-errors'], 'node --test tests/apiErrors.test.mjs');
});
