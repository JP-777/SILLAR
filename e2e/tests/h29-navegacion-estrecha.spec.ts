import type { Page } from '@playwright/test';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

type Rol = 'editor' | 'admin' | 'super_admin';

const MODULOS_TODOS = ['core', 'catalog', 'cms', 'crm'] as const;
const MODULOS_COMPACTOS = ['core', 'catalog'] as const;

async function prepararCaso(
  page: Page,
  rol: Rol,
  modulos: readonly string[],
): Promise<void> {
  await page.setViewportSize({ width: 390, height: 844 });

  // La cookie sigue siendo la de un usuario real del stack. Solo se simulan
  // rol y capacidades para ejercer de forma determinista los tres casos que
  // fijó diseño: 16, 10 y 5 destinos.
  await loginAsE2eAdmin(page);

  await page.route('**/api/capabilities', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        product: 'SILLAR',
        version: 'h29-e2e',
        modules: modulos.map((code) => ({
          code,
          version: 'e2e',
        })),
      }),
    });
  });

  await page.route('**/api/admin/auth/me', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 777,
        fullName: 'Saúl Sivincha',
        email: 'h29@example.test',
        role: rol,
      }),
    });
  });

  await page.goto('/admin/catalogo/productos');
  await expect(page.getByRole('heading', { name: 'Productos' })).toBeVisible();
}

test('H29: a 390 px hay topbar compacta y 16 destinos usan grupos plegables', async ({
  page,
}) => {
  await prepararCaso(page, 'super_admin', MODULOS_TODOS);

  await expect(page.locator('.ly-sidebar')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Abrir navegación' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Abrir cuenta' })).toBeVisible();
  await expect(page.getByRole('dialog', { name: 'Navegación' })).toHaveCount(0);

  const sinOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth <= window.innerWidth,
  );
  expect(sinOverflow, 'la página se sale horizontalmente a 390 px').toBe(true);

  const heading = await page.getByRole('heading', { name: 'Productos' }).boundingBox();
  expect(heading, 'Productos no tiene caja visible').not.toBeNull();
  expect(heading!.y, 'el contenido no aparece en el primer viewport').toBeLessThan(844);

  const menu = page.getByRole('button', { name: 'Abrir navegación' });
  await menu.click();

  const drawer = page.getByRole('dialog', { name: 'Navegación' });
  await expect(drawer).toBeVisible();
  await expect(drawer).toContainText('16 destinos · grupos plegables');

  const grupos = drawer.locator('.ly-mobile-nav__group-toggle');
  await expect(grupos).toHaveCount(4);

  const sistema = drawer
    .locator('.ly-mobile-nav__group-toggle')
    .filter({ hasText: 'Sistema' });
  const catalogo = drawer
    .locator('.ly-mobile-nav__group-toggle')
    .filter({ hasText: 'Catálogo' });

  await expect(sistema).toHaveAttribute('aria-expanded', 'false');
  await expect(catalogo).toHaveAttribute('aria-expanded', 'true');

  await page.keyboard.press('Escape');
  await expect(drawer).toBeHidden();
  await expect(menu).toBeFocused();

  await menu.click();
  await drawer.getByRole('link', { name: 'Categorías', exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/catalogo\/categorias$/);
  await expect(drawer).toBeHidden();
});

test('H29: editor con 10 destinos conserva disclosures', async ({ page }) => {
  await prepararCaso(page, 'editor', MODULOS_TODOS);

  await page.getByRole('button', { name: 'Abrir navegación' }).click();

  const drawer = page.getByRole('dialog', { name: 'Navegación' });
  await expect(drawer).toContainText('10 destinos · grupos plegables');

  // Sistema + Catálogo + Contenido. Clientes exige un rol superior.
  await expect(drawer.locator('.ly-mobile-nav__group-toggle')).toHaveCount(3);
  await expect(
    drawer.locator('.ly-mobile-nav__group-toggle').filter({ hasText: 'Catálogo' }),
  ).toHaveAttribute('aria-expanded', 'true');
});

test('H29: editor con CORE y Catálogo tiene 5 destinos sin disclosures', async ({
  page,
}) => {
  await prepararCaso(page, 'editor', MODULOS_COMPACTOS);

  await page.getByRole('button', { name: 'Abrir navegación' }).click();

  const drawer = page.getByRole('dialog', { name: 'Navegación' });
  await expect(drawer).toContainText('5 destinos · todos visibles');

  await expect(drawer.locator('.ly-mobile-nav__group-toggle')).toHaveCount(0);
  await expect(drawer.locator('.ly-mobile-nav__group-title')).toHaveCount(2);
  await expect(drawer.locator('.ly-mobile-nav__link')).toHaveCount(5);

  await expect(drawer.getByRole('link', { name: 'Inicio', exact: true })).toBeVisible();
  await expect(drawer.getByRole('link', { name: 'Archivos', exact: true })).toBeVisible();
  await expect(drawer.getByRole('link', { name: 'Productos', exact: true })).toBeVisible();
  await expect(drawer.getByRole('link', { name: 'Categorías', exact: true })).toBeVisible();
  await expect(drawer.getByRole('link', { name: 'Marcas', exact: true })).toBeVisible();
});

test('H29: cuenta estrecha conserva tema, contraseña y cierre de sesión', async ({
  page,
}) => {
  await prepararCaso(page, 'super_admin', MODULOS_TODOS);

  const cuenta = page.getByRole('button', { name: 'Abrir cuenta' });
  await cuenta.click();

  const panel = page.locator('#cuenta-estrecha');
  await expect(panel).toBeVisible();
  await expect(panel).toContainText('Saúl Sivincha');
  await expect(panel).toContainText('Administrador principal');
  await expect(panel.getByRole('link', { name: 'Mi contraseña' })).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible();

  const tema = panel.getByRole('button', { name: /Cambiar a tema/ });
  await expect(tema).toBeVisible();

  const temaAntes = await page.locator('html').getAttribute('data-theme');
  await tema.click();
  const temaDespues = await page.locator('html').getAttribute('data-theme');

  expect(temaDespues, 'el control de tema dejó de funcionar dentro de la cuenta')
    .not.toBe(temaAntes);

  await page.keyboard.press('Escape');
  await expect(panel).toBeHidden();
  await expect(cuenta).toBeFocused();
});

test('H29: movimiento reducido elimina el desplazamiento de las superficies', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await prepararCaso(page, 'super_admin', MODULOS_TODOS);

  await page.getByRole('button', { name: 'Abrir navegación' }).click();

  const drawer = page.locator('.ui-drawer--left');
  await expect(drawer).toBeVisible();

  const movimientoDrawer = await drawer.evaluate((element) => {
    const style = getComputedStyle(element);
    return {
      animationName: style.animationName,
      transform: style.transform,
      transitionProperty: style.transitionProperty,
      transitionDuration: style.transitionDuration,
    };
  });

  expect(movimientoDrawer.animationName).toBe('none');
  expect(movimientoDrawer.transform).toBe('none');
  expect(movimientoDrawer.transitionProperty).toBe('opacity');
  expect(movimientoDrawer.transitionDuration).toBe('0.08s');

  await page.keyboard.press('Escape');

  await page.getByRole('button', { name: 'Abrir cuenta' }).click();
  const cuenta = page.locator('#cuenta-estrecha');

  const movimientoCuenta = await cuenta.evaluate((element) => {
    const style = getComputedStyle(element);
    return {
      animationName: style.animationName,
      transform: style.transform,
      transitionProperty: style.transitionProperty,
      transitionDuration: style.transitionDuration,
    };
  });

  expect(movimientoCuenta.animationName).toBe('none');
  expect(movimientoCuenta.transform).toBe('none');
  expect(movimientoCuenta.transitionProperty).toBe('opacity');
  expect(movimientoCuenta.transitionDuration).toBe('0.08s');
});

test('H29: escritorio conserva la barra lateral y oculta los controles móviles', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await loginAsE2eAdmin(page);
  await page.goto('/admin/catalogo/productos');

  await expect(page.getByRole('heading', { name: 'Productos' })).toBeVisible();
  await expect(page.locator('.ly-sidebar')).toBeVisible();
  await expect(page.locator('.ly-mobile-menu')).toBeHidden();
  await expect(page.locator('.ly-mobile-account')).toBeHidden();
});
