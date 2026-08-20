import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * Dos pestañas escribiendo a la vez, sin un solo 403.
 *
 * Es la prueba de la ADR-012: el token CSRF es **determinista**, derivado de
 * la identidad de la sesión por HMAC, así que dos pestañas de la misma sesión
 * obtienen el mismo token y ninguna invalida a la otra. Con el esquema
 * anterior —token rotatorio— la segunda pestaña en escribir recibía un 403, y
 * el síntoma aparecía justo cuando alguien tenía el panel abierto dos veces,
 * que es lo normal.
 *
 * La alternancia importa: A, B, A, B. Un solo cambio en cada una no probaría
 * nada, porque el fallo aparecía al volver a la primera después de que la
 * segunda hubiera rotado el token.
 */

/** Escribe un valor en una clave de configuración y espera su confirmación. */
async function guardar(page: import('@playwright/test').Page, clave: string, valor: string) {
  const fila = page.locator('.set-row').filter({ has: page.locator(`code:text-is("${clave}")`) });

  await fila.getByRole('textbox').fill(valor);
  await fila.getByRole('button', { name: 'Guardar' }).click();

  // El aviso de escritura correcta. Si hubiera 403, la pantalla mostraría el
  // mensaje de fallo en su lugar y esta espera se agotaría.
  await expect(page.getByRole('status').filter({ hasText: 'Se guardó' })).toBeVisible();
}

test('Dos pestañas escriben alternando sin que ninguna reciba un 403', async ({ page, context }) => {
  await loginAsE2eAdmin(page);

  const tabA = page;
  const tabB = await context.newPage();

  // Misma sesión: la cookie vive en el contexto, no en la pestaña.
  await tabA.goto('/admin/configuracion');
  await tabB.goto('/admin/configuracion');

  await expect(tabA.locator('.set-row').first()).toBeVisible();
  await expect(tabB.locator('.set-row').first()).toBeVisible();

  // A, B, A, B — sobre claves distintas para que ninguna escritura dependa de
  // lo que la otra dejó, y la única variable sea el token.
  await guardar(tabA, 'contact_phone', '+51 999 111 222');
  await guardar(tabB, 'business_hours', 'Lunes a sábado, 9 a 19');
  await guardar(tabA, 'business_reference', 'Frente al parque');
  await guardar(tabB, 'main_message', 'Todo para tu colegio');

  // Y la primera sigue viva al final, que es lo que rompía el esquema viejo.
  await guardar(tabA, 'contact_phone', '+51 999 333 444');

  await tabB.close();
});
