import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  auditEntityLabel,
  composeAuditEntityVocabularies,
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
  },
  {
    file: 'src/modules/catalog/routes.tsx',
    exportName: 'catalogAuditEntityVocabulary',
    moduleCode: 'catalog',
  },
  {
    file: 'src/modules/cms/cmsHome.tsx',
    exportName: 'cmsAuditEntityVocabulary',
    moduleCode: 'cms',
  },
  {
    file: 'src/modules/crm/routes.tsx',
    exportName: 'crmAuditEntityVocabulary',
    moduleCode: 'crm',
  },
];

function source(path) {
  return readFileSync(resolve(frontend, path), 'utf8');
}

function contribution({ file, exportName, moduleCode }) {
  const text = source(file);
  const escaped = exportName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  const match = new RegExp(
    `export const ${escaped}: AuditEntityVocabulary = \\{` +
      `\\s*moduleCode: '([^']+)',` +
      `\\s*labels: \\{([\\s\\S]*?)\\n\\s*\\},` +
      `\\s*\\};`,
  ).exec(text);

  assert.ok(match, `${file} no declara ${exportName}`);
  assert.equal(match[1], moduleCode, `${exportName} cambió de moduleCode`);

  const labels = {};
  const pair = /^\s*([a-z0-9_]+):\s*'([^']*)',\s*$/gm;

  for (const item of match[2].matchAll(pair)) {
    labels[item[1]] = item[2];
  }

  return { moduleCode, labels };
}

test('las cuatro contribuciones componen exactamente las veinte etiquetas históricas', () => {
  const actual = composeAuditEntityVocabularies(surfaces.map(contribution));

  assert.equal(Object.keys(actual).length, 20);
  assert.deepEqual(actual, expected);
});

test('el registro estático contiene las cuatro contribuciones y ninguna etiqueta concreta', () => {
  const registry = source('src/platform/auditEntityVocabularies.ts');

  const body = /AUDIT_ENTITY_VOCABULARIES:[\s\S]*?=\s*\[([\s\S]*?)\];/.exec(registry);
  assert.ok(body, 'no se encontró AUDIT_ENTITY_VOCABULARIES');

  const listed = [
    ...body[1].matchAll(/\b([a-zA-Z]+AuditEntityVocabulary)\b/g),
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

test('un entityType no registrado degrada al código técnico exacto', () => {
  assert.equal(
    auditEntityLabel('tipo_que_nadie_declaro', expected),
    'tipo_que_nadie_declaro',
  );
});

test('una clave duplicada se detecta e identifica entityType y módulos', () => {
  assert.throws(
    () =>
      composeAuditEntityVocabularies([
        { moduleCode: 'modulo_a', labels: { repetido: 'Uno' } },
        { moduleCode: 'modulo_b', labels: { repetido: 'Dos' } },
      ]),
    (error) => {
      assert.match(error.message, /repetido/);
      assert.match(error.message, /modulo_a/);
      assert.match(error.message, /modulo_b/);
      return true;
    },
  );
});

test('AuditPage no contiene el mapa concreto y consulta la plataforma', () => {
  const page = source('src/modules/core/pages/AuditPage.tsx');

  assert.equal(/\bENTIDADES\b/.test(page), false);
  assert.match(page, /visibleAuditEntityLabels/);
  assert.match(page, /auditEntityLabel/);

  for (const [entityType, label] of Object.entries(expected)) {
    const mapping = `${entityType}: '${label}'`;
    assert.equal(
      page.includes(mapping),
      false,
      `AuditPage todavía conoce ${mapping}`,
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
      `AuditPage conoce la contribución ${contributionName}`,
    );
  }
});
