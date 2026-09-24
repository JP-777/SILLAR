/**
 * M02: ciclo diagnóstico con stack E2E efímero.
 * No certifica el cierre mientras existan criterios funcionales abiertos.
 */
import path from 'node:path';
import type { APIRequestContext, Page, TestInfo } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { psql, psqlArchivo } from '../setup/docker.js';
import { CONNECTION_STRING, ROOT } from '../setup/env.js';
import { run } from '../setup/shell.js';

const sello = Date.now();
const nombre = `Producto ciclo M02 ${sello}`;
const slug = `producto-ciclo-m02-${sello}`;
const enlace = `https://youtube.com/@sillarm02ciclo${sello}`;

async function csrf(api: APIRequestContext) {
  const response = await api.get('/api/admin/auth/csrf');
  expect(response.ok()).toBe(true);
  const data = await response.json() as { csrfToken: string };
  return { 'X-CSRF-Token': data.csrfToken };
}

async function crearProducto(api: APIRequestContext) {
  const response = await api.post('/api/admin/catalog/products', {
    headers: await csrf(api),
    data: {
      name: nombre,
      slug,
      shortDescription: null,
      description: null,
      primaryCategoryId: null,
      categoryIds: [],
      brandId: null,
      listPrice: 12,
      saleUnit: null,
      variantLabel: null,
      code: null,
      barcode: null,
    },
  });

  expect(
    response.ok(),
    `crear producto: ${response.status()} ${await response.text()}`,
  ).toBe(true);

  return (await response.json() as { id: string }).id;
}

async function publicarCms(
  api: APIRequestContext,
  productId: string,
) {
  const featured = await api.post(
    '/api/admin/cms/featured-products',
    {
      headers: await csrf(api),
      data: {
        productId,
        startsAt: null,
        endsAt: null,
      },
    },
  );

  expect(
    featured.ok(),
    `destacar producto: ${featured.status()} ${await featured.text()}`,
  ).toBe(true);

  const social = await api.post('/api/admin/cms/social-links', {
    headers: await csrf(api),
    data: {
      platform: 'youtube',
      url: enlace,
    },
  });

  expect(
    social.ok(),
    `publicar red: ${social.status()} ${await social.text()}`,
  ).toBe(true);
}

async function capacidades(api: APIRequestContext) {
  const response = await api.get('/api/capabilities');
  expect(response.ok()).toBe(true);

  const data = await response.json() as {
    modules: { code: string }[];
  };

  return data.modules.map((item) => item.code).sort();
}

async function cambiarModulo(
  page: Page,
  accion: 'Activar' | 'Desactivar',
) {
  await page.goto('/admin/modulos');

  await duringExpectedOutage(page, async () => {
    await page.locator('#modulo-cms')
      .getByRole('switch')
      .click();

    await page.getByRole('alertdialog')
      .getByRole('button', {
        name: new RegExp(`^${accion}`),
      })
      .click();

    const overlay = page.getByRole('alertdialog', {
      name: 'Aplicando el cambio',
    });

    await expect(overlay).toBeVisible();
    await expect(overlay).toBeHidden({
      timeout: 90_000,
    });
  });
}

async function revisarSinCms(
  page: Page,
  otrasCapacidades: string[],
  testInfo: TestInfo,
  etapa: string,
) {
  const activas = await capacidades(page.request);

  expect(activas).not.toContain('cms');
  expect(activas).toEqual(otrasCapacidades);

  await page.goto('/admin');

  const menu = page.getByRole('navigation', {
    name: 'Secciones del panel',
  });

  await expect(menu).toBeVisible();
  await expect(
    menu.locator('a[href^="/admin/contenido/"]'),
  ).toHaveCount(0);

  await page.goto('/admin/contenido/banners');

  await expect(page).not.toHaveURL(
    /\/admin\/contenido\/banners$/,
  );

  await page.goto('/');

  await expect(
    page.getByRole('heading', { level: 1 }),
  ).toBeVisible();

  for (const id of [
    'cms-banners',
    'cms-promotions',
    'cms-featured-products',
    'cms-featured-projects',
  ]) {
    await expect(
      page.locator(
        `section[aria-labelledby="${id}-title"]`,
      ),
    ).toHaveCount(0);
  }

  await expect(
    page.getByRole('contentinfo'),
  ).toHaveCount(0);

  const catalog = await page.request.get(
    `/api/catalog/products/${slug}`,
  );

  expect(
    catalog.ok(),
    `Catálogo dejó de responder: ${catalog.status()}`,
  ).toBe(true);

  await page.goto(`/producto/${slug}`);

  await expect(
    page.getByRole('heading', { name: nombre }),
  ).toBeVisible();

  await testInfo.attach(`m02-${etapa}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png',
  });
}

async function estadoAjeno() {
  return {
    usuarios: await psql(
      'SELECT count(*) FROM core.admin_users',
    ),
    ajustes: await psql(
      'SELECT count(*) FROM core.site_settings',
    ),
    medios: await psql(
      'SELECT count(*) FROM core.media_assets',
    ),
    productos: await psql(
      'SELECT count(*) FROM catalog.products',
    ),
    tablasCore: await psql(
      "SELECT count(*) FROM information_schema.tables " +
      "WHERE table_schema = 'core'",
    ),
    tablasCatalogo: await psql(
      "SELECT count(*) FROM information_schema.tables " +
      "WHERE table_schema = 'catalog'",
    ),
    tablasCrm: await psql(
      "SELECT count(*) FROM information_schema.tables " +
      "WHERE table_schema = 'crm'",
    ),
  };
}

async function tablasCms() {
  return psql(
    "SELECT count(*) FROM information_schema.tables " +
    "WHERE table_schema = 'cms'",
  );
}

test(
  '[M02-CICLO] desmontar e instalar sin romper CORE, Catálogo ni superficies',
  async ({ page }, testInfo) => {
    test.setTimeout(420_000);

    await loginAsE2eAdmin(page);

    const productId = await crearProducto(page.request);
    await publicarCms(page.request, productId);

    await page.goto('/');

    await expect(
      page.locator(
        'section[aria-labelledby="cms-featured-products-title"]',
      ).getByText(nombre),
    ).toBeVisible();

    await expect(
      page.getByRole('navigation', {
        name: 'Redes sociales',
      }).getByRole('link', { name: 'YouTube' }),
    ).toBeVisible();

    const inicio = await estadoAjeno();
    const cmsInicial = await tablasCms();
    const iniciales = await capacidades(page.request);
    const otras = iniciales.filter(
      (code) => code !== 'cms',
    );

    expect(iniciales).toContain('cms');
    expect(Number(cmsInicial)).toBeGreaterThan(0);
    expect(Number(inicio.usuarios)).toBeGreaterThan(0);

    // Integración instalada e idempotente.
    await psqlArchivo(
      '/scripts/integrations/cms_catalog.sql',
    );

    await psqlArchivo(
      '/scripts/integrations/cms_catalog.sql',
    );

    const fkInicial = await psql(
      "SELECT count(*) FROM pg_constraint " +
      "WHERE conrelid = " +
      "'cms.featured_products'::regclass " +
      "AND conname = " +
      "'fk_featured_products_product_id'",
    );

    expect(fkInicial).toBe('1');

    // Desactivar el módulo antes de tocar su schema.
    await cambiarModulo(page, 'Desactivar');

    await revisarSinCms(
      page,
      otras,
      testInfo,
      'desactivado',
    );

    // Retirar primero la integración física.
    await psqlArchivo(
      '/scripts/integrations/cms_catalog_drop.sql',
    );

    await psqlArchivo(
      '/scripts/integrations/cms_catalog_drop.sql',
    );

    const fkRetirada = await psql(
      "SELECT count(*) FROM pg_constraint " +
      "WHERE conrelid = " +
      "'cms.featured_products'::regclass " +
      "AND conname = " +
      "'fk_featured_products_product_id'",
    );

    expect(fkRetirada).toBe('0');

    const snapshots = await psql(
      'SELECT count(*) FROM cms.featured_products ' +
      'WHERE product_id IS NULL',
    );

    expect(Number(snapshots)).toBeGreaterThan(0);

    // Desinstalación física real, repetida.
    await psqlArchivo(
      '/scripts/modules/cms/99_drop.sql',
    );

    await psqlArchivo(
      '/scripts/modules/cms/99_drop.sql',
    );

    expect(await tablasCms()).toBe('0');

    const sinCms = await psql(
      "SELECT count(*) FROM information_schema.schemata " +
      "WHERE schema_name = 'cms'",
    );

    expect(sinCms).toBe('0');

    expect(await estadoAjeno()).toEqual(inicio);

    await revisarSinCms(
      page,
      otras,
      testInfo,
      'desinstalado',
    );

    // Reinstalar únicamente las migraciones de CMS.
    await run(
      'dotnet',
      [
        'ef',
        'database',
        'update',
        '--project',
        'Sillar.Modules.Cms',
        '--startup-project',
        'Sillar.Api',
      ],
      {
        cwd: path.join(ROOT, 'backend'),
        env: {
          ConnectionStrings__Default: CONNECTION_STRING,
        },
      },
    );

    await psqlArchivo(
      '/scripts/modules/cms/02_seed.sql',
    );

    await psqlArchivo(
      '/scripts/modules/cms/02_seed.sql',
    );

    expect(await tablasCms()).toBe(cmsInicial);

    await psqlArchivo(
      '/scripts/integrations/cms_catalog.sql',
    );

    await psqlArchivo(
      '/scripts/integrations/cms_catalog.sql',
    );

    const fkRestaurada = await psql(
      "SELECT count(*) FROM pg_constraint " +
      "WHERE conrelid = " +
      "'cms.featured_products'::regclass " +
      "AND conname = " +
      "'fk_featured_products_product_id'",
    );

    expect(fkRestaurada).toBe('1');

    expect(await estadoAjeno()).toEqual(inicio);

    // Comprobar que el módulo se puede activar de nuevo.
    await cambiarModulo(page, 'Activar');

    const restauradas = await capacidades(page.request);
    expect(restauradas).toEqual(iniciales);

    await page.goto('/admin/contenido/banners');

    await expect(
      page.getByRole('heading', { name: 'Banners' }),
    ).toBeVisible();

    // La instalación nueva debe poder publicar contenido.
    await publicarCms(page.request, productId);

    await page.goto('/');

    await expect(
      page.locator(
        'section[aria-labelledby="cms-featured-products-title"]',
      ).getByText(nombre),
    ).toBeVisible();

    await expect(
      page.getByRole('navigation', {
        name: 'Redes sociales',
      }).getByRole('link', { name: 'YouTube' }),
    ).toBeVisible();

    await page.goto(`/producto/${slug}`);

    await expect(
      page.getByRole('heading', { name: nombre }),
    ).toBeVisible();

    expect(await estadoAjeno()).toEqual(inicio);

    await testInfo.attach(
      'm02-reinstalado.png',
      {
        body: await page.screenshot({ fullPage: true }),
        contentType: 'image/png',
      },
    );
  },
);
