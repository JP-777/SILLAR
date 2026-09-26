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
 *
 * **Por qué `COMPOSICION` es una lista de ficheros y no de carpetas.** Una
 * excepción por carpeta —«`platform/` puede importar módulos»— deja pasar el
 * siguiente fichero que nadie ha mirado. Por fichero, cada punto de composición
 * nuevo tiene que escribirse aquí, y eso se ve en el diff.
 *
 * **Y una entrada que ya no se usa también es un fallo.** Una excepción que
 * sobrevive a su motivo es permiso para el próximo que llegue.
 *
 * Los imports se extraen con `ts.preProcessFile`, el mismo analizador del
 * compilador: ve `import type`, `export … from`, `import()` y los imports de
 * efecto, y no confunde un comentario ni una cadena con un import.
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

function importsDe(ruta, texto) {
  if (ruta.endsWith('.css')) {
    const salida = [];
    const re = /@import\s+(?:url\(\s*)?['"]([^'"]+)['"]/g;
    let m;
    while ((m = re.exec(texto))) salida.push({ especificador: m[1], linea: lineaDe(texto, m.index) });
    return salida;
  }
  const info = ts.preProcessFile(texto, true, true);
  return info.importedFiles.map((f) => ({ especificador: f.fileName, linea: lineaDe(texto, f.pos) }));
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

    for (const { especificador, linea } of importsDe(ruta, texto)) {
      imports += 1;

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
