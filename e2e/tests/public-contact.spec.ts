import type { Page } from '@playwright/test';
import { expect, test } from '../fixtures/base.js';

const configuracionCompleta = {
  business_name: 'Negocio E2E',
  whatsapp_number: '+51 999 888 777',
  contact_email: 'contacto@sillar.test',
  contact_phone: '054 123456',
  business_address: 'Av. Ejemplo 123',
  business_reference: 'Frente a la plaza',
  google_maps_url: 'https://maps.google.com/?q=Av.+Ejemplo+123',
  business_hours: 'Lun–Sáb 09:00–18:00',
};

async function responderPublico(
  page: Page,
  settings: Record<string, string>,
  socialLinks: unknown[],
): Promise<void> {
  await page.route('**/api/settings/public', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.continue();
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(settings),
    });
  });

  await page.route('**/api/cms/social-links', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.continue();
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(socialLinks),
    });
  });
}

test('§18 — CORE publica contacto y su WhatsApp prevalece sobre CMS', async ({
  page,
}) => {
  await responderPublico(page, configuracionCompleta, [
    {
      id: 10,
      platform: 'whatsapp',
      url: 'https://wa.me/519111222333',
    },
    {
      id: 11,
      platform: 'instagram',
      url: 'https://instagram.com/sillar-e2e',
    },
  ]);

  await page.goto('/');

  const footer = page.locator('footer.pf-footer');
  await expect(footer).toBeVisible();

  const whatsapp = footer.getByRole('link', {
    name: /WhatsApp.*\+51 999 888 777/,
  });

  await expect(whatsapp).toBeVisible();
  await expect(whatsapp).toHaveAttribute(
    'href',
    'https://wa.me/51999888777',
  );

  await expect(
    footer.getByRole('link', { name: 'Instagram' }),
  ).toHaveAttribute(
    'href',
    'https://instagram.com/sillar-e2e',
  );

  await expect(
    footer.locator('a[href="https://wa.me/519111222333"]'),
  ).toHaveCount(0);

  await page.goto('/contacto');

  const contenido = page.locator('main#contenido');

  await expect(
    contenido.getByRole('heading', { name: 'Contacto', level: 1 }),
  ).toBeVisible();

  await expect(
    contenido.getByText('Datos de contacto', { exact: true }),
  ).toBeVisible();

  const cta = contenido.getByRole('link', {
    name: /Escribir por WhatsApp.*\+51 999 888 777/,
  });

  await expect(cta).toHaveAttribute(
    'href',
    'https://wa.me/51999888777',
  );

  await expect(
    contenido.getByRole('link', { name: 'contacto@sillar.test' }),
  ).toHaveAttribute(
    'href',
    'mailto:contacto@sillar.test',
  );

  await expect(contenido.getByText('054 123456', { exact: true })).toBeVisible();
  await expect(contenido.getByText('Lun–Sáb 09:00–18:00')).toBeVisible();
  await expect(contenido.getByText('Av. Ejemplo 123')).toBeVisible();
  await expect(contenido.getByText('Frente a la plaza')).toBeVisible();

  await expect(
    contenido.getByRole('link', { name: 'Ver ubicación en Google Maps' }),
  ).toHaveAttribute(
    'href',
    'https://maps.google.com/?q=Av.+Ejemplo+123',
  );
});

test('§18 — valores pendientes o vacíos no se publican', async ({ page }) => {
  await responderPublico(
    page,
    {
      business_name: 'Negocio E2E',
      whatsapp_number: 'PENDIENTE_DEFINIR',
      contact_email: '   ',
      contact_phone: '',
      business_address: 'PENDIENTE_DEFINIR',
      business_reference: '',
      google_maps_url: '   ',
      business_hours: 'PENDIENTE_DEFINIR',
    },
    [],
  );

  await page.goto('/');

  await expect(
    page.getByRole('link', { name: 'Contacto' }),
  ).toBeVisible();

  await expect(page.locator('footer.pf-footer')).toBeHidden();
  await expect(page.locator('body')).not.toContainText('PENDIENTE_DEFINIR');

  await page.goto('/contacto');

  await expect(
    page.getByText('Envíanos un mensaje', { exact: true }),
  ).toBeVisible();

  await expect(
    page.getByText('Datos de contacto', { exact: true }),
  ).toHaveCount(0);

  await expect(page.locator('body')).not.toContainText('PENDIENTE_DEFINIR');
});
