#!/usr/bin/env node
/**
 * Barrera de fronteras entre módulos del frontend.
 *
 * **Lo que vigila** (ADR-005, `src/modules/README.md`):
 *
 *   F1. Un módulo nunca importa de otro módulo. Lo compartido vive en `shared/`.
 *       **Sin excepciones**: no existe ninguna lista que la afloje.
 *   F2. `shared/` no importa nada de fuera de `shared/`. Si lo hiciera, dejaría
 *       de ser compartido: arrastraría a quien lo importa.
 *   F3. Un módulo no importa de `app/` ni de `main.tsx`: la aplicación compone
 *       módulos, y el camino inverso es un ciclo.
 *   F4. Fuera de `modules/`, solo los **puntos de composición** enumerados en
 *       `COMPOSICION` importan de un módulo, y solo lo que ahí se nombra.
 *   F5. Nadie llega al árbol por una ruta no relativa (`/src/…`, `src/…`,
 *       `@/…`, `~/…`): sería la forma de saltarse las cuatro anteriores con un
 *       alias.
 *   F6. Nadie sale de `src/` con un import relativo.
 *   F7. Lo que no se puede verificar sin ejecutar el código **falla**, no se
 *       ignora: un `import()` o un `import.meta.glob` con ruta calculada, un
 *       glob que abarca todo `src/` o que vuelve a subir tras un comodín, y
 *       cualquier otro uso de `import.meta` que no sea `.env`, `.url`, `.hot`
 *       o `.glob`.
 *
 * **Qué cuenta como referencia**, además de `import`/`export … from`,
 * `import type` y `typeof import('…')`:
 *
 *   - `import('literal')` y `` import(`sin sustituciones`) ``.
 *   - `import.meta.glob('patrón' | ['patrón', …])`: cada patrón se juzga por su
 *     **directorio base** —lo que hay antes del primer comodín—, que es lo más
 *     lejos que puede llegar. Los patrones de exclusión (`!…`) no amplían nada.
 *   - `new URL('literal', import.meta.url)`: un recurso del propio árbol,
 *     resuelto por Vite respecto del fichero. **Con cualquier otra base** —
 *     `new URL(asset.url, window.location.origin)` en `core/pages/MediaPage.tsx`—
 *     es una URL de la página, no una referencia al árbol, y no se mira.
 *   - `url(…)` e `@import` en CSS, con los comentarios quitados antes. Se
 *     ignoran los esquemas (`data:`, `https:`…), `//…`, `#…` y las rutas
 *     absolutas `/…`, que Vite sirve desde `public/`, no desde `src/`; salvo
 *     `/src/…`, que es F5.
 *
 * **Fuera de alcance, a propósito:**
 *
 *   - `url(…)` dentro de un `style` en línea de un componente: el navegador lo
 *     resuelve respecto de la **página**, no del fichero; Vite no lo empaqueta
 *     y no puede alcanzar el árbol de otro módulo.
 *   - `image-set("…")` con cadenas sin `url()` en CSS: no hay ningún uso, y
 *     reconocerlo exige un analizador de CSS. Si aparece, se amplía aquí.
 *   - `require()`: el frontend es ESM puro y Vite no empaqueta `require` en
 *     `src/`. Un `require('literal')` se sigue juzgando; uno calculado, no.
 *
 * **Por qué `COMPOSICION` es una lista de ficheros y no de carpetas.** Una
 * excepción por carpeta —«`platform/` puede importar módulos»— deja pasar el
 * siguiente fichero que nadie ha mirado. Por fichero, cada punto de composición
 * nuevo tiene que escribirse aquí, y eso se ve en el diff.
 *
 * **Y una entrada que ya no se usa también es un fallo.** Una excepción que
 * sobrevive a su motivo es permiso para el próximo que llegue.
 *
 * Las referencias se extraen recorriendo el **árbol sintáctico** de
 * TypeScript: un comentario o una cadena que parece un import no lo es. No se
 * usa `ts.preProcessFile` porque ignora en silencio un `import()` calculado,
 * que es justo el caso que tiene que fallar.
 */

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const ts = require('typescript');

const EXTENSIONES_CODIGO = ['.ts', '.tsx', '.mts', '.cts', '.js', '.jsx', '.mjs', '.cjs'];
const EXTENSIONES = [...EXTENSIONES_CODIGO, '.css'];

/** Destino comodín: el `routes` de cualquier módulo, que es su cara pública. */
const ROUTES_DE_CUALQUIER_MODULO = 'modules/*/routes';

/**
 * Puntos de composición: fichero fuera de `modules/` → lo que puede importar de
 * un módulo. Rutas relativas a `src/`, sin extensión.
 */
export const COMPOSICION = {
  // Monta las rutas de los módulos activos.
  'app/routes': [ROUTES_DE_CUALQUIER_MODULO],
  // Construye el menú desde las entradas que exporta cada módulo.
  'layout/navigation': [ROUTES_DE_CUALQUIER_MODULO],
  // Registros de la portada, del vocabulario de auditoría y del footer.
  'platform/homeSections': [ROUTES_DE_CUALQUIER_MODULO, 'modules/cms/cmsHome'],
  'platform/auditEntityVocabularies': [
    ROUTES_DE_CUALQUIER_MODULO,
    'modules/cms/cmsHome',
    'modules/core/auditEntityVocabulary',
  ],
  'platform/footerContributions': ['modules/cms/cmsFooter', 'modules/core/coreFooter'],
  // Existentes en `main` al escribir la barrera y **no son registros**: la
  // plataforma llega a la capa de servicios o de sesión de un módulo. Se
  // enumeran para que la barrera no nazca en rojo, no porque se aprueben:
  // son hallazgos para Integración.
  'platform/HomePage': ['modules/core/services/settings'],
  'app/App': ['modules/crm/session', 'modules/crm/services/customerAuth'],
};

const REGLAS = {
  F1: 'un módulo no importa de otro módulo',
  F2: 'shared/ no importa de fuera de shared/',
  F3: 'un módulo no importa de app/ ni de main',
  F4: 'solo un punto de composición declarado importa de un módulo',
  F5: 'ruta no relativa hacia el árbol',
  F6: 'import relativo que sale de src/',
  F7: 'referencia que no se puede verificar estáticamente',
};

function listarFicheros(dir) {
  const salida = [];
  for (const nombre of readdirSync(dir).sort()) {
    const ruta = join(dir, nombre);
    if (statSync(ruta).isDirectory()) {
      salida.push(...listarFicheros(ruta));
    } else if (EXTENSIONES.includes(extname(nombre)) && !nombre.endsWith('.d.ts')) {
      salida.push(ruta);
    }
  }
  return salida;
}

/** Ruta relativa a `src/` con barras normales, igual en Windows y en Linux. */
function aSrc(raiz, ruta) {
  return relative(raiz, ruta).split(sep).join('/');
}

/** Quita extensión e `/index` para comparar destinos como se escriben. */
function canonica(rutaSrc) {
  let r = rutaSrc;
  const ext = EXTENSIONES_CODIGO.find((e) => r.endsWith(e));
  if (ext) r = r.slice(0, -ext.length);
  return r.endsWith('/index') ? r.slice(0, -'/index'.length) : r;
}

/** `modules/<código>` para lo que vive en un módulo; si no, la carpeta de primer nivel. */
function zona(rutaSrc) {
  const partes = rutaSrc.split('/');
  if (partes[0] === 'modules') {
    return partes.length >= 2 && partes[1] !== '' ? `modules/${partes[1]}` : 'modules';
  }
  // `main.tsx` es la raíz de la aplicación y va con `app`. Un import de carpeta
  // —`../session`— cae aquí con una sola parte y es esa carpeta.
  return canonica(partes[0]) === 'main' ? 'app' : partes[0];
}

function esModulo(z) {
  return z.startsWith('modules/');
}

function esRutaNoRelativaAlArbol(especificador) {
  return /^(\/|src\/|@\/|~\/)/.test(especificador);
}

function lineaDe(texto, posicion) {
  let linea = 1;
  for (let i = 0; i < posicion; i += 1) if (texto.charCodeAt(i) === 10) linea += 1;
  return linea;
}

/**
 * Un recurso referido por `url(…)` o `new URL(…, import.meta.url)`. Las rutas
 * `/…` son de `public/` y no del árbol, salvo `/src/…`; un valor sin `./`
 * también es relativo al fichero.
 */
function clasificarRecurso(valor) {
  const v = valor.trim();
  if (v === '' || /^[a-z][a-z0-9+.-]*:/i.test(v) || v.startsWith('//') || v.startsWith('#')) return null;
  if (/^(\/src\/|@\/|~\/)/.test(v)) return v; // F5 en el análisis
  if (v.startsWith('/')) return null;
  return v.startsWith('.') ? v : `./${v}`;
}

/** Primer carácter de comodín de un glob: `*`, `?`, `{`, `[` o un extglob `(`. */
const COMODIN = /[*?{[(]/;

/**
 * Lo más lejos que puede llegar un patrón de `import.meta.glob`: su directorio
 * base. Devuelve la referencia a juzgar, un F7, o nada si es una exclusión.
 */
function juzgarGlob(patron) {
  if (patron.startsWith('!')) return null;
  const corte = patron.search(COMODIN);
  const base = corte < 0 ? patron : patron.slice(0, corte);
  const resto = corte < 0 ? '' : patron.slice(corte);
  if (resto.split(/[/,{}]/).includes('..')) {
    return { f7: 'el glob vuelve a subir después de un comodín', texto: patron };
  }
  const directorio = base.slice(0, base.lastIndexOf('/') + 1);
  if (esRutaNoRelativaAlArbol(directorio)) return { especificador: directorio, glob: true };
  if (!directorio.startsWith('.')) {
    return { f7: 'glob sin ruta relativa desde el fichero', texto: patron };
  }
  return { especificador: directorio, glob: true };
}

function tipoDeScript(ruta) {
  if (ruta.endsWith('.tsx')) return ts.ScriptKind.TSX;
  if (ruta.endsWith('.jsx')) return ts.ScriptKind.JSX;
  if (/\.(m|c)?js$/.test(ruta)) return ts.ScriptKind.JS;
  return ts.ScriptKind.TS;
}

function esLiteral(nodo) {
  return nodo && (ts.isStringLiteral(nodo) || ts.isNoSubstitutionTemplateLiteral(nodo));
}

function esImportMeta(nodo) {
  return ts.isMetaProperty(nodo) && nodo.keywordToken === ts.SyntaxKind.ImportKeyword && nodo.name.text === 'meta';
}

function esImportMetaPunto(nodo, nombre) {
  return nodo && ts.isPropertyAccessExpression(nodo) && esImportMeta(nodo.expression) && nodo.name.text === nombre;
}

const USOS_DE_IMPORT_META = new Set(['env', 'url', 'hot', 'glob', 'globEager']);

/** Referencias de un fichero de código, recorriendo su árbol sintáctico. */
function referenciasDeCodigo(ruta, texto) {
  const sf = ts.createSourceFile(ruta, texto, ts.ScriptTarget.Latest, true, tipoDeScript(ruta));
  const salida = [];
  const linea = (nodo) => sf.getLineAndCharacterOfPosition(nodo.getStart(sf)).line + 1;
  const ref = (nodo, especificador) => salida.push({ especificador, linea: linea(nodo) });
  const f7 = (nodo, motivo) => salida.push({ f7: motivo, texto: nodo.getText(sf).slice(0, 80), linea: linea(nodo) });

  const visitar = (nodo) => {
    if ((ts.isImportDeclaration(nodo) || ts.isExportDeclaration(nodo)) && nodo.moduleSpecifier && esLiteral(nodo.moduleSpecifier)) {
      ref(nodo, nodo.moduleSpecifier.text);
    } else if (ts.isImportEqualsDeclaration(nodo) && ts.isExternalModuleReference(nodo.moduleReference)
      && esLiteral(nodo.moduleReference.expression)) {
      ref(nodo, nodo.moduleReference.expression.text);
    } else if (ts.isImportTypeNode(nodo) && ts.isLiteralTypeNode(nodo.argument) && esLiteral(nodo.argument.literal)) {
      ref(nodo, nodo.argument.literal.text);
    } else if (ts.isCallExpression(nodo)) {
      const [primero] = nodo.arguments;
      if (nodo.expression.kind === ts.SyntaxKind.ImportKeyword) {
        if (esLiteral(primero)) ref(nodo, primero.text);
        else f7(nodo, 'import() con ruta calculada');
      } else if (ts.isIdentifier(nodo.expression) && nodo.expression.text === 'require' && esLiteral(primero)) {
        ref(nodo, primero.text);
      } else if (esImportMetaPunto(nodo.expression, 'glob') || esImportMetaPunto(nodo.expression, 'globEager')) {
        const patrones = esLiteral(primero)
          ? [primero]
          : primero && ts.isArrayLiteralExpression(primero) && primero.elements.every(esLiteral)
            ? primero.elements
            : null;
        if (!patrones || patrones.length === 0) {
          f7(nodo, 'import.meta.glob con patrón calculado');
        } else {
          for (const p of patrones) {
            const juicio = juzgarGlob(p.text);
            if (juicio?.f7) f7(p, `${juicio.f7}: '${juicio.texto}'`);
            else if (juicio) salida.push({ ...juicio, linea: linea(p) });
          }
        }
      }
    } else if (ts.isNewExpression(nodo) && ts.isIdentifier(nodo.expression) && nodo.expression.text === 'URL'
      && nodo.arguments?.length >= 2 && esImportMetaPunto(nodo.arguments[1], 'url')) {
      const [primero] = nodo.arguments;
      if (esLiteral(primero)) {
        const recurso = clasificarRecurso(primero.text);
        if (recurso) ref(nodo, recurso);
      } else {
        f7(nodo, 'new URL(…, import.meta.url) con ruta calculada');
      }
    } else if (esImportMeta(nodo)) {
      const padre = nodo.parent;
      const nombre = ts.isPropertyAccessExpression(padre) && padre.expression === nodo ? padre.name.text : null;
      const esLlamadaGlob = (nombre === 'glob' || nombre === 'globEager')
        && ts.isCallExpression(padre.parent) && padre.parent.expression === padre;
      if (!nombre || !USOS_DE_IMPORT_META.has(nombre) || ((nombre === 'glob' || nombre === 'globEager') && !esLlamadaGlob)) {
        f7(padre ?? nodo, 'uso de import.meta que no se puede verificar');
      }
    }
    ts.forEachChild(nodo, visitar);
  };
  visitar(sf);
  return salida;
}

/** Referencias de una hoja de estilo: `@import` y `url(…)`, sin comentarios. */
function referenciasDeCss(texto) {
  const limpio = texto.replace(/\/\*[\s\S]*?\*\//g, (c) => c.replace(/[^\n]/g, ' '));
  const salida = [];
  let m;
  const reImport = /@import\s+(['"])(.*?)\1/g;
  while ((m = reImport.exec(limpio))) salida.push({ especificador: m[2], linea: lineaDe(limpio, m.index) });
  const reUrl = /url\(\s*(?:(['"])(.*?)\1|([^'")\s]*))\s*\)/g;
  while ((m = reUrl.exec(limpio))) {
    const recurso = clasificarRecurso(m[2] ?? m[3] ?? '');
    if (recurso) salida.push({ especificador: recurso, linea: lineaDe(limpio, m.index) });
  }
  return salida;
}

function referenciasDe(ruta, texto) {
  return ruta.endsWith('.css') ? referenciasDeCss(texto) : referenciasDeCodigo(ruta, texto);
}

function permitidoPorComposicion(composicion, origenCanonico, destinoCanonico) {
  const permitidos = composicion[origenCanonico];
  if (!permitidos) return null;
  return (
    permitidos.find((p) => {
      if (p === ROUTES_DE_CUALQUIER_MODULO) return /^modules\/[^/]+\/routes$/.test(destinoCanonico);
      return p === destinoCanonico;
    }) ?? null
  );
}

/**
 * Analiza `raiz` (la carpeta `src/`) y devuelve las violaciones.
 *
 * @param {string} raiz
 * @param {{ composicion?: Record<string, string[]> }} [opciones]
 */
export function analizarFronteras(raiz, { composicion = COMPOSICION } = {}) {
  const raizAbs = resolve(raiz);
  const violaciones = [];
  const usadas = new Set();
  let ficheros = 0;
  let imports = 0;

  const anotar = (regla, fichero, linea, especificador, detalle) =>
    violaciones.push({ regla, descripcion: REGLAS[regla], fichero, linea, especificador, detalle });

  for (const ruta of listarFicheros(raizAbs)) {
    ficheros += 1;
    const origen = aSrc(raizAbs, ruta);
    const origenCanonico = canonica(origen);
    const zonaOrigen = zona(origen);
    const texto = readFileSync(ruta, 'utf8');

    for (const referencia of referenciasDe(ruta, texto)) {
      imports += 1;
      const { especificador, linea } = referencia;

      if (referencia.f7) {
        anotar('F7', origen, linea, referencia.texto, referencia.f7);
        continue;
      }
      if (esRutaNoRelativaAlArbol(especificador)) {
        anotar('F5', origen, linea, especificador);
        continue;
      }
      if (!especificador.startsWith('.')) continue; // paquete: react, react-router-dom…

      const destinoAbs = resolve(dirname(ruta), especificador);
      const destino = aSrc(raizAbs, destinoAbs);
      if (destino.startsWith('..')) {
        anotar('F6', origen, linea, especificador);
        continue;
      }
      if (referencia.glob && destino === '') {
        anotar('F7', origen, linea, especificador, 'el glob abarca todo src/');
        continue;
      }
      const destinoCanonico = canonica(destino);
      const zonaDestino = zona(destino);
      if (zonaOrigen === zonaDestino) continue;

      if (esModulo(zonaOrigen)) {
        if (esModulo(zonaDestino) || zonaDestino === 'modules') {
          anotar('F1', origen, linea, especificador, `${zonaOrigen} → ${zonaDestino}`);
        } else if (zonaDestino === 'app') {
          anotar('F3', origen, linea, especificador);
        }
        continue;
      }

      if (zonaOrigen === 'shared') {
        anotar('F2', origen, linea, especificador, `shared → ${zonaDestino}`);
        continue;
      }

      if (esModulo(zonaDestino) || zonaDestino === 'modules') {
        const entrada = permitidoPorComposicion(composicion, origenCanonico, destinoCanonico);
        if (entrada) {
          usadas.add(`${origenCanonico} ⇒ ${entrada}`);
        } else {
          anotar('F4', origen, linea, especificador, `${origenCanonico} → ${destinoCanonico}`);
        }
      }
    }
  }

  const sinUso = [];
  for (const [origen, destinos] of Object.entries(composicion)) {
    for (const destino of destinos) {
      if (!usadas.has(`${origen} ⇒ ${destino}`)) sinUso.push(`${origen} ⇒ ${destino}`);
    }
  }

  return { ficheros, imports, violaciones, composicionSinUso: sinUso };
}

function formatear({ ficheros, imports, violaciones, composicionSinUso }) {
  const lineas = [];
  for (const v of violaciones) {
    const detalle = v.detalle ? ` (${v.detalle})` : '';
    lineas.push(`  ${v.regla} ${v.fichero}:${v.linea} importa '${v.especificador}' — ${v.descripcion}${detalle}`);
  }
  for (const e of composicionSinUso) {
    lineas.push(`  COMPOSICION sin uso: ${e} — una excepción que ya no se usa se borra`);
  }
  const cabecera = lineas.length === 0
    ? `Fronteras del frontend: ${ficheros} ficheros, ${imports} imports, sin violaciones.`
    : `Fronteras del frontend: ${lineas.length} problema(s) en ${ficheros} ficheros.`;
  return [cabecera, ...lineas].join('\n');
}

export function ejecutar(raiz) {
  const resultado = analizarFronteras(raiz);
  const ok = resultado.violaciones.length === 0 && resultado.composicionSinUso.length === 0;
  return { ok, texto: formatear(resultado), resultado };
}

const esPrincipal = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (esPrincipal) {
  const raiz = process.argv[2] ?? join(dirname(fileURLToPath(import.meta.url)), '..', 'src');
  if (!existsSync(raiz)) {
    console.error(`No existe la carpeta a analizar: ${raiz}`);
    process.exit(2);
  }
  const { ok, texto } = ejecutar(raiz);
  (ok ? console.log : console.error)(texto);
  process.exit(ok ? 0 : 1);
}
