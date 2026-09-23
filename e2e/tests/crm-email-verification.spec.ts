import { expect, test } from '../fixtures/base.js';
import { API_URL } from '../setup/env.js';
import { login, waitApiReady } from '../setup/api.js';
import {
  composeUpMailpit,
  psql,
  recreateApiForMailpit,
  recreateApiWithoutSmtp,
} from '../setup/docker.js';
import {
  waitMailpitMessage,
  waitMailpitReady,
} from '../setup/mailpit.js';

const PASSWORD = 'cobre-lima-dieciocho-lunas';

interface Session {
  cookie: string;
  csrfToken: string;
}

interface Setting {
  key: string;
  value: string;
  isPublic: boolean;
}

async function writeSetting(
  session: Session,
  key: string,
  value: string,
  isPublic = false,
): Promise<void> {
  const response = await fetch(
    `${API_URL}/api/admin/settings/${encodeURIComponent(key)}`,
    {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Cookie: session.cookie,
        'X-CSRF-Token': session.csrfToken,
      },
      body: JSON.stringify({
        value,
        isPublic,
      }),
    },
  );

  if (!response.ok) {
    throw new Error(
      `Configurar ${key} devolvió ${response.status}: ${await response.text()}`,
    );
  }
}

async function readMailSettings(session: Session): Promise<Setting[]> {
  const response = await fetch(`${API_URL}/api/admin/settings`, {
    headers: {
      Cookie: session.cookie,
      'X-CSRF-Token': session.csrfToken,
    },
  });

  if (!response.ok) {
    throw new Error(
      `Leer settings devolvió ${response.status}: ${await response.text()}`,
    );
  }

  const settings = (await response.json()) as Setting[];

  return settings.filter((setting) =>
    ['smtp_server', 'smtp_port', 'smtp_from'].includes(setting.key),
  );
}

function sqlLiteral(value: string): string {
  return `'${value.replaceAll("'", "''")}'`;
}

async function restoreMailSettings(previous: Setting[]): Promise<void> {
  for (const setting of previous) {
    await psql(
      `UPDATE core.site_settings
       SET setting_value = ${sqlLiteral(setting.value)},
           is_public = ${setting.isPublic ? 'true' : 'false'}
       WHERE setting_key = ${sqlLiteral(setting.key)};`,
    );
  }
}

test(
  'M04 criterio 1 — el registro recibe y consume su correo de verificación',
  async ({ page }) => {
    const originalSession = await login();
    const previous = await readMailSettings(originalSession);

    expect(previous).toHaveLength(3);

    await composeUpMailpit();
    await waitMailpitReady();

    try {
      await recreateApiForMailpit();
      await waitApiReady();

      const session = await login();

      await writeSetting(session, 'smtp_server', 'mailpit');
      await writeSetting(session, 'smtp_port', '1025');
      await writeSetting(session, 'smtp_from', 'no-reply@sillar.test');

      const email =
        `criterio1-${Date.now()}-${Math.random()
          .toString(16)
          .slice(2)}@sillar.test`;

      await page.goto('/crear-cuenta');
      await page.getByLabel('Nombre completo').fill('Cliente Criterio Uno');
      await page.getByLabel('Correo').fill(email);
      await page.getByLabel('Teléfono').fill('999888777');
      await page.getByLabel('Contraseña').fill(PASSWORD);
      await page.getByRole('button', { name: 'Crear cuenta' }).click();

      await expect(
        page.getByText('Solicitud de registro procesada.', { exact: false }),
      ).toBeVisible();

      // Si el SMTP real no entregó el mensaje, esta espera expira y la prueba
      // falla. No se obtiene el token desde la base ni desde código interno.
      const message = await waitMailpitMessage(
        email,
        'Verifica tu correo',
      );

      expect(
        message.Text,
        'Mailpit devolvió el mensaje sin cuerpo de texto',
      ).toBeTruthy();

      const link = message.Text!.match(
        /https?:\/\/[^\s]+\/verificar-correo\?token=[^\s]+/,
      )?.[0];

      expect(
        link,
        'El correo recibido no contiene el enlace de verificación',
      ).toBeTruthy();

      // Se consume exactamente el enlace extraído del correo recibido.
      await page.goto(link!);
      await page.getByRole('button', { name: 'Verificar correo' }).click();

      await expect(
        page.getByText('Tu correo quedó verificado.', { exact: true }),
      ).toBeVisible();

      await page.goto('/entrar');
      await page.getByLabel('Correo').fill(email);
      await page.getByLabel('Contraseña').fill(PASSWORD);
      await page.getByRole('button', { name: 'Entrar' }).click();

      await expect(page).toHaveURL('/mi-cuenta');

      const customer = await page.evaluate(async () => {
        const response = await fetch('/api/customer/auth/me');

        return {
          status: response.status,
          body: await response.json(),
        };
      });

      expect(customer.status).toBe(200);
      expect(customer.body).toMatchObject({
        email,
        emailVerified: true,
      });
    } finally {
      // La suite comparte stack: se restituyen tanto los settings como la
      // credencial SMTP efímera para no cambiar pruebas posteriores.
      await restoreMailSettings(previous);
      await recreateApiWithoutSmtp();
      await waitApiReady();
    }
  },
);
