/**
 * El envoltorio de `page.goto` (`fixtures/base.ts`) dice **qué** había en
 * pantalla cuando no encuentra el armazón, en vez de «no llegó a pintar» para
 * todo.
 *
 * Cada caso provoca un estado de arranque con la API simulada —nada llega al
 * servidor del stack— y comprueba que el fallo, que sigue ocurriendo igual y
 * con el mismo plazo, lo nombra bien y adjunta su diagnóstico.
 *
 * Las navegaciones van dentro de `duringExpectedOutage`: provocan a propósito
 * respuestas fallidas, y Chromium las anota en la consola.
 */
import type { Page, TestInfo } from '@playwright/test';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';

const json = (status: number, body: unknown) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

/** Todo `/api` responde 404 salvo lo que cada caso intercepte después. */
async function sinBackend(page: Page) {
  await page.route('**/api/**', (route) => route.fulfill({ status: 404, body: '' }));
}

/** Navega, espera el fallo del envoltorio y devuelve su mensaje. */
async function falloAlNavegar(page: Page, ruta: string): Promise<string> {
  const error = await duringExpectedOutage(page, () => page.goto(ruta).then(
    () => null,
    (e: unknown) => e,
  ));
  expect(error, 'el envoltorio dejó pasar una pantalla sin armazón').toBeInstanceOf(Error);
  return (error as Error).message;
}

/** El estado que el envoltorio adjuntó en `diagnostico-arranque.json`. */
function estadoAdjunto(testInfo: TestInfo): string {
  const adjunto = testInfo.attachments.find((a) => a.name === 'diagnostico-arranque.json');
  expect(adjunto, 'el envoltorio no adjuntó su diagnóstico').toBeDefined();
  return (JSON.parse(String(adjunto!.body)) as { estado: string }).estado;
}

test('Arnés: una página de error de plataforma se nombra como tal, no como «no pintó»', async ({ page }, testInfo) => {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) => route.fulfill(json(500, { title: 'fallo' })));

  const mensaje = await falloAlNavegar(page, '/');

  expect(mensaje).toContain('pagina-de-error');
  expect(mensaje).toContain('No se pudo cargar el sistema');
  expect(estadoAdjunto(testInfo)).toBe('pagina-de-error');
});

test('Arnés: la pantalla de migraciones pendientes se nombra como tal', async ({ page }, testInfo) => {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) =>
    route.fulfill(json(200, { setupRequired: true, migrationsPending: true })));

  const mensaje = await falloAlNavegar(page, '/');

  expect(mensaje).toContain('migraciones-pendientes');
  expect(estadoAdjunto(testInfo)).toBe('migraciones-pendientes');
});

test('Arnés: el asistente de instalación se nombra como tal', async ({ page }, testInfo) => {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) => route.fulfill(json(200, { setupRequired: true })));

  const mensaje = await falloAlNavegar(page, '/');

  expect(mensaje).toContain('asistente-de-instalacion');
  expect(estadoAdjunto(testInfo)).toBe('asistente-de-instalacion');
});

test('Arnés: un arranque que no termina se distingue de uno que terminó en error', async ({ page }, testInfo) => {
  await sinBackend(page);
  // La primera pregunta del arranque no recibe respuesta nunca.
  await page.route('**/api/setup/status', () => new Promise<void>(() => {}));

  try {
    const mensaje = await falloAlNavegar(page, '/');

    expect(mensaje).toContain('arranque-sin-terminar');
    expect(mensaje).not.toContain('pagina-de-error');
    expect(estadoAdjunto(testInfo)).toBe('arranque-sin-terminar');
  } finally {
    await page.unrouteAll({ behavior: 'ignoreErrors' });
  }
});

test('Arnés: el diagnóstico no copia la consulta de la URL, que puede llevar un token', async ({ page }, testInfo) => {
  await sinBackend(page);
  await page.route('**/api/setup/status', (route) => route.fulfill(json(500, { title: 'fallo' })));

  const mensaje = await falloAlNavegar(page, '/verificar-correo?token=secreto-de-un-solo-uso');
  expect(mensaje).not.toContain('secreto-de-un-solo-uso');

  const adjunto = testInfo.attachments.find((a) => a.name === 'diagnostico-arranque.json');
  expect(String(adjunto?.body)).not.toContain('secreto-de-un-solo-uso');
  expect(JSON.parse(String(adjunto!.body)).ruta).toBe('/verificar-correo');
});
