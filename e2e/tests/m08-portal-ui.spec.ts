import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Pruebas de estados/DOM de P1–P6. Las respuestas se controlan para provocar
 * cada estado visual; NO se usan como evidencia de propiedad o aislamiento
 * HTTP. Esa barrera pertenece a 07A/07B contra PostgreSQL y API reales.
 */

const CUSTOMER_A = {
  customerId: 'aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaaa',
  fullName: 'Cliente Alfa',
  email: 'alfa@example.test',
  emailVerified: true,
};

const CUSTOMER_B = {
  customerId: 'bbbbbbbb-bbbb-7bbb-8bbb-bbbbbbbbbbbb',
  fullName: 'Cliente Beta',
  email: 'beta@example.test',
  emailVerified: true,
};

const ORDER = {
  orderCode: 'P-2026-0042',
  status: 'pending_payment',
  totalAmount: 48.5,
  lineCount: 2,
  placedAt: '2026-10-10T15:00:00Z',
  paymentDueAt: '2026-10-11T15:00:00Z',
};

const WORK = {
  visibleCode: 'OS-2026-0017',
  currentStatus: 'in_progress',
  receivedAt: '2026-10-09T15:00:00Z',
  promisedAt: '2026-10-12T15:00:00Z',
  lastStatusChangedAt: '2026-10-10T15:00:00Z',
};

function overview(
  fullName = CUSTOMER_A.fullName,
  orders: unknown[] = [ORDER],
  work: unknown[] = [WORK],
) {
  return {
    profile: { fullName, email: 'cliente@example.test', phone: '999888777' },
    orders: { state: orders.length ? 'available' : 'empty', items: orders },
    work: { state: work.length ? 'available' : 'empty', items: work },
    customerId: 'no-debe-aparecer',
    boardPriority: 99,
  };
}

async function prepareBoot(
  page: Page,
  options: {
    customer?: typeof CUSTOMER_A | typeof CUSTOMER_B | null;
    modules?: string[];
  } = {},
) {
  const customer = options.customer === undefined ? CUSTOMER_A : options.customer;
  const modules = options.modules ?? ['crm', 'portal', 'sales', 'tracking'];

  await page.route('**/api/setup/status', (route) =>
    route.fulfill({ json: { setupRequired: false } }));
  await page.route('**/api/capabilities', (route) =>
    route.fulfill({
      json: {
        product: 'SILLAR',
        version: 'test',
        modules: modules.map((code) => ({ code, version: '1.0.0' })),
      },
    }));
  await page.route('**/api/admin/auth/me', (route) =>
    route.fulfill({ contentType: 'application/json', body: 'null' }));
  await page.route('**/api/customer/auth/me', (route) =>
    route.fulfill({ contentType: 'application/json', body: JSON.stringify(customer) }));
  await page.route('**/api/settings/public', (route) => route.fulfill({ json: {} }));
}

test('M08 UI — P1 limita recientes, separa proveedores y no muestra datos internos', async ({ page }) => {
  await prepareBoot(page);
  await page.route('**/api/portal/overview*', (route) =>
    route.fulfill({
      json: {
        ...overview(CUSTOMER_A.fullName, [ORDER, { ...ORDER, orderCode: 'P-2026-0043' }, { ...ORDER, orderCode: 'P-2026-0044' }, { ...ORDER, orderCode: 'P-2026-0045' }]),
        orders: { state: 'error', items: [] },
      },
    }));

  await page.goto('/mi-portal');

  await expect(page.getByRole('heading', { name: 'Mi portal', level: 1 })).toBeVisible();
  await expect(page.getByText('No pudimos cargar tus pedidos. Reintenta.')).toBeVisible();
  await expect(page.getByText(WORK.visibleCode)).toBeVisible();
  await expect(page.getByText('no-debe-aparecer')).toHaveCount(0);
  await expect(page.getByText('boardPriority')).toHaveCount(0);
});

test('M08 UI — P1 muestra como máximo tres pedidos y tres trabajos', async ({ page }) => {
  await prepareBoot(page);
  const orders = Array.from({ length: 4 }, (_, index) => ({ ...ORDER, orderCode: `P-2026-010${index}` }));
  const work = Array.from({ length: 4 }, (_, index) => ({ ...WORK, visibleCode: `OS-2026-010${index}` }));
  await page.route('**/api/portal/overview*', (route) => route.fulfill({ json: overview(CUSTOMER_A.fullName, orders, work) }));

  await page.goto('/mi-portal');

  await expect(page.getByRole('link', { name: 'Ver pedido' })).toHaveCount(3);
  await expect(page.getByRole('link', { name: 'Ver trabajo' })).toHaveCount(3);
  await expect(page.getByRole('link', { name: 'Ver todos los pedidos' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Ver todos los trabajos' })).toBeVisible();
});

test('M08 UI — P2 cubre carga, vacío y error recuperable', async ({ page }) => {
  await prepareBoot(page);
  let release: (() => void) | undefined;
  const waiting = new Promise<void>((resolve) => { release = resolve; });
  let attempt = 0;
  await page.route('**/api/portal/overview*', async (route) => {
    attempt += 1;
    if (attempt === 1) {
      await waiting;
      await route.fulfill({ json: overview(CUSTOMER_A.fullName, [], []) });
      return;
    }

    await route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{}' });
  });

  await page.goto('/mi-portal/pedidos');
  await expect(page.getByText('Cargando tus pedidos')).toBeVisible();
  release?.();
  await expect(page.getByText('Todavía no tienes pedidos')).toBeVisible();
  await page.reload();
  await expect(page.getByText('No pudimos cargar tus pedidos. Reintenta.')).toBeVisible();
});

test('M08 UI — P3 conserva el mensaje de vencimiento y el 404 indistinguible', async ({ page }) => {
  await prepareBoot(page);
  await page.route('**/api/sales/my-orders/P-2026-0042', (route) =>
    route.fulfill({
      json: {
        ...ORDER,
        status: 'expired',
        lines: [{
          productName: 'Cuaderno',
          variantValue: 'A4',
          saleUnit: 'unidad',
          quantity: 2,
          unitPrice: 24.25,
          subtotal: 48.5,
        }],
      },
    }));
  await page.route('**/api/sales/my-orders/P-2026-9999', (route) => route.fulfill({ status: 404, json: {} }));

  await page.goto('/mi-portal/pedidos/P-2026-0042');
  await expect(page.getByText('El plazo para pagar venció. El pedido no está cancelado por ese motivo.')).toBeVisible();
  await page.goto('/mi-portal/pedidos/P-2026-9999');
  await expect(page.getByText('No encontramos ese pedido.')).toBeVisible();
});

test('M08 UI — P5 distingue proveedor ausente de 503 sin causa verificable', async ({ page }) => {
  await prepareBoot(page, { modules: ['crm', 'portal', 'sales'] });
  await page.goto(`/mi-portal/trabajos/${WORK.visibleCode}`);
  await expect(page.getByText('El seguimiento de trabajos no está disponible en esta instalación.')).toBeVisible();

  await page.unrouteAll();
  await prepareBoot(page);
  await page.route(`**/api/portal/work/${WORK.visibleCode}`, (route) =>
    route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{}' }));
  await page.reload();
  await expect(page.getByText('No pudimos cargar este trabajo. Reintenta.')).toBeVisible();
  await expect(page.getByText('El seguimiento de trabajos no está disponible en esta instalación.')).toHaveCount(0);
});

test('M08 UI — P5 mantiene el 404 uniforme y no expone seguimiento interno', async ({ page }) => {
  await prepareBoot(page);
  await page.route('**/api/portal/work/OS-2026-AJENO', (route) =>
    route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{}' }));
  await page.route(`**/api/portal/work/${WORK.visibleCode}`, (route) =>
    route.fulfill({
      json: {
        ...WORK,
        items: [{
          serviceName: 'Anillado',
          publicDescription: 'Listo para revisión del cliente.',
          quantity: 1,
          saleUnit: 'servicio',
        }],
        trackingNotes: 'nota privada que no debe verse',
        boardPriority: 0,
        internalDueAt: '2026-10-11T00:00:00Z',
      },
    }));

  await page.goto('/mi-portal/trabajos/OS-2026-AJENO');
  await expect(page.getByText('No encontramos ese trabajo.')).toBeVisible();
  await page.goto(`/mi-portal/trabajos/${WORK.visibleCode}`);
  await expect(page.getByText('Anillado')).toBeVisible();
  await expect(page.getByText('nota privada que no debe verse')).toHaveCount(0);
  await expect(page.getByText('boardPriority')).toHaveCount(0);
});

test('M08 UI — un 401 retira el contenido privado y vuelve al acceso', async ({ page }) => {
  await prepareBoot(page);
  await page.route(`**/api/portal/work/${WORK.visibleCode}`, (route) =>
    route.fulfill({ status: 401, contentType: 'application/problem+json', body: '{}' }));

  await page.goto(`/mi-portal/trabajos/${WORK.visibleCode}`);

  await expect(page).toHaveURL(/\/entrar$/);
  await expect(page.getByText(WORK.visibleCode)).toHaveCount(0);
});

test('M08 UI — cambiar de cliente invalida el contenido de A', async ({ page }) => {
  let current = CUSTOMER_A;
  await prepareBoot(page, { customer: CUSTOMER_A });
  await page.route('**/api/portal/overview*', (route) =>
    route.fulfill({ json: overview(current.fullName, [{ ...ORDER, orderCode: current === CUSTOMER_A ? 'PEDIDO-ALFA' : 'PEDIDO-BETA' }], []) }));
  await page.route('**/api/customer/auth/logout', (route) => route.fulfill({ status: 204 }));
  await page.route('**/api/customer/auth/login', async (route) => {
    current = CUSTOMER_B;
    await route.fulfill({ json: { customer: CUSTOMER_B, csrfToken: 'token-beta' } });
  });

  await page.goto('/mi-portal');
  await expect(page.getByText('PEDIDO-ALFA')).toBeVisible();
  await page.getByRole('link', { name: 'Mi cuenta' }).click();
  await page.getByRole('button', { name: 'Cerrar sesión' }).click();
  await page.goto('/entrar');
  await page.getByLabel('Correo').fill(CUSTOMER_B.email);
  await page.getByLabel('Contraseña').fill('clave-de-prueba');
  await page.getByRole('button', { name: 'Entrar' }).click();
  await page.getByRole('link', { name: 'Volver a la tienda' }).click();
  await page.getByRole('link', { name: 'Ir a Mi portal' }).click();

  await expect(page.getByText('PEDIDO-BETA')).toBeVisible();
  await expect(page.getByText('PEDIDO-ALFA')).toHaveCount(0);
});

test('M08 UI — navegación local, tema oscuro, móvil y accesibilidad', async ({ page }) => {
  await prepareBoot(page);
  await page.route('**/api/portal/overview*', (route) => route.fulfill({ json: overview() }));
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ colorScheme: 'dark', reducedMotion: 'reduce' });

  await page.goto('/mi-portal');
  const nav = page.getByRole('navigation', { name: 'Navegación de Mi portal' });
  await expect(nav.getByRole('link')).toHaveCount(4);
  await expect(nav.getByRole('link', { name: 'Mi cuenta' })).toHaveAttribute('href', '/mi-cuenta');
  await nav.getByRole('link', { name: 'Mi portal' }).focus();
  await expect(nav.getByRole('link', { name: 'Mi portal' })).toBeFocused();

  const results = await new AxeBuilder({ page }).include('main').analyze();
  expect(results.violations).toEqual([]);
});
