import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import { COMPOSICION, analizarFronteras } from '../scripts/fronteras-frontend.mjs';

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const srcReal = join(frontend, 'src');
const script = join(frontend, 'scripts', 'fronteras-frontend.mjs');

/** Crea un `src/` de pega con los ficheros dados y devuelve su ruta. */
function arbol(ficheros) {
  const base = mkdtempSync(join(tmpdir(), 'fronteras-'));
  const src = join(base, 'src');
  for (const [ruta, contenido] of Object.entries(ficheros)) {
    const destino = join(src, ruta);
    mkdirSync(dirname(destino), { recursive: true });
    writeFileSync(destino, contenido);
  }
  return src;
}

function limpiar(src) {
  rmSync(dirname(src), { recursive: true, force: true });
}

/** Lo mínimo para que un árbol de pega use una composición vacía sin quejas. */
function analizar(ficheros, composicion = {}) {
  const src = arbol(ficheros);
  try {
    return analizarFronteras(src, { composicion });
  } finally {
    limpiar(src);
  }
}

function reglas(resultado) {
  return resultado.violaciones.map((v) => v.regla);
}

const MODULO_LIMPIO = {
  'shared/ui/index.tsx': 'export const Boton = () => null;\n',
  'shared/ui/ui.css': '.ui-boton { color: var(--text); }\n',
  'modules/catalog/components/Card.tsx': "export const Card = () => null;\n",
};

// --- El árbol real -------------------------------------------------------

test('el frontend real cumple las fronteras entre módulos', () => {
  const r = analizarFronteras(srcReal);
  assert.deepEqual(r.violaciones, []);
  assert.ok(r.ficheros > 50, `solo se analizaron ${r.ficheros} ficheros: ¿se leyó la carpeta correcta?`);
  assert.ok(r.imports > 200, `solo se vieron ${r.imports} imports: ¿se extraen de verdad?`);
});

test('cada excepción de composición se usa en el frontend real', () => {
  assert.deepEqual(analizarFronteras(srcReal).composicionSinUso, []);
});

test('la lista de composición no nombra ningún fichero de un módulo', () => {
  for (const origen of Object.keys(COMPOSICION)) {
    assert.ok(!origen.startsWith('modules/'), `${origen} es de un módulo: F1 no admite excepciones`);
  }
});

// --- Positivas: lo legal pasa -------------------------------------------

test('un módulo importa de shared, de sí mismo y de paquetes sin violación', () => {
  const r = analizar({
    ...MODULO_LIMPIO,
    'modules/catalog/pages/Lista.tsx': [
      "import { useState } from 'react';",
      "import { Boton } from '../../../shared/ui';",
      "import '../../../shared/ui/ui.css';",
      "import { Card } from '../components/Card';",
      "import type { X } from './tipos';",
      '',
    ].join('\n'),
    'modules/catalog/pages/tipos.ts': 'export type X = 1;\n',
  });
  assert.deepEqual(r.violaciones, []);
});

test('un punto de composición declarado importa el routes de cualquier módulo', () => {
  const r = analizar(
    {
      'modules/catalog/routes.tsx': 'export const r = 1;\n',
      'modules/cms/routes.tsx': 'export const r = 1;\n',
      'app/routes.tsx': [
        "import { r } from '../modules/catalog/routes';",
        "import { r as s } from '../modules/cms/routes';",
        '',
      ].join('\n'),
    },
    { 'app/routes': ['modules/*/routes'] },
  );
  assert.deepEqual(r.violaciones, []);
  assert.deepEqual(r.composicionSinUso, []);
});

test('un comentario o una cadena con forma de import no cuentan', () => {
  const r = analizar({
    ...MODULO_LIMPIO,
    'modules/cms/Banner.tsx': [
      "// import { Card } from '../catalog/components/Card';",
      "/* export * from '../catalog/components/Card'; */",
      "const ejemplo = \"import { Card } from '../catalog/components/Card'\";",
      'export default ejemplo;',
      '',
    ].join('\n'),
  });
  assert.deepEqual(r.violaciones, []);
});

// --- Negativas: cada regla dice que no ----------------------------------

test('F1: un módulo que importa de otro módulo es rechazado, con fichero y línea', () => {
  const r = analizar({
    ...MODULO_LIMPIO,
    'modules/cms/Banner.tsx': "\nimport { Card } from '../catalog/components/Card';\n",
  });
  assert.equal(r.violaciones.length, 1);
  const [v] = r.violaciones;
  assert.equal(v.regla, 'F1');
  assert.equal(v.fichero, 'modules/cms/Banner.tsx');
  assert.equal(v.linea, 2);
  assert.equal(v.detalle, 'modules/cms → modules/catalog');
});

test('F1: ninguna forma de import se escapa', () => {
  const formas = [
    "import type { Card } from '../catalog/components/Card';",
    "export { Card } from '../catalog/components/Card';",
    "export * from '../catalog/components/Card';",
    "const m = import('../catalog/components/Card');",
    "import '../catalog/components/Card';",
    "import { Card } from './../catalog/components/Card';",
    "import { Card } from '../cms/../catalog/components/Card';",
    "import { Card } from '../../modules/catalog/components/Card';",
    "import * as todo from '../catalog';",
  ];
  for (const forma of formas) {
    const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.tsx': `${forma}\n` });
    assert.deepEqual(reglas(r), ['F1'], `se escapó: ${forma}`);
  }
});

test('F1: el CSS de otro módulo tampoco se importa', () => {
  const r = analizar({
    ...MODULO_LIMPIO,
    'modules/catalog/components/tienda.css': '.ti-card {}\n',
    'modules/cms/cms.css': "@import '../catalog/components/tienda.css';\n",
    'modules/cms/Banner.tsx': "import '../catalog/components/tienda.css';\n",
  });
  assert.deepEqual(reglas(r).sort(), ['F1', 'F1']);
});

test('F1: ninguna entrada de composición afloja la regla entre módulos', () => {
  // Aunque alguien declare el fichero de un módulo como punto de composición,
  // la regla entre módulos se evalúa antes y no mira la lista.
  const r = analizar(
    {
      ...MODULO_LIMPIO,
      'modules/cms/routes.tsx': "import { Card } from '../catalog/components/Card';\n",
    },
    { 'modules/cms/routes': ['modules/catalog/components/Card'] },
  );
  assert.deepEqual(reglas(r), ['F1']);
});

test('F2: shared no importa de un módulo, de platform ni de app', () => {
  for (const destino of ['../../modules/catalog/components/Card', '../../platform/x', '../../app/App']) {
    const r = analizar({
      ...MODULO_LIMPIO,
      'platform/x.ts': 'export const x = 1;\n',
      'app/App.tsx': 'export const App = 1;\n',
      'shared/ui/Otro.tsx': `import { x } from '${destino}';\n`,
    });
    assert.deepEqual(reglas(r), ['F2'], `shared → ${destino} no se rechazó`);
  }
});

test('F3: un módulo no importa de app ni de main', () => {
  for (const destino of ['../../app/routes', '../../main']) {
    const r = analizar({
      ...MODULO_LIMPIO,
      'app/routes.tsx': 'export const r = 1;\n',
      'main.tsx': 'export const m = 1;\n',
      'modules/cms/Banner.tsx': `import { r } from '${destino}';\n`,
    });
    assert.deepEqual(reglas(r), ['F3'], `módulo → ${destino} no se rechazó`);
  }
});

test('F4: un fichero de plataforma no declarado no importa de un módulo', () => {
  const r = analizar(
    {
      ...MODULO_LIMPIO,
      'platform/Nueva.tsx': "import { Card } from '../modules/catalog/components/Card';\n",
    },
    {},
  );
  assert.deepEqual(reglas(r), ['F4']);
});

test('F4: un punto de composición solo importa lo que tiene declarado', () => {
  const r = analizar(
    {
      ...MODULO_LIMPIO,
      'modules/catalog/routes.tsx': 'export const r = 1;\n',
      'app/routes.tsx': [
        "import { r } from '../modules/catalog/routes';",
        "import { Card } from '../modules/catalog/components/Card';",
        '',
      ].join('\n'),
    },
    { 'app/routes': ['modules/*/routes'] },
  );
  assert.deepEqual(reglas(r), ['F4']);
  assert.equal(r.violaciones[0].linea, 2);
});

test('F4: una excepción de composición que ya no se usa hace fallar', () => {
  const r = analizar(MODULO_LIMPIO, { 'platform/Vieja': ['modules/catalog/components/Card'] });
  assert.deepEqual(r.violaciones, []);
  assert.deepEqual(r.composicionSinUso, ['platform/Vieja ⇒ modules/catalog/components/Card']);
});

test('F5: una ruta no relativa hacia el árbol es rechazada', () => {
  for (const especificador of [
    '/src/modules/catalog/components/Card',
    'src/modules/catalog/components/Card',
    '@/modules/catalog/components/Card',
    '~/modules/catalog/components/Card',
  ]) {
    const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.tsx': `import { Card } from '${especificador}';\n` });
    assert.deepEqual(reglas(r), ['F5'], `se escapó: ${especificador}`);
  }
});

test('F6: un import relativo que sale de src es rechazado', () => {
  const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.tsx': "import x from '../../../tests/x';\n" });
  assert.deepEqual(reglas(r), ['F6']);
});

// --- Falsificación deliberada -------------------------------------------

test('romper el frontend real: el import de NoPhoto de M02 desde catálogo se rechaza', () => {
  // Réplica del caso que motivó esta barrera: `cmsHome.tsx` en la rama de
  // cierre de M02 importaba NoPhoto del catálogo.
  const base = mkdtempSync(join(tmpdir(), 'fronteras-real-'));
  const copia = join(base, 'src');
  try {
    cpSync(srcReal, copia, { recursive: true });
    const cmsHome = join(copia, 'modules', 'cms', 'cmsHome.tsx');
    writeFileSync(
      cmsHome,
      `import { ProductCard } from '../catalog/components/ProductCard';\n${readFileSync(cmsHome, 'utf8')}`,
    );
    const r = analizarFronteras(copia);
    assert.deepEqual(reglas(r), ['F1']);
    assert.equal(r.violaciones[0].fichero, 'modules/cms/cmsHome.tsx');
    assert.equal(r.violaciones[0].linea, 1);
  } finally {
    rmSync(base, { recursive: true, force: true });
  }
});

test('el comando termina en 1 con una violación y en 0 sin ella', () => {
  const sucio = arbol({ ...MODULO_LIMPIO, 'modules/cms/Banner.tsx': "import { Card } from '../catalog/components/Card';\n" });
  const limpio = arbol(MODULO_LIMPIO);
  try {
    const rojo = spawnSync(process.execPath, [script, sucio], { encoding: 'utf8' });
    assert.equal(rojo.status, 1, rojo.stdout + rojo.stderr);
    assert.match(rojo.stderr, /F1 modules\/cms\/Banner\.tsx:1/);

    // Sin composición declarada en el árbol de pega, la lista real queda sin
    // uso y el comando también tiene que decirlo: por eso el limpio se
    // comprueba con la función, y el comando solo con el árbol real.
    assert.deepEqual(analizarFronteras(limpio, { composicion: {} }).violaciones, []);

    const real = spawnSync(process.execPath, [script], { encoding: 'utf8' });
    assert.equal(real.status, 0, real.stdout + real.stderr);
    assert.match(real.stdout, /sin violaciones/);
  } finally {
    limpiar(sucio);
    limpiar(limpio);
  }
});

test('el comando falla si la carpeta a analizar no existe', () => {
  const r = spawnSync(process.execPath, [script, join(tmpdir(), 'no-existe-fronteras')], { encoding: 'utf8' });
  assert.equal(r.status, 2);
});
