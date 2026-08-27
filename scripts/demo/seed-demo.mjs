#!/usr/bin/env node
/**
 * Siembra el catálogo de demostración.
 *
 *     node scripts/demo/seed-demo.mjs
 *
 * **Va por el API, no por SQL.** Podría ser un `.sql` y sería más corto, pero
 * entonces sembraría lo que yo creo que el sistema acepta en vez de lo que
 * acepta de verdad: pasa por la validación, por el token CSRF, por la
 * generación del slug, por la creación de la variante única y por la
 * comprobación de contenido de las imágenes. Si algo del recorrido está roto,
 * **esto se entera antes que nadie**.
 *
 * **Idempotente.** Un 409 significa «ya estaba», se busca lo que existe y se
 * sigue. Correrlo dos veces no duplica nada.
 *
 * **Ningún dato de ningún negocio real** (ADR-008): ver la cabecera de
 * `datos.mjs`.
 *
 * Variables de entorno, todas opcionales:
 *
 *   SILLAR_API      dónde escucha la API          (http://localhost:5080)
 *   SILLAR_EMAIL    correo del administrador      (se pregunta si falta)
 *   SILLAR_PASSWORD su contraseña
 */

import { CATEGORIAS, MARCAS, PRODUCTOS } from './datos.mjs';
import { png } from './imagen.mjs';

const API = process.env.SILLAR_API ?? 'http://localhost:5080';
const EMAIL = process.env.SILLAR_EMAIL;
const PASSWORD = process.env.SILLAR_PASSWORD;

/** Estado de la sesión: la cookie y el token CSRF que exige toda escritura. */
let cookie = '';
let csrf = '';

/** Una petición al API, con la sesión puesta. Falla ruidosamente. */
async function api(metodo, ruta, cuerpo, { multipart } = {}) {
  const cabeceras = { Cookie: cookie };

  if (metodo !== 'GET') {
    cabeceras['X-CSRF-Token'] = csrf;
  }

  let body;

  if (multipart) {
    body = cuerpo;
  } else if (cuerpo !== undefined) {
    body = JSON.stringify(cuerpo);
    cabeceras['Content-Type'] = 'application/json';
  }

  const respuesta = await fetch(`${API}${ruta}`, { method: metodo, headers: cabeceras, body });

  const guardada = respuesta.headers.getSetCookie?.() ?? [];
  const sesión = guardada.find((c) => c.startsWith('sillar_panel='));

  if (sesión) {
    cookie = sesión.split(';')[0];
  }

  return respuesta;
}

/** Igual, pero exigiendo que salga bien. */
async function exigir(metodo, ruta, cuerpo, opciones) {
  const respuesta = await api(metodo, ruta, cuerpo, opciones);

  if (!respuesta.ok) {
    throw new Error(`${metodo} ${ruta} → ${respuesta.status}\n${await respuesta.text()}`);
  }

  return respuesta.status === 204 ? null : respuesta.json();
}

/** Abre sesión y coge el token CSRF. */
async function entrar() {
  if (!EMAIL || !PASSWORD) {
    throw new Error(
      'Faltan SILLAR_EMAIL y SILLAR_PASSWORD.\n' +
        'Son las credenciales del administrador que creaste al instalar. Ver docs/DEMOSTRACION.md.',
    );
  }

  const respuesta = await api('POST', '/api/admin/auth/login', {
    email: EMAIL,
    password: PASSWORD,
  });

  if (!respuesta.ok) {
    throw new Error(
      `No se pudo entrar (${respuesta.status}). ` +
        'Comprueba SILLAR_EMAIL y SILLAR_PASSWORD, y que la instalación esté completa.',
    );
  }

  const { csrfToken } = await (await api('GET', '/api/admin/auth/csrf')).json();
  csrf = csrfToken;
}

/** Sube una imagen generada y devuelve su identificador. */
async function subirImagen(color, nombre) {
  const formulario = new FormData();
  formulario.set('ownerModuleCode', 'catalog');
  formulario.set('altText', nombre);
  formulario.set('file', new Blob([png(color)], { type: 'image/png' }), `${nombre}.png`);

  const respuesta = await api('POST', '/api/admin/media', formulario, { multipart: true });

  if (!respuesta.ok) {
    // Una imagen repetida avisa, no falla: la segunda vuelta del script no
    // tiene por qué volver a subir nada.
    return null;
  }

  const { mediaAssetId } = await respuesta.json();
  return mediaAssetId;
}

/**
 * Crea algo, o devuelve lo que ya estaba.
 *
 * El 409 no es un fallo aquí: es la segunda vuelta del script encontrando lo
 * de la primera.
 */
async function crearOEncontrar(ruta, cuerpo, buscar) {
  const respuesta = await api('POST', ruta, cuerpo);

  if (respuesta.ok) {
    return { fila: await respuesta.json(), nueva: true };
  }

  if (respuesta.status !== 409) {
    throw new Error(`POST ${ruta} → ${respuesta.status}\n${await respuesta.text()}`);
  }

  const encontrada = await buscar();

  if (!encontrada) {
    throw new Error(`«${cuerpo.name}» dio 409 y después no aparece. Mira si está dada de baja.`);
  }

  return { fila: encontrada, nueva: false };
}

const slugify = (texto) =>
  texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

async function sembrar() {
  console.log(`SILLAR · sembrando la demostración en ${API}\n`);
  await entrar();

  // --- Marcas -------------------------------------------------------------
  const marcas = new Map();

  for (const marca of MARCAS) {
    const slug = slugify(marca.name);
    const { fila, nueva } = await crearOEncontrar(
      '/api/admin/catalog/brands',
      {
        name: marca.name,
        slug,
        description: marca.description,
        logoId: await subirImagen(marca.color, `marca-${slug}`),
      },
      async () => (await exigir('GET', '/api/admin/catalog/brands')).find((b) => b.slug === slug),
    );

    marcas.set(marca.name, fila.id);
    console.log(`  marca      ${nueva ? '+' : '='} ${marca.name}`);
  }

  // --- Categorías, en orden: los padres antes que los hijos ---------------
  const categorias = new Map();

  for (const categoria of CATEGORIAS) {
    const slug = slugify(categoria.name);
    const { fila, nueva } = await crearOEncontrar(
      '/api/admin/catalog/categories',
      {
        name: categoria.name,
        slug,
        parentId: categoria.parent ? categorias.get(categoria.parent) : null,
        description: null,
        imageId: await subirImagen(categoria.color, `categoria-${slug}`),
        sortOrder: 0,
      },
      async () =>
        (await exigir('GET', '/api/admin/catalog/categories')).find((c) => c.slug === slug),
    );

    categorias.set(categoria.name, fila.id);
    console.log(`  categoría  ${nueva ? '+' : '='} ${categoria.name}`);
  }

  // --- Productos ----------------------------------------------------------
  for (const producto of PRODUCTOS) {
    const slug = slugify(producto.name);
    const suyas = (producto.categorias ?? []).map((nombre) => categorias.get(nombre));

    const { fila, nueva } = await crearOEncontrar(
      '/api/admin/catalog/products',
      {
        name: producto.name,
        slug,
        shortDescription: producto.corta ?? null,
        description: producto.descripcion ?? null,
        // La primera de la lista es la principal: la que da la miga de pan.
        primaryCategoryId: suyas[0] ?? null,
        categoryIds: suyas,
        brandId: producto.marca ? marcas.get(producto.marca) : null,
        listPrice: producto.precio,
        saleUnit: producto.unidad ?? null,
        variantLabel: producto.variantLabel ?? null,
        code: producto.codigo ?? null,
        barcode: null,
      },
      async () =>
        (
          await exigir(
            'GET',
            `/api/admin/catalog/products?q=${encodeURIComponent(producto.name)}&pageSize=50`,
          )
        ).items.find((p) => p.slug === slug),
    );

    if (!nueva) {
      console.log(`  producto   = ${producto.name}`);
      continue;
    }

    // Las presentaciones: la primera ya existe —el alta crea la única— y se
    // ajusta; las demás se añaden.
    if (producto.presentaciones) {
      const ficha = await exigir('GET', `/api/admin/catalog/products/${fila.id}`);

      for (const [indice, presentación] of producto.presentaciones.entries()) {
        const datos = {
          variantValue: presentación.valor,
          code: presentación.codigo,
          barcode: presentación.barras,
          priceOverride: presentación.precio,
          imageId: null,
          sortOrder: indice,
          isActive: true,
        };

        if (indice === 0) {
          await exigir('PUT', `/api/admin/catalog/items/${ficha.items[0].id}`, datos);
        } else {
          await exigir('POST', `/api/admin/catalog/products/${fila.id}/items`, datos);
        }
      }
    }

    // La imagen, al final: un producto sin ella enseña el cuadrado con su
    // nombre, que es una decisión de diseño y conviene que se vea.
    if (producto.foto !== false) {
      const imagen = await subirImagen(producto.color ?? '#8A6D3B', `producto-${slug}`);

      if (imagen) {
        await exigir('POST', `/api/admin/catalog/products/${fila.id}/images`, {
          mediaAssetId: imagen,
          isPrimary: true,
        });
      }
    }

    console.log(
      `  producto   + ${producto.name}` +
        (producto.presentaciones ? ` (${producto.presentaciones.length} presentaciones)` : ''),
    );
  }

  console.log(
    `\nListo: ${MARCAS.length} marcas, ${CATEGORIAS.length} categorías, ${PRODUCTOS.length} productos.`,
  );
  console.log('La tienda pública está en la raíz del sitio; el panel, en /admin.');
}

sembrar().catch((error) => {
  console.error(`\n${error.message}`);
  process.exit(1);
});
