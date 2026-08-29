import AxeBuilder from '@axe-core/playwright';
import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

test('M04 — contacto público y panel quedan limpios en axe, claro y oscuro', async ({ page }) => {
  const record = themeRecorder(page, 'crm-final');

  await page.goto('/contacto');
  await expect(page.getByRole('heading', { name: 'Contacto' })).toBeVisible();
  await record('contacto-publico');

  await loginAsE2eAdmin(page);
  await page.goto('/admin/clientes');
  await expect(page.getByRole('heading', { name: 'Clientes' })).toBeVisible();
  await record('panel-clientes');
});

test('M04 — contacto respeta reduced motion y sigue limpio en axe', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/contacto');
  await expect(page.getByRole('heading', { name: 'Contacto' })).toBeVisible();

  const results = await new AxeBuilder({ page }).analyze();
  expect(
    results.violations,
    results.violations
      .map((violation) => `${violation.id}: ${violation.description}`)
      .join('\n'),
  ).toEqual([]);
});
