import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';
import { psql } from '../setup/docker.js';

/**
 * Lo que esta prueba defiende: **la columna «Resumen» de la auditoría no pone
 * delante del lector nada que venga del dato** —ni el destinatario de un correo,
 * ni un identificador—, y el resumen original sigue estando, íntegro, a un clic.
 *
 * `SmtpEmailSender` compone el resumen con el destinatario dentro
 * (`backend/Sillar.Core/Email/SmtpEmailSender.cs:101` y `:128`) y, cuando falla,
 * detrás va `exception.Message` (`:107`): texto libre de un servidor ajeno. Y
 * `OutgoingEmail.Kind` es un `string` sin enumeración ni `CHECK`
 * (`backend/Sillar.Core.Contracts/Email/IEmailSender.cs:8`), así que `entity_id`
 * tampoco es un identificador de conjunto cerrado por contrato.
 *
 * Por eso los casos no son variantes del mismo caso: **son las tres
 * procedencias del texto** —las listas del producto, lo que diga MailKit, y lo
 * que traiga `entity_id`— y los tres defectos que se encontraron en cada una.
 */
const UUID = '3bb862d9-1acd-417b-8247-b94b2cd93113';
const CORREO_CON_UUID = `m08-audit-${UUID}@sillar.test`;

const GENERICO = 'falló — abre el detalle para ver qué dijo el servidor.';

const cases = [
  {
    nombre: 'envío logrado',
    entityId: 'smtp_test',
    original: "Correo 'smtp_test' a 'persona.normal@sillar.test': enviado.",
    visible: 'Correo de prueba: enviado.',
  },
  {
    nombre: 'motivo que redacta SILLAR',
    entityId: 'email_verification',
    original: `Correo 'email_verification' a '${CORREO_CON_UUID}': falló. Falta configurar smtp_server.`,
    visible: 'Verificación de correo: falló — falta configurar el servidor SMTP.',
  },
  {
    // **A15-01.** El error del servidor repite el destinatario, con su UUID.
    nombre: 'error del servidor que repite el destinatario',
    entityId: 'password_reset',
    original: `Correo 'password_reset' a '${CORREO_CON_UUID}': falló. Rechazado: ${CORREO_CON_UUID}`,
    visible: `Restablecer contraseña: ${GENERICO}`,
  },
  {
    // **A15-03.** El error termina en «': enviado.», que es exactamente el
    // final de un envío logrado. Reconocer el éxito por el final de la frase
    // lo presentaba como enviado; reconocer la estructura, no.
    nombre: 'error que termina como un envío logrado',
    entityId: 'invitation',
    original: "Correo 'invitation' a 'ana@sillar.test': falló. El servidor dijo 'no se envió': enviado.",
    visible: `Invitación de cuenta: ${GENERICO}`,
  },
  {
    // **A15-04.** `entity_id` con un UUID dentro y sin arroba: ni la lista de
    // clases lo reconoce ni el guardia de la arroba lo vería. La fila se
    // presenta sin clase.
    nombre: 'clase de correo que no está en la lista',
    entityId: `verificacion-${UUID}`,
    original: `Correo 'verificacion-${UUID}' a 'ana@sillar.test': enviado.`,
    visible: 'Correo: enviado.',
  },
  {
    nombre: 'resumen sin la forma que escribe el envío',
    entityId: 'legacy_import',
    original: 'Se intentó enviar a ana@sillar.test y no se pudo.',
    visible: 'Evento de correo.',
  },
] as const;

const sql = (value: string) => `'${value.replaceAll("'", "''")}'`;

/** Direcciones de correo y uuid: las dos cosas que no pueden verse sin desplegar. */
const CORREO = /[\w.+-]+@[\w.-]+\.\w+/g;
const IDENTIFICADOR = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

test('email_send oculta destinatario e identificador, y conserva el summary original en detalle', async ({ page }) => {
  await loginAsE2eAdmin(page);

  for (const item of cases) {
    await psql(`
      INSERT INTO core.audit_log
        (admin_user_id, admin_user_email, module_code, entity_type, entity_id, action, summary, ip_address)
      VALUES
        (NULL, NULL, 'crm', 'email', ${sql(item.entityId)}, 'email_send', ${sql(item.original)}, '127.0.0.1')
    `);
  }

  await page.goto('/admin/auditoria');
  await page.waitForLoadState('networkidle');
  await expect(page.locator('main .ui-spinner')).toHaveCount(0);

  // **La columna se busca por su cabecera, no por su posición.** Un índice
  // escrito a mano se queda callado el día que alguien añada una columna
  // antes; la cabecera dice cuál es «Resumen» aunque se muevan todas.
  const cabeceras = (await page.locator('table.ui-table thead th').allInnerTexts())
    .map((texto) => texto.trim());
  const columnaResumen = cabeceras.indexOf('Resumen');

  expect(columnaResumen, 'la tabla de auditoría ya no tiene columna «Resumen»').toBeGreaterThanOrEqual(0);

  for (const item of cases) {
    // La fila se busca por su texto original, que es único y está siempre en
    // el DOM aunque esté plegado. Buscarla por el texto visible no serviría:
    // varios casos comparten la frase genérica, que es justo lo que se busca.
    const row = page.locator('tbody tr').filter({ hasText: item.original });
    await expect(row, `fila de ${item.nombre}`).toHaveCount(1);

    const celda = row.locator('td').nth(columnaResumen);

    await expect(
      celda.getByText(item.visible, { exact: true }),
      `línea visible de ${item.nombre}`,
    ).toBeVisible();

    // **`innerText` y no `textContent`, y aquí está la diferencia.** El
    // original está en el DOM de esta misma celda: `textContent` lo traería y
    // la comprobación pasaría siempre. `innerText` es lo que se ve, y el
    // contenido de un `<details>` plegado no entra.
    expect(
      await celda.innerText(),
      `el original se ve sin desplegar en ${item.nombre}`,
    ).not.toContain(item.original);

    const detail = celda.locator('details');
    const original = detail.getByText(item.original, { exact: true });

    await expect(original, `original plegado en ${item.nombre}`).toBeHidden();
    await detail.getByText('Ver texto original', { exact: true }).click();
    await expect(original).toBeVisible();
    await expect(original).toHaveText(item.original);
    await detail.getByText('Ver texto original', { exact: true }).click();
    await expect(original, `vuelve a plegarse en ${item.nombre}`).toBeHidden();
  }

  // **El barrido, y no la suma de los casos.** Lo anterior comprueba cada caso
  // previsto; esto comprueba que no hay ninguna dirección ni ningún
  // identificador a la vista en toda la columna, venga de la fila que venga.
  // Es la comprobación que sobrevive a que mañana alguien añada un quinto
  // motivo sin añadir su caso aquí.
  //
  // **Solo esa columna, y a propósito.** El correo de quien actuó se muestra
  // en «Quién» porque ahí es el dato de la fila, no una filtración: barrer la
  // página entera daría rojo por la columna correcta.
  const resumenes = await page.locator(`tbody tr td:nth-child(${columnaResumen + 1})`).allInnerTexts();
  const aLaVista = resumenes.flatMap((texto) => [
    ...(texto.match(CORREO) ?? []),
    ...(texto.match(IDENTIFICADOR) ?? []),
  ]);

  expect(
    aLaVista,
    'la columna «Resumen» muestra datos que solo deberían verse al desplegar el detalle',
  ).toEqual([]);

  // Y el registro original intacto donde tiene que estar: en el API.
  const response = await page.request.get('/api/admin/audit?page=1&pageSize=100');
  expect(response.ok()).toBe(true);
  const data = (await response.json()) as {
    items: { action: string; entityId: string | null; summary: string | null }[];
  };

  for (const item of cases) {
    expect(
      data.items.some((entry) =>
        entry.action === 'email_send'
        && entry.entityId === item.entityId
        && entry.summary === item.original),
      `el API conserva el resumen original de ${item.nombre}`,
    ).toBe(true);
  }
});
