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

// --- Vías que no son un import escrito: 1) import() calculado ------------

test('F7: import() con ruta calculada falla, no se ignora', () => {
  for (const forma of [
    'const m = import(`../${codigo}/routes`);',
    'const m = import(nombre);',
    "const m = import('../' + 'catalog/components/Card');",
    'const m = import();',
  ]) {
    const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.tsx': `const codigo = 'x'; const nombre = 'y';\n${forma}\n` });
    assert.deepEqual(reglas(r), ['F7'], `se escapó: ${forma}`);
    assert.equal(r.violaciones[0].linea, 2);
  }
});

test('import() con plantilla sin sustituciones se juzga como un import literal', () => {
  const cruzado = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.tsx': 'const m = import(`../catalog/components/Card`);\n' });
  assert.deepEqual(reglas(cruzado), ['F1']);
  const propio = analizar({ ...MODULO_LIMPIO, 'modules/catalog/pages/Lista.tsx': 'const m = import(`../components/Card`);\n' });
  assert.deepEqual(propio.violaciones, []);
});

test('typeof import() de otro módulo en un tipo es F1', () => {
  const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.ts': "type C = typeof import('../catalog/components/Card');\n" });
  assert.deepEqual(reglas(r), ['F1']);
});

// --- 2) import.meta.glob -------------------------------------------------

test('import.meta.glob dentro del propio módulo, de shared o con exclusión pasa', () => {
  const r = analizar({
    ...MODULO_LIMPIO,
    'modules/catalog/pages/Lista.tsx': [
      "const a = import.meta.glob('./secciones/*.tsx');",
      "const b = import.meta.glob(['../components/**/*.tsx', '!../components/**/*.test.tsx']);",
      "const c = import.meta.glob('../../../shared/ui/*.tsx', { eager: true });",
      "const d = import.meta.glob('!../../cms/**');",
      "const e = import.meta.glob('../*/index.tsx');",
      '',
    ].join('\n'),
  });
  assert.deepEqual(r.violaciones, []);
});

test('import.meta.glob que alcanza otro módulo es F1, con la línea del patrón', () => {
  for (const forma of [
    "import.meta.glob('../../catalog/components/*.tsx')",
    "import.meta.glob('../../*/routes.tsx')",
    "import.meta.globEager('../../catalog/**/*.tsx')",
    "import.meta.glob(['./propio/*.ts', '../../catalog/components/*.tsx'])",
  ]) {
    const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/pages/Portada.tsx': `\nconst m = ${forma};\n` });
    assert.deepEqual(reglas(r), ['F1'], `se escapó: ${forma}`);
    assert.equal(r.violaciones[0].linea, 2);
  }
});

test('import.meta.glob calculado, que abarca src/ o que vuelve a subir tras un comodín es F7', () => {
  for (const forma of [
    'import.meta.glob(patron)',
    'import.meta.glob(`../${codigo}/*.tsx`)',
    'import.meta.glob([patron])',
    "import.meta.glob('../../../**/*.tsx')",
    "import.meta.glob('./propio/**/../../../catalog/*.tsx')",
    "import.meta.glob('./{propio,../../catalog}/*.tsx')",
    "import.meta.glob('**/*.tsx')",
  ]) {
    const r = analizar({
      ...MODULO_LIMPIO,
      'modules/cms/pages/Portada.tsx': `const patron = 'x'; const codigo = 'y';\nconst m = ${forma};\n`,
    });
    assert.deepEqual(reglas(r), ['F7'], `se escapó: ${forma}`);
  }
});

test('import.meta.glob desde la plataforma hacia un módulo es F4', () => {
  const r = analizar({ ...MODULO_LIMPIO, 'platform/Registro.ts': "const m = import.meta.glob('../modules/*/routes.tsx');\n" });
  assert.deepEqual(reglas(r), ['F4']);
});

test('import.meta que no se puede verificar es F7; env, url y hot pasan', () => {
  const limpio = analizar({
    ...MODULO_LIMPIO,
    'platform/x.ts': [
      'const dev = import.meta.env.DEV;',
      'const aqui = import.meta.url;',
      'if (import.meta.hot) import.meta.hot.accept();',
      '',
    ].join('\n'),
  });
  assert.deepEqual(limpio.violaciones, []);
  for (const forma of [
    'const g = import.meta.glob;',
    "const g = import.meta['glob']('../../catalog/*.tsx');",
    'const m = import.meta;',
    'const { glob } = import.meta;',
  ]) {
    const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.ts': `${forma}\n` });
    assert.deepEqual(reglas(r), ['F7'], `se escapó: ${forma}`);
  }
});

// --- 3) new URL(…, import.meta.url) --------------------------------------

test('new URL con import.meta.url hacia otro módulo es F1; hacia lo propio pasa', () => {
  const cruzado = analizar({
    ...MODULO_LIMPIO,
    'modules/cms/Banner.ts': "const u = new URL('../catalog/components/foto.png', import.meta.url);\n",
  });
  assert.deepEqual(reglas(cruzado), ['F1']);
  const sinPunto = analizar({
    ...MODULO_LIMPIO,
    'modules/cms/Banner.ts': "const u = new URL('../catalog/components/foto.png', import.meta.url);\nconst w = new Worker(new URL('../catalog/w.ts', import.meta.url));\n",
  });
  assert.deepEqual(reglas(sinPunto), ['F1', 'F1']);
  const propio = analizar({
    ...MODULO_LIMPIO,
    'modules/catalog/pages/Lista.ts': "const u = new URL('../components/foto.png', import.meta.url);\nconst v = new URL('foto.png', import.meta.url);\n",
  });
  assert.deepEqual(propio.violaciones, []);
});

test('new URL con import.meta.url y ruta calculada es F7; hacia /src es F5', () => {
  const calculada = analizar({ ...MODULO_LIMPIO, 'modules/cms/Banner.ts': 'const r = "x";\nconst u = new URL(r, import.meta.url);\n' });
  assert.deepEqual(reglas(calculada), ['F7']);
  const absoluta = analizar({
    ...MODULO_LIMPIO,
    'modules/cms/Banner.ts': "const u = new URL('/src/modules/catalog/foto.png', import.meta.url);\n",
  });
  assert.deepEqual(reglas(absoluta), ['F5']);
});

test('new URL con otra base no es una referencia al árbol, como en MediaPage', () => {
  const r = analizar({
    ...MODULO_LIMPIO,
    'modules/cms/Banner.ts': [
      'const asset = { url: "/media/x.png" };',
      'const a = new URL(asset.url, window.location.origin).href;',
      "const b = new URL('../catalog/foto.png', window.location.origin);",
      "const c = new URL('https://ejemplo.pe/x');",
      '',
    ].join('\n'),
  });
  assert.deepEqual(r.violaciones, []);
  assert.match(
    readFileSync(join(srcReal, 'modules/core/pages/MediaPage.tsx'), 'utf8'),
    /new URL\(asset\.url, window\.location\.origin\)/,
    'el uso real de CORE que esta prueba protege ya no está: revisa la prueba',
  );
});

// --- 4) url(…) en CSS ------------------------------------------------------

test('url() en CSS hacia otro módulo es F1, con o sin comillas', () => {
  for (const valor of ["'../catalog/components/foto.png'", '"../catalog/components/foto.png"', '../catalog/components/foto.png']) {
    const r = analizar({ ...MODULO_LIMPIO, 'modules/cms/cms.css': `.a {\n  background: url(${valor});\n}\n` });
    assert.deepEqual(reglas(r), ['F1'], `se escapó: url(${valor})`);
    assert.equal(r.violaciones[0].linea, 2);
  }
});

test('url() en CSS: shared hacia un módulo es F2, plataforma es F4, /src es F5', () => {
  assert.deepEqual(
    reglas(analizar({ ...MODULO_LIMPIO, 'shared/ui/x.css': ".a { background: url('../../modules/catalog/foto.png'); }\n" })),
    ['F2'],
  );
  assert.deepEqual(
    reglas(analizar({ ...MODULO_LIMPIO, 'platform/x.css': ".a { background: url('../modules/catalog/foto.png'); }\n" })),
    ['F4'],
  );
  assert.deepEqual(
    reglas(analizar({ ...MODULO_LIMPIO, 'modules/cms/cms.css': ".a { background: url('/src/modules/catalog/foto.png'); }\n" })),
    ['F5'],
  );
});

test('url() en CSS hacia lo propio, shared, public, esquemas o anclas pasa; y un comentario no cuenta', () => {
  const r = analizar({
    ...MODULO_LIMPIO,
    'modules/catalog/components/tienda.css': [
      '.a { background: url(./foto.png); }',
      ".b { background: url('../../../shared/ui/icono.svg'); }",
      ".c { src: url('/fuentes/inter.woff2'); }",
      '.d { background: url("data:image/svg+xml;utf8,<svg/>"); }',
      ".e { background: url('https://cdn.ejemplo.pe/x.png'); }",
      ".f { mask: url(#recorte); }",
      '/* .g { background: url(../../cms/foto.png); } */',
      '',
    ].join('\n'),
  });
  assert.deepEqual(r.violaciones, []);
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
