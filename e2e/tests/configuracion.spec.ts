import { loginAsE2eAdmin, loginComoAdminMenor } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * Configuración — `VERIFICACION-VISUAL-CORE.md` §8.
 *
 * Dos reglas, y la segunda solo se ve desde un rol que no lo puede todo: el
 * segundo usuario lo siembra `global-setup.ts` justo para esto.
 */

test('Los datos sin definir se destacan y se cuentan', async ({ page }) => {
  const record = themeRecorder(page, 'configuracion');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/configuracion');

  await expect(page.locator('.set-row').first()).toBeVisible();

  // Se cuentan: una instalación recién hecha tiene que leerse como una lista
  // de tareas, no como un formulario mudo. El seed deja nueve claves en
  // PENDIENTE_DEFINIR (database/modules/core/02_seed.sql:31-39), pero el
  // número exacto no se fija aquí — otras pruebas escriben configuración y lo
  // que importa es que el recuento coincida con lo que hay marcado.
  const marcadas = page.locator('.set-row[data-pending="true"]');
  const cuantas = await marcadas.count();
  expect(cuantas).toBeGreaterThan(0);

  const aviso = page.getByRole('status').filter({ hasText: 'por completar' });
  await expect(aviso).toBeVisible();
  await expect(aviso).toContainText(`Faltan ${cuantas} datos por completar`);

  // Y se destacan una por una, no solo en el recuento de arriba.
  await expect(marcadas.first().getByText('Sin definir')).toBeVisible();

  // El valor centinela es de la base, no texto de interfaz: no debe verse.
  expect(await page.locator('main').innerText()).not.toContain('PENDIENTE_DEFINIR');

  await record('configuracion-con-datos-sin-definir');
});

test('Con rol admin el interruptor de publicación está deshabilitado y dice por qué', async ({ page }) => {
  const record = themeRecorder(page, 'configuracion');
  await loginComoAdminMenor(page);
  await page.goto('/admin/configuracion');

  const fila = page.locator('.set-row').first();
  await expect(fila).toBeVisible();

  // Deshabilitado, no oculto: ocultarlo haría creer que el dato no puede ser
  // público. Ver el comentario de SettingRow.tsx:80-82.
  //
  // Lo visible es el `.ui-switch`, no el `<input role="switch">`: el input va
  // oculto por CSS y encima se pinta un `.ui-switch__track`, que es el patrón
  // habitual de interruptor con estilo propio. Así que se comprueban las dos
  // mitades — que el control se vea, y que el que manda esté deshabilitado.
  await expect(fila.locator('.ui-switch')).toBeVisible();
  await expect(fila.getByRole('switch')).toBeDisabled();

  // Y con su razón escrita al lado. Deshabilitado sin explicación no vale.
  await expect(fila).toContainText('Cambiar esto exige el rol de administrador principal');

  await record('interruptor-deshabilitado-con-su-razon');
});

test('Con rol super_admin el interruptor de publicación sí se puede usar', async ({ page }) => {
  // El contraste que demuestra que lo de arriba es por el rol y no un fallo
  // general del render.
  await loginAsE2eAdmin(page);
  await page.goto('/admin/configuracion');

  const fila = page.locator('.set-row').first();
  await expect(fila.getByRole('switch')).toBeEnabled();
  await expect(fila).not.toContainText('Cambiar esto exige el rol de administrador principal');
});
