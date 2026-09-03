import { loginAsE2eAdmin } from '../fixtures/auth.js';
import { duringExpectedOutage, expect, test } from '../fixtures/base.js';
import { themeRecorder } from '../fixtures/themes.js';

/**
 * Medios — `VERIFICACION-VISUAL-CORE.md` §7.
 *
 * Los archivos de apoyo **no existen en el repositorio ni en el disco**: se
 * pasan como buffers en memoria a `setInputFiles`. Un binario de 5 MB
 * commiteado por una sola aserción entra al historial de git para siempre, y
 * generarlo en disco obligaría a limpiarlo después. Así no hay ni una cosa ni
 * la otra.
 */

/** PNG válido de 1×1. Pasa la comprobación de contenido del servidor. */
const PNG_1X1 = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

/** Pasa de 5 MB. Lo rechaza `precheck` por tamaño, sin llegar a la red. */
const DEMASIADO_GRANDE = Buffer.concat([PNG_1X1, Buffer.alloc(5 * 1024 * 1024, 0)]);

/** Dice `.png` y es texto. Solo el servidor puede cazarlo: mira los bytes. */
const PNG_FALSO = Buffer.from('esto no es un png, es texto plano disfrazado', 'utf8');

const SVG = Buffer.from(
  '<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"></svg>',
  'utf8',
);

const ENTRADA = '.gal-drop input[type="file"]';

/** Sube un archivo y devuelve el texto del aviso de error, si lo hubo. */
async function subirYLeerError(
  page: import('@playwright/test').Page,
  name: string,
  mimeType: string,
  buffer: Buffer,
): Promise<string> {
  await page.setInputFiles(ENTRADA, { name, mimeType, buffer });

  const aviso = page.getByRole('alert').first();
  await expect(aviso).toBeVisible();
  return (await aviso.innerText()).trim();
}

test('Los tres rechazos de subida dicen cosas distintas entre sí', async ({ page }) => {
  const record = themeRecorder(page, 'medios');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/archivos');
  await expect(page.locator('.gal-drop')).toBeVisible();

  // 1 · Pesa demasiado. Lo caza `precheck` por tamaño (media.ts:89).
  const porTamano = await subirYLeerError(page, 'enorme.png', 'image/png', DEMASIADO_GRANDE);
  expect(porTamano).toContain('5 MB');

  await page.reload();

  // 2 · Es un SVG. Se nombra el formato, no una lista de los admitidos
  // (media.ts:99): quien sube un logo vectorial merece saber que es *ese*
  // formato el que no entra.
  const porFormato = await subirYLeerError(page, 'logo.svg', 'image/svg+xml', SVG);
  expect(porFormato.toLowerCase()).toContain('svg');

  await page.reload();

  // 3 · Dice `.png` y no lo es. Este sí sale a la red: solo el servidor puede
  // saberlo, porque mira el contenido y no la extensión (ADR-011).
  let porContenido = '';
  await duringExpectedOutage(page, async () => {
    // El 415 es la respuesta correcta y esperada; el navegador lo anuncia en
    // consola igualmente.
    porContenido = await subirYLeerError(page, 'trampa.png', 'image/png', PNG_FALSO);
  });
  expect(porContenido.length).toBeGreaterThan(0);

  await record('tres-rechazos-de-subida');

  // La afirmación que de verdad pide la guía: no que aparezcan tres mensajes,
  // sino que sean **distintos entre sí**. Tres aserciones sueltas pasarían
  // con el mismo texto genérico en los tres casos, que es justo lo que la
  // guía prohíbe — «si dicen lo mismo, no sirven».
  const mensajes = [porTamano, porFormato, porContenido];
  expect(new Set(mensajes).size, `los tres mensajes no son distintos: ${JSON.stringify(mensajes)}`)
    .toBe(3);
});

test('Subir dos veces el mismo archivo avisa, no falla', async ({ page }) => {
  const record = themeRecorder(page, 'medios');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/archivos');
  await expect(page.locator('.gal-drop')).toBeVisible();

  await page.setInputFiles(ENTRADA, { name: 'repetida.png', mimeType: 'image/png', buffer: PNG_1X1 });
  await expect(page.getByRole('status').filter({ hasText: 'Se subió' })).toBeVisible();

  await page.setInputFiles(ENTRADA, { name: 'repetida.png', mimeType: 'image/png', buffer: PNG_1X1 });

  // Aviso, no error: el archivo se subió. Presentarlo como fallo confundiría
  // a quien acaba de hacer algo correcto — repetir una imagen es normal.
  const aviso = page.getByRole('status').filter({ hasText: 'Ya tenías este archivo' });
  await expect(aviso).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);

  await record('subida-repetida-avisa-sin-fallar');
});

test('Antes de dar de baja un archivo se avisa sin contar cuántos lo usan', async ({ page }) => {
  const record = themeRecorder(page, 'medios');
  await loginAsE2eAdmin(page);
  await page.goto('/admin/archivos');

  // Hace falta al menos uno. Si otra prueba ya lo subió, el duplicado sirve
  // igual: lo que importa es que exista una tarjeta con su botón.
  await page.setInputFiles(ENTRADA, { name: 'para-borrar.png', mimeType: 'image/png', buffer: PNG_1X1 });
  await expect(page.locator('.gal__item').first()).toBeVisible();

  await page.locator('.gal__item').first().getByRole('button', { name: 'Dar de baja' }).click();

  const dialogo = page.getByRole('alertdialog');
  await expect(dialogo).toBeVisible();

  // La frase exacta, y sin recuento: contar referencias entre módulos no
  // tiene segundo caso real (SPEC de M01 §6.8).
  await expect(dialogo).toContainText('Si esta imagen está en uso, desaparecerá de donde esté.');
  await expect(dialogo).not.toContainText(/\d+\s+(módulo|referencia|uso)/);

  // Y el botón nombra la acción, nunca «Aceptar».
  await expect(dialogo.getByRole('button', { name: 'Dar de baja' })).toBeVisible();
  await expect(dialogo.getByRole('button', { name: /^\s*aceptar\s*$/i })).toHaveCount(0);

  await record('aviso-antes-de-dar-de-baja');

  await dialogo.getByRole('button', { name: 'Cancelar' }).click();
  await expect(dialogo).toBeHidden();
});

/**
 * **El resumen de auditoría de una subida no lleva el identificador dentro.**
 *
 * `MediaService.cs` escribía «Archivo subido para el módulo 'catalog':
 * 019fff83-….png (image/png)», y ese nombre no es el que la persona subió: es
 * el identificador generado más la extensión, porque la clave de un medio *es*
 * el nombre del archivo (ADR-018, `MediaStorage.cs:56`). La columna «Resumen»
 * de Auditoría acababa enseñando el `uuid` entero a quien no lo había pedido
 * —la misma fuga que la columna «Entidad», por otra puerta— y ninguna prueba
 * lo veía, porque la de `transversal.spec.ts` estaba marcada `fail` por lo
 * otro.
 *
 * Las cuatro afirmaciones van juntas a propósito: quitar el identificador del
 * resumen no puede costar ni la entrada, ni la trazabilidad, ni lo que el
 * resumen sí tiene que decir.
 */
test('El resumen de una subida no contiene el identificador, y la entrada sigue completa', async ({
  page,
}) => {
  await loginAsE2eAdmin(page);
  const api = page.request;

  const { csrfToken } = (await (await api.get('/api/admin/auth/csrf')).json()) as {
    csrfToken: string;
  };

  const subida = await api.post('/api/admin/media', {
    headers: { 'X-CSRF-Token': csrfToken },
    multipart: {
      ownerModuleCode: 'catalog',
      file: { name: `rastro-${Date.now()}.png`, mimeType: 'image/png', buffer: PNG_1X1 },
    },
  });
  expect(subida.ok(), `subir: ${subida.status()} ${await subida.text()}`).toBe(true);
  const { mediaAssetId } = (await subida.json()) as { mediaAssetId: string };
  expect(mediaAssetId, 'la subida no devolvió un uuid').toMatch(
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
  );

  // Se busca **por su identificador**, no «la última entrada»: la suite corre
  // en paralelo con lo que dejaron otras pruebas, y quedarse con la primera
  // fila haría que esto pasara o fallara según quién subiera antes.
  const auditoria = (await (
    await api.get(`/api/admin/audit?entityType=media_asset&entityId=${mediaAssetId}`)
  ).json()) as { items: { entityId: string | null; summary: string | null; moduleCode: string | null }[] };

  // 1 · La entrada sigue registrándose.
  expect(auditoria.items, 'subir un medio dejó de dejar entrada de auditoría').toHaveLength(1);
  const entrada = auditoria.items[0];

  // 2 · Y `entityId` sigue siendo el identificador completo, sin acortar.
  expect(entrada.entityId, 'la auditoría perdió el identificador de la entidad').toBe(mediaAssetId);

  // 3 · El resumen no lo lleva dentro — ni el `uuid` ni el nombre almacenado.
  expect(
    entrada.summary ?? '',
    'el resumen de auditoría vuelve a contener el identificador del archivo',
  ).not.toContain(mediaAssetId);
  expect(
    entrada.summary ?? '',
    'el resumen de auditoría contiene el nombre almacenado, que es el identificador con extensión',
  ).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);

  // 4 · Pero sigue diciendo lo que sí se lee: para qué módulo y de qué tipo.
  expect(entrada.summary ?? '', 'el resumen dejó de decir para qué módulo era').toContain('catalog');
  expect(entrada.summary ?? '', 'el resumen dejó de decir de qué tipo era').toContain('image/png');
});
