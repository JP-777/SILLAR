import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const moduleDir = resolve(frontend, 'src/modules/tracking');
const source = (path) => readFileSync(resolve(frontend, path), 'utf8');
const moduleFiles = (dir = moduleDir) => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? moduleFiles(path) : [path];
});
const code = (text) => text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');

test('M06 solo aporta menú y ruta cuando la capacidad tracking está activa', () => {
  const routes = source('src/app/routes.tsx');
  const navigation = source('src/layout/navigation.ts');
  const trackingRoutes = source('src/modules/tracking/routes.tsx');

  assert.match(routes, /\{has\('tracking'\) && trackingRoutes\}/);
  assert.match(navigation, /trackingNavigation,/);
  assert.match(trackingRoutes, /moduleCode: 'tracking'/);
  assert.match(trackingRoutes, /path="seguimiento"/);
  assert.match(trackingRoutes, /minimum="editor"/);
});

test('M06 frontend usa las siete rutas ratificadas y ningún fetch directo', () => {
  const service = source('src/modules/tracking/services/tracking.ts');
  for (const fragment of [
    '/board',
    '/orders/${serviceOrderId}',
    '/priority',
    '/due',
    '/notes',
    '/notes/${trackingNoteId}',
    '/status',
  ]) assert.ok(service.includes(fragment), fragment);

  for (const file of moduleFiles().filter((path) => /\.(tsx?|css)$/.test(path))) {
    assert.doesNotMatch(code(readFileSync(file, 'utf8')), /\bfetch\s*\(/, relative(frontend, file));
  }
});

test('M06 no copia el vocabulario de estados de M05b', () => {
  for (const file of moduleFiles().filter((path) => /\.tsx?$/.test(path))) {
    assert.doesNotMatch(
      code(readFileSync(file, 'utf8')),
      /['"](?:received|in_progress|completed|cancelled)['"]/,
      relative(frontend, file),
    );
  }
});

test('A2 reordena por orderedPeerIds y A5 conserva expectedStatus visto', () => {
  const page = source('src/modules/tracking/pages/TrackingPage.tsx');
  assert.match(page, /orderedPeerIds/);
  assert.match(page, /expectedStatus: pending\.card\.currentStatus/);
  assert.match(page, /isApiError\(error, 'Conflict'\)/);
  assert.match(page, /await refreshOpenBoard\(\)/);
  assert.equal(page.includes('reintentar automáticamente'), false);
});

test('A3 separa solo lectura de M05b y datos editables de M06', () => {
  const page = source('src/modules/tracking/pages/TrackingPage.tsx');
  assert.match(page, /Orden de servicio · solo lectura/);
  assert.match(page, /Datos editables de seguimiento/);
  assert.match(page, /Historial autoritativo de M05b/);
  assert.match(page, /Notas de seguimiento/);
  assert.match(page, /no son las notas internas de la orden/);
});

test('dar de baja una nota se ofrece solo desde admin y con confirmación nombrada', () => {
  const page = source('src/modules/tracking/pages/TrackingPage.tsx');
  assert.match(page, /hasRole\('admin'\)/);
  assert.match(page, /confirmLabel="Dar de baja nota"/);
  assert.doesNotMatch(code(page), />\s*Aceptar\s*</);
});

test('ningún archivo de M06 escribe un color literal', () => {
  for (const file of moduleFiles()) {
    assert.doesNotMatch(
      readFileSync(file, 'utf8'),
      /#[0-9a-f]{3,8}\b|\brgba?\(|\bhsla?\(/i,
      relative(frontend, file),
    );
  }
});
