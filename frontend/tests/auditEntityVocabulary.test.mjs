import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  auditEntityLabel,
  composeAuditEntityVocabularies,
  reportAuditEntityConflictInDevelopment,
  visibleAuditEntityLabelsFrom,
} from '../src/platform/auditEntityVocabulary.ts';

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');

const expected = {
  admin_session: 'Sesión',
  admin_user: 'Usuario',
  banner: 'Banner',
  brand: 'Marca',
  category: 'Categoría',
  contact_message: 'Mensaje de contacto',
  customer: 'Cliente',
  customer_invitation: 'Invitación de cliente',
  email: 'Correo',
  featured_product: 'Producto destacado',
  featured_project: 'Trabajo destacado',
  installation: 'Instalación',
  media_asset: 'Archivo',
  module: 'Módulo',
  product: 'Producto',
  product_image: 'Imagen de producto',
  product_item: 'Presentación',
  promotion: 'Promoción',
  setting: 'Ajuste',
  social_link: 'Red social',
};

const surfaces = [
  {
    file: 'src/modules/core/auditEntityVocabulary.ts',
    exportName: 'coreAuditEntityVocabulary',
    moduleCode: 'core',
    count: 7,
  },
  {
    file: 'src/modules/catalog/routes.tsx',
    exportName: 'catalogAuditEntityVocabulary',
    moduleCode: 'catalog',
    count: 5,
  },
  {
    file: 'src/modules/cms/cmsHome.tsx',
    exportName: 'cmsAuditEntityVocabulary',
    moduleCode: 'cms',
    count: 5,
  },
  {
    file: 'src/modules/crm/routes.tsx',
    exportName: 'crmAuditEntityVocabulary',
    moduleCode: 'crm',
    count: 3,
  },
];

function source(path) {
  return readFileSync(resolve(frontend, path), 'utf8');
}

function contribution({ file, exportName, moduleCode, count }) {
  const text = source(file);
  const escaped = exportName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  const match = new RegExp(
    `export const ${escaped}: AuditEntityVocabulary = \\{` +
      `\\s*moduleCode: '([^']+)',` +
      `\\s*entries: \\[([\\s\\S]*?)\\n\\s*\\],` +
      `\\s*\\};`,
  ).exec(text);

  assert.ok(match, `${file} no declara ${exportName}`);
  assert.equal(match[1], moduleCode, `${exportName} cambió de moduleCode`);

  const entries = [
    ...match[2].matchAll(
      /^\s*\['([^']+)', '([^']*)'\],\s*$/gm,
    ),
  ].map((item) => [item[1], item[2]]);

  assert.equal(
    entries.length,
    count,
    `${exportName} cambió su número de entradas`,
  );

  return {
    moduleCode,
    entries,
  };
}

function contributions() {
  return surfaces.map(contribution);
}

test('CORE + M01 + M02 + M04 activos componen exactamente las veinte etiquetas', () => {
  const actual = visibleAuditEntityLabelsFrom(
    contributions(),
    () => true,
  );

  assert.equal(Object.keys(actual).length, 20);
  assert.deepEqual(actual, expected);
});

test('sin M02 social_link no queda registrado y degrada al código técnico', () => {
  const actual = visibleAuditEntityLabelsFrom(
    contributions(),
    (moduleCode) => moduleCode !== 'cms',
  );

  assert.equal('social_link' in actual, false);
  assert.equal(
    auditEntityLabel('social_link', actual),
    'social_link',
  );
});

test('sin M01 product_item no queda registrado y degrada al código técnico', () => {
  const actual = visibleAuditEntityLabelsFrom(
    contributions(),
    (moduleCode) => moduleCode !== 'catalog',
  );

  assert.equal('product_item' in actual, false);
  assert.equal(
    auditEntityLabel('product_item', actual),
    'product_item',
  );
});

test('solo CORE aporta exactamente sus siete etiquetas', () => {
  const actual = visibleAuditEntityLabelsFrom(
    contributions(),
    (moduleCode) => moduleCode === 'core',
  );

  assert.equal(Object.keys(actual).length, 7);
  assert.deepEqual(actual, {
    admin_session: 'Sesión',
    admin_user: 'Usuario',
    email: 'Correo',
    installation: 'Instalación',
    media_asset: 'Archivo',
    module: 'Módulo',
    setting: 'Ajuste',
  });
});

test('el registro pasa el predicado de actividad al compositor', () => {
  const registry = source('src/platform/auditEntityVocabularies.ts');

  assert.match(
    registry,
    /visibleAuditEntityLabelsFrom\(\s*AUDIT_ENTITY_VOCABULARIES,\s*isActive,\s*reportAuditEntityConflict,\s*\)/,
  );
});

test('el registro contiene las cuatro contribuciones y ninguna etiqueta concreta', () => {
  const registry = source('src/platform/auditEntityVocabularies.ts');

  const body =
    /AUDIT_ENTITY_VOCABULARIES:[\s\S]*?=\s*\[([\s\S]*?)\];/.exec(
      registry,
    );

  assert.ok(body, 'no se encontró AUDIT_ENTITY_VOCABULARIES');

  const listed = [
    ...body[1].matchAll(
      /\b([a-zA-Z]+AuditEntityVocabulary)\b/g,
    ),
  ].map((match) => match[1]);

  assert.deepEqual(listed, [
    'coreAuditEntityVocabulary',
    'catalogAuditEntityVocabulary',
    'cmsAuditEntityVocabulary',
    'crmAuditEntityVocabulary',
  ]);

  for (const label of Object.values(expected)) {
    assert.equal(
      registry.includes(`'${label}'`),
      false,
      `plataforma conoce la etiqueta concreta "${label}"`,
    );
  }
});

test('un entityType desconocido degrada al código técnico exacto', () => {
  assert.equal(
    auditEntityLabel('tipo_que_nadie_declaro', expected),
    'tipo_que_nadie_declaro',
  );
});

test('duplicado entre módulos no tiene ganador y reporta ambas procedencias', () => {
  const conflicts = [];

  const labels = composeAuditEntityVocabularies(
    [
      {
        moduleCode: 'modulo_a',
        entries: [['repetido', 'Primero']],
      },
      {
        moduleCode: 'modulo_b',
        entries: [['repetido', 'Segundo']],
      },
    ],
    (conflict) => conflicts.push(conflict),
  );

  assert.equal('repetido' in labels, false);
  assert.equal(
    auditEntityLabel('repetido', labels),
    'repetido',
  );

  assert.equal(conflicts.length, 1);
  assert.equal(conflicts[0].entityType, 'repetido');
  assert.deepEqual(
    conflicts[0].declarations.map((item) => item.moduleCode),
    ['modulo_a', 'modulo_b'],
  );
});

test('duplicado dentro de una contribución tampoco tiene ganador', () => {
  const conflicts = [];

  const labels = composeAuditEntityVocabularies(
    [
      {
        moduleCode: 'modulo_a',
        entries: [
          ['repetido', 'Primero'],
          ['repetido', 'Segundo'],
        ],
      },
    ],
    (conflict) => conflicts.push(conflict),
  );

  assert.equal('repetido' in labels, false);
  assert.equal(
    auditEntityLabel('repetido', labels),
    'repetido',
  );

  assert.equal(conflicts.length, 1);
  assert.equal(conflicts[0].entityType, 'repetido');
  assert.deepEqual(
    conflicts[0].declarations.map((item) => item.moduleCode),
    ['modulo_a', 'modulo_a'],
  );
  assert.deepEqual(
    conflicts[0].declarations.map((item) => item.label),
    ['Primero', 'Segundo'],
  );
});

test('la señal de duplicado existe en desarrollo e identifica clave y módulos', () => {
  const messages = [];
  const conflict = {
    entityType: 'repetido',
    declarations: [
      { moduleCode: 'modulo_a', label: 'Uno' },
      { moduleCode: 'modulo_b', label: 'Dos' },
    ],
  };

  reportAuditEntityConflictInDevelopment(
    true,
    conflict,
    (message) => messages.push(message),
  );

  assert.equal(messages.length, 1);
  assert.match(messages[0], /repetido/);
  assert.match(messages[0], /modulo_a/);
  assert.match(messages[0], /modulo_b/);
});

test('en producción una inconsistencia no emite señal ni rompe composición', () => {
  const messages = [];
  const conflict = {
    entityType: 'repetido',
    declarations: [
      { moduleCode: 'modulo_a', label: 'Uno' },
      { moduleCode: 'modulo_b', label: 'Dos' },
    ],
  };

  assert.doesNotThrow(() =>
    reportAuditEntityConflictInDevelopment(
      false,
      conflict,
      (message) => messages.push(message),
    ),
  );

  assert.deepEqual(messages, []);
});

test('el registro conecta la señal de conflicto exclusivamente a import.meta.env.DEV', () => {
  const registry = source('src/platform/auditEntityVocabularies.ts');

  assert.match(
    registry,
    /reportAuditEntityConflictInDevelopment\(import\.meta\.env\.DEV,\s*conflict\)/,
  );
});

test('AuditPage consulta la plataforma con useCapability().has', () => {
  const page = source('src/modules/core/pages/AuditPage.tsx');

  assert.equal(/\bENTIDADES\b/.test(page), false);

  assert.match(
    page,
    /visibleAuditEntityLabels\(useCapability\(\)\.has\)/,
  );

  assert.match(page, /auditEntityLabel/);

  for (const [entityType, label] of Object.entries(expected)) {
    assert.equal(
      page.includes(`${entityType}: '${label}'`),
      false,
      `AuditPage todavía conoce ${entityType} → ${label}`,
    );
  }

  for (const contributionName of [
    'coreAuditEntityVocabulary',
    'catalogAuditEntityVocabulary',
    'cmsAuditEntityVocabulary',
    'crmAuditEntityVocabulary',
  ]) {
    assert.equal(
      page.includes(contributionName),
      false,
      `AuditPage conoce ${contributionName}`,
    );
  }
});

test('package.json expone un comando permanente para las focales', () => {
  const pkg = JSON.parse(source('package.json'));

  assert.equal(
    pkg.scripts['test:audit-vocabulary'],
    'node --test tests/auditEntityVocabulary.test.mjs',
  );
});
