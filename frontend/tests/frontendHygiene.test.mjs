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
import {
  hasPublicContact,
  publicContactFromSettings,
} from '../src/platform/publicContact.ts';

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

test('H-14 conserva el día visible incluso al cambiar timezone', () => {
  assert.deepEqual(
    parseDateFilter('11/09/2026', 'start'),
    {
      kind: 'valid',
      apiValue: '2026-09-11T00:00:00Z',
    },
  );

  assert.deepEqual(
    parseDateFilter('11/09/2026', 'end'),
    {
      kind: 'valid',
      apiValue: '2026-09-11T23:59:59Z',
    },
  );

  assert.deepEqual(
    parseDateFilter('01/10/2026', 'start'),
    {
      kind: 'valid',
      apiValue: '2026-10-01T00:00:00Z',
    },
  );

  assert.deepEqual(
    parseDateFilter('31/09/2026', 'start'),
    { kind: 'invalid' },
  );

  const originalTimezone = process.env.TZ;

  try {
    process.env.TZ = 'Pacific/Kiritimati';
    const east = parseDateFilter('11/09/2026', 'start');

    process.env.TZ = 'America/Los_Angeles';
    const west = parseDateFilter('11/09/2026', 'start');

    assert.deepEqual(
      east,
      {
        kind: 'valid',
        apiValue: '2026-09-11T00:00:00Z',
      },
    );

    assert.deepEqual(west, east);
  } finally {
    if (originalTimezone === undefined) {
      delete process.env.TZ;
    } else {
      process.env.TZ = originalTimezone;
    }
  }
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
  const homeSinComentarios = home.replace(/\/\*[\s\S]*?\*\//g, '');

  assert.match(home, /Administración disponible/);
  assert.equal(home.includes('falta la interfaz'), false);
  assert.equal(home.includes('siguiente entrega'), false);
  assert.equal(
    (homeSinComentarios.match(/módulos activos/gi) ?? []).length,
    1,
  );
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

test('§18 oculta valores vacíos y PENDIENTE_DEFINIR', () => {
  const contact = publicContactFromSettings({
    whatsapp_number: 'PENDIENTE_DEFINIR',
    contact_email: '   ',
    contact_phone: '',
    business_address: 'PENDIENTE_DEFINIR',
    business_reference: '',
    google_maps_url: '   ',
    business_hours: 'PENDIENTE_DEFINIR',
  });

  assert.deepEqual(contact, {
    whatsappNumber: null,
    whatsappUrl: null,
    contactEmail: null,
    contactPhone: null,
    businessAddress: null,
    businessReference: null,
    googleMapsUrl: null,
    businessHours: null,
  });
  assert.equal(hasPublicContact(contact), false);
});

test('§18 CORE convierte su WhatsApp principal en enlace público', () => {
  const contact = publicContactFromSettings({
    whatsapp_number: '+51 999 888 777',
    contact_email: ' contacto@negocio.pe ',
    contact_phone: '054 123456',
    business_address: 'Av. Ejemplo 123',
    business_reference: 'Frente a la plaza',
    google_maps_url: 'https://maps.example.test/local',
    business_hours: 'Lun–Sáb 09:00–18:00',
  });

  assert.equal(contact.whatsappNumber, '+51 999 888 777');
  assert.equal(contact.whatsappUrl, 'https://wa.me/51999888777');
  assert.equal(contact.contactEmail, 'contacto@negocio.pe');
  assert.equal(contact.contactPhone, '054 123456');
  assert.equal(contact.businessAddress, 'Av. Ejemplo 123');
  assert.equal(contact.businessReference, 'Frente a la plaza');
  assert.equal(contact.googleMapsUrl, 'https://maps.example.test/local');
  assert.equal(contact.businessHours, 'Lun–Sáb 09:00–18:00');
  assert.equal(hasPublicContact(contact), true);
});

test('§18 usa CORE en footer y contacto, y CMS no compite por WhatsApp', () => {
  const registry = source('src/platform/footerContributions.ts');
  const coreFooter = source('src/modules/core/coreFooter.tsx');
  const cmsFooter = source('src/modules/cms/cmsFooter.tsx');
  const contactPage = source('src/modules/crm/pages/ContactPage.tsx');

  assert.match(
    registry,
    /FOOTER_CONTRIBUTIONS[\s\S]*coreFooter[\s\S]*cmsFooter/,
  );
  assert.match(coreFooter, /moduleCode: 'core'/);
  assert.match(coreFooter, /PublicContactDetails/);

  assert.match(contactPage, /PublicContactDetails/);
  assert.match(contactPage, /Escribir por WhatsApp/);

  assert.match(
    cmsFooter,
    /contact\.whatsappUrl[\s\S]*enlace\.platform\.toLowerCase\(\) === 'whatsapp'/,
  );
});
