import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { reorderedIds } from '../src/modules/services/state/reorder.ts';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = (path) => readFileSync(resolve(root, path), 'utf8');

test('S3 cubre carga, vacío, datos, error y reordenamiento accesible', () => {
  const page = source('src/modules/services/pages/ServicesAdminPage.tsx');
  assert.match(page, /loading=\{showLoading\}/);
  assert.match(page, /Todavía no hay servicios/);
  assert.match(page, /state\.status === 'error'/);
  assert.match(page, /formatServicePrice/);
  assert.match(page, /aria-label=\{`Subir/);
  assert.deepEqual(reorderedIds([{ id: 1 }, { id: 2 }, { id: 3 }], 2, 0), [3, 1, 2]);
  assert.throws(() => reorderedIds([{ id: 1 }], 0, 1), RangeError);
});

test('S4 conserva datos y muestra validación y conflictos reales sin concurrencia ficticia', () => {
  const form = source('src/modules/services/components/ServiceForm.tsx');
  const allServices = ['src/modules/services/components/ServiceForm.tsx', 'src/modules/services/pages/ServicesAdminPage.tsx', 'src/modules/services/routes.tsx'].map(source).join('\n');
  assert.match(form, /fieldErrors/);
  assert.match(form, /slug/);
  assert.match(form, /imageAltText/);
  assert.match(form, /setFailure\(describe/);
  assert.doesNotMatch(allServices, /otra persona editó|ver versión actual|ETag|rowVersion|concurrenc/i);
});

test('la navegación y las rutas dependen de la capacidad services', () => {
  const routes = source('src/app/routes.tsx');
  const navigation = source('src/layout/navigation.ts');
  assert.match(routes, /has\('services'\) && servicesPublicRoutes/);
  assert.match(routes, /has\('services'\) && servicesAdminRoutes/);
  assert.match(navigation, /servicesNavigation/);
});

test('la portada sitúa Servicios entre Catálogo y CRM y solo aporta con publicados', () => {
  const home = source('src/platform/homeSections.ts');
  const services = source('src/modules/services/routes.tsx');
  assert.match(home, /catalogHome, servicesHome, crmHome/);
  assert.match(services, /state\.data\.length > 0/);
  assert.match(services, /hasContent \? 'con-contenido' : 'vacio'/);
  assert.match(services, /return hasContent \?/);
});

test('la superficie pública cubre vacío, error recuperable, 404 y fallo de imagen', () => {
  const list = source('src/modules/services/pages/ServicesPage.tsx');
  const detail = source('src/modules/services/pages/ServicePage.tsx');
  const image = source('src/modules/services/components/ServiceImage.tsx');
  assert.match(list, /Todavía no hay servicios publicados/);
  assert.match(list, /Volver a intentar/);
  assert.match(detail, /Este servicio no está disponible/);
  assert.match(detail, /Volver a intentar/);
  assert.match(image, /onError/);
  assert.match(source('src/modules/services/services/services.ts'), /'Precio a consultar'/);
});
