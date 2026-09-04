import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * El registro de auditoría, en lo que la decisión de producto cambió: **nadie
 * tiene que saberse un identificador para usar esta pantalla.**
 *
 * El filtro «Usuario» pedía un número —la ayuda decía literalmente «Su
 * identificador numérico»—, así que para buscar lo que hizo alguien había que
 * averiguar antes su `adminUserId`. Un dato interno convertido en requisito
 * para trabajar.
 *
 * El identificador sigue viajando en la consulta; lo que desaparece es la
 * obligación de conocerlo.
 */

const AUDITORIA = '/admin/auditoria';

test('Para filtrar por persona no hace falta saberse ningún identificador', async ({ page }) => {
  await loginAsE2eAdmin(page);
  const api = page.request;

  const { csrfToken } = (await (await api.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };
  const cabeceras = { 'X-CSRF-Token': csrfToken };

  // **El administrador de baja se crea aquí y se da de baja aquí.** Si la
  // prueba esperase a encontrar uno inactivo, pasaría o fallaría según lo que
  // hubieran dejado otras specs, y el caso que importa —que los inactivos
  // aparecen— es justo el que se perdería el día que nadie desactive a nadie.
  const sello = Date.now();
  const nombre = `Auditora De Baja ${sello}`;
  const correo = `auditora.baja.${sello}@sillar.test`;

  const alta = await api.post('/api/admin/users', {
    headers: cabeceras,
    data: {
      fullName: nombre,
      email: correo,
      // Sin parecido con el nombre ni con el correo, y sin el sello: la
      // política los rechaza (`PasswordPolicy.cs`), y el sello está en el
      // correo.
      password: 'Retama-Quilla-47xW',
      role: 'editor',
      phone: null,
    },
  });
  expect(alta.ok(), `crear el administrador: ${alta.status()} ${await alta.text()}`).toBe(true);
  const inactivo = (await alta.json()) as { id: number };

  const baja = await api.delete(`/api/admin/users/${inactivo.id}`, { headers: cabeceras });
  expect(baja.ok(), `dar de baja: ${baja.status()} ${await baja.text()}`).toBe(true);

  await page.goto(AUDITORIA);
  await expect(page.locator('main')).toBeVisible();

  // --- 1 · No queda ningún sitio donde escribir un identificador ----------
  await expect(
    page.locator('main input[type="number"]'),
    'sigue habiendo un campo numérico en los filtros de auditoría',
  ).toHaveCount(0);
  await expect(
    page.getByText('Su identificador numérico.'),
    'la ayuda sigue pidiendo el identificador de la persona',
  ).toHaveCount(0);

  // --- 2 · Se elige por nombre y correo, y los de baja están --------------
  const selector = page.getByLabel('Usuario');
  await expect(selector, 'no hay desplegable de usuario').toBeVisible();

  const opcion = selector.locator('option', { hasText: correo });

  // El texto completo y exacto: así se afirma a la vez que lleva nombre y
  // correo, que dice «(inactivo)», y que **no lleva nada más** — ningún
  // identificador colado al final de la etiqueta.
  await expect(
    opcion,
    'la opción del administrador de baja no está, o no dice lo que debe',
  ).toHaveText(`${nombre} — ${correo} (inactivo)`);

  // Y sigue habiendo una opción para no filtrar por nadie.
  await expect(selector.locator('option', { hasText: 'Todos' })).toHaveCount(1);

  // --- 3 · Al elegirlo, el identificador viaja por dentro -----------------
  const consulta = page.waitForRequest(
    (peticion) =>
      peticion.url().includes('/api/admin/audit') &&
      new URL(peticion.url()).searchParams.get('adminUserId') === String(inactivo.id),
  );

  await selector.selectOption(String(inactivo.id));

  // Si esto expira, es que la selección no llegó a la consulta: la pantalla
  // estaría filtrando por otra cosa, o por nada.
  await consulta;

  // --- 4 · Y nunca se le enseñó a nadie ----------------------------------
  // Lo que la persona ve del administrador elegido es su nombre y su correo.
  // El número que acaba de viajar en la URL no está en la pantalla.
  await expect(selector).toHaveValue(String(inactivo.id));
});
