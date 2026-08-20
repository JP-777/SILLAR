import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { expect, test } from '../fixtures/base.js';

/**
 * El interruptor de tema — aviso del equipo de diseño: `tokens.css` definía
 * el oscuro en dos niveles (`:root` claro, `[data-theme="dark"]` oscuro) sin
 * el medio `prefers-color-scheme` de por medio, así que una elección
 * explícita a claro no tenía ninguna regla que anulara al sistema cuando
 * este prefería oscuro. Corregido en `shared/styles/tokens.css` (tres
 * niveles: `:root`, el medio guardado con `:not([data-theme="light"])`, y
 * los dos `[data-theme]` explícitos) y en `shared/hooks/useTheme.ts` (ya no
 * fija un valor por defecto: sin elección, no pone atributo, y deja que el
 * medio decida).
 */

const STONE_50 = 'rgb(250, 248, 245)'; // --bg claro, --stone-50
const STONE_900 = 'rgb(26, 23, 19)'; // --bg oscuro, --stone-900

test('Con el sistema en oscuro, elegir tema claro deja la interfaz clara de verdad', async ({ page }) => {
  // El sistema operativo prefiere oscuro durante toda la prueba: lo que
  // cambia es únicamente la elección de la persona, nunca el sistema.
  await page.emulateMedia({ colorScheme: 'dark' });

  await loginAsE2eAdmin(page);
  await page.goto('/admin/modulos');

  // Antes de tocar el interruptor, nadie eligió nada: el medio
  // prefers-color-scheme es lo único que puede estar decidiendo, y decide
  // oscuro porque el sistema está en oscuro. Se comprueba el color de
  // verdad —no el atributo, que a esta altura ni siquiera existe— porque es
  // lo único que prueba que la cascada de tokens.css resolvió de verdad.
  await expect(page.locator('html')).not.toHaveAttribute('data-theme');
  await expect(page.locator('body')).toHaveCSS('background-color', STONE_900);

  await page.getByRole('button', { name: 'Cambiar a tema claro' }).click();

  // La elección explícita a claro tiene que ganarle al sistema, que sigue en
  // oscuro durante toda la prueba: es el caso exacto que exige el guardado
  // :not([data-theme="light"]) del medio en tokens.css. Sin él, el bloque
  // oscuro del sistema pintaría por debajo del explícito a claro.
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect(page.locator('body')).toHaveCSS('background-color', STONE_50);
});
