import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import {
  formatDateFilter,
  parseDateFilter,
} from '../src/shared/dateFilter.ts';
import {
  roleFormLabel,
  roleLabel,
} from '../src/session/roleVocabulary.ts';
import { moduleDisplayName } from '../src/capabilities/moduleDisplayName.ts';

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function source(path) {
  return readFileSync(resolve(frontend, path), 'utf8');
}

test('H-20 tiene un único nombre visible por rol', () => {
  assert.equal(roleLabel('super_admin'), 'Administrador principal');
  assert.equal(roleLabel('admin'), 'Administrador');
  assert.equal(roleLabel('editor'), 'Editor');
});

test('H-20 no permite volver a Principal', () => {
  const vocabulary = source('src/session/roleVocabulary.ts');
  const users = source('src/modules/core/pages/UsersPage.tsx');

  assert.equal(vocabulary.includes('shortLabel'), false);
  assert.equal(users.includes('roleShortLabel'), false);
  assert.equal(users.includes("'Principal'"), false);
});

test('H-20 sus tres consumidores no tienen ROLE_LABELS local', () => {
  const shell = source('src/layout/AdminShell.tsx');
  const users = source('src/modules/core/pages/UsersPage.tsx');
  const form = source('src/modules/core/components/UserForm.tsx');

  assert.equal(shell.includes('ROLE_LABELS'), false);
  assert.equal(users.includes('ROLE_LABELS'), false);
  assert.equal(form.includes('ROLE_LABELS'), false);

  assert.match(shell, /roleLabel\(user\.role\)/);
  assert.match(users, /roleLabel\(user\.role\)/);
  assert.match(form, /roleFormLabel\(value\)/);
});

test('H-20 UserForm conserva las descripciones actuales', () => {
  assert.equal(
    roleFormLabel('super_admin'),
    'Administrador principal — gestiona usuarios y módulos',
  );
  assert.equal(
    roleFormLabel('admin'),
    'Administrador — configura el negocio',
  );
  assert.equal(
    roleFormLabel('editor'),
    'Editor — edita contenido y sube archivos',
  );
});

test('H-19 mantiene Usuarios y cambia el alta', () => {
  const routes = source('src/modules/core/routes.tsx');
  const users = source('src/modules/core/pages/UsersPage.tsx');
  const form = source('src/modules/core/components/UserForm.tsx');

  assert.match(routes, /label: 'Usuarios'/);
  assert.match(users, /Nuevo usuario/);
  assert.match(form, /Nuevo usuario/);
  assert.match(form, /Crear usuario/);

  assert.equal(users.includes('Nuevo administrador'), false);
  assert.equal(form.includes('Nuevo administrador'), false);
  assert.equal(form.includes('Crear administrador'), false);
});

test('H-15 traduce code con displayName de AdminModule', () => {
  const realModules = [
    { code: 'catalog', displayName: 'Catálogo' },
  ];

  assert.equal(
    moduleDisplayName(realModules, 'catalog'),
    'Catálogo',
  );

  assert.equal(
    moduleDisplayName(realModules, 'missing'),
    undefined,
  );
});

test('H-15 Media y Auditoría usan la misma traducción', () => {
  const media = source('src/modules/core/pages/MediaPage.tsx');
  const audit = source('src/modules/core/pages/AuditPage.tsx');

  assert.match(media, /modulesService\.list/);
  assert.match(audit, /modulesService\.list/);

  assert.match(
    media,
    /moduleDisplayName\(moduleDisplayModules, code\)/,
  );

  assert.match(
    audit,
    /moduleDisplayName\(moduleDisplayModules, item\.code\)/,
  );

  assert.equal(audit.includes('placeholder="core, catalog…"'), false);
});

test('H-14 interpreta 10/09/2026 como 10 de septiembre', () => {
  assert.deepEqual(
    parseDateFilter('10/09/2026', 'start'),
    {
      kind: 'valid',
      apiValue: '2026-09-10T00:00:00Z',
    },
  );

  assert.deepEqual(
    parseDateFilter('10/09/2026', 'end'),
    {
      kind: 'valid',
      apiValue: '2026-09-10T23:59:59Z',
    },
  );

  assert.equal(
    formatDateFilter('2026-09-10T00:00:00Z'),
    '10/09/2026',
  );
});

test('H-14 fecha inválida no genera valor API', () => {
  assert.deepEqual(
    parseDateFilter('31/02/2026', 'start'),
    { kind: 'invalid' },
  );

  assert.deepEqual(
    parseDateFilter('2026-09-10', 'start'),
    { kind: 'invalid' },
  );

  const field = source('src/shared/ui/DateFilterField.tsx');

  assert.match(
    field,
    /if \(parsed\.kind === 'invalid'\)[\s\S]*?return;/,
  );
});

test('H-14 vacío significa sin filtro', () => {
  assert.deepEqual(parseDateFilter('', 'start'), { kind: 'empty' });
  assert.deepEqual(parseDateFilter('   ', 'end'), { kind: 'empty' });
});

test('H-14 Media y Audit comparten DateFilterField', () => {
  const media = source('src/modules/core/pages/MediaPage.tsx');
  const audit = source('src/modules/core/pages/AuditPage.tsx');

  assert.equal(media.includes('type="date"'), false);
  assert.equal(audit.includes('type="date"'), false);
  assert.match(media, /DateFilterField/);
  assert.match(audit, /DateFilterField/);
});

test('H-12 Inicio usa Setting.description', () => {
  const home = source('src/platform/HomePage.tsx');

  assert.match(home, /settingsService\.list\(\)/);
  assert.match(home, /setting\.needsSetup/);
  assert.match(
    home,
    /setting\.description\?\.trim\(\) \|\| setting\.key/,
  );
  assert.equal(home.includes('Object.entries(settings.all)'), false);
});

test('H-13 Inicio refleja el producto actual', () => {
  const home = source('src/platform/HomePage.tsx');

  assert.match(home, /Administración disponible/);
  assert.equal(home.includes('falta la interfaz'), false);
  assert.equal(home.includes('siguiente entrega'), false);
});

test('H-18 CTA administrativo depende de sesión', () => {
  const publicSite = source('src/platform/PublicSite.tsx');

  assert.match(
    publicSite,
    /const \{ isAuthenticated \} = useSession\(\)/,
  );

  assert.match(
    publicSite,
    /isAuthenticated[\s\S]*Ir al panel de administración/,
  );
});
