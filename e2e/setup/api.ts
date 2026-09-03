import { serviceRuntimeIdentity, waitServiceRestarted } from './docker.js';
import { API_URL } from './env.js';
import { sleep } from './shell.js';

/** Credenciales del super_admin que crea el arnés. Solo existen en la base efímera. */
export const E2E_ADMIN = {
  fullName: 'Persona Verificadora',
  email: 'verificacion@sillar.test',
  // Sin relación con el nombre ni el correo: PasswordPolicy.Check los
  // rechaza si aparecen dentro (Sillar.Core/Authentication/PasswordPolicy.cs).
  password: 'sandia-morada-catorce-uvas',
};

/**
 * Espera a que el proceso responda algo — 200, 404, lo que sea — en vez de
 * rechazar la conexión. Sirve igual antes y después de cada reinicio: activar
 * un módulo detiene el host a propósito (SPEC de CORE §7) y Docker lo
 * relanza solo, pero tarda unos segundos.
 */
export async function waitApiReady(timeoutMs = 60_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    try {
      await fetch(`${API_URL}/api/setup/status`);
      return;
    } catch {
      await sleep(1000);
    }
  }

  throw new Error('La API e2e no respondió a tiempo.');
}

/** Completa la instalación con el super_admin de {@link E2E_ADMIN}. Detiene el host al terminar. */
export async function completeSetup(): Promise<void> {
  const response = await fetch(`${API_URL}/api/setup`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      businessName: 'Arnés e2e',
      licenseType: 'trial',
      admin: E2E_ADMIN,
    }),
  });

  if (!response.ok) {
    throw new Error(`POST /api/setup devolvió ${response.status}: ${await response.text()}`);
  }
}

/**
 * Segundo usuario, de rol `admin` y no `super_admin`.
 *
 * Existe por una sola razón: hay reglas que solo se ven desde un rol que NO
 * lo puede todo — el interruptor de sitio público aparece deshabilitado con
 * su razón escrita para `admin` (`SettingRow.tsx:82` y `:91`), y con el
 * `super_admin` que crea la instalación eso no se puede observar.
 */
export const E2E_ADMIN_MENOR = {
  fullName: 'Encargada De Turno',
  email: 'turno@sillar.test',
  password: 'ventana-lenta-quince-nubes',
};

interface Session {
  cookie: string;
  csrfToken: string;
}

/**
 * Abre sesión y devuelve la cookie y el token CSRF que necesitan las
 * escrituras. Reintenta sobre un 404, y solo un 404: es el síntoma exacto de
 * pedirle login al proceso viejo, que arrancó en modo instalación y por eso
 * nunca montó `AuthEndpoints` — `/api/setup/status` de ese mismo proceso ya
 * puede estar contestando que la instalación está completa (relee la base en
 * cada petición), sin que eso signifique que el proceso nuevo esté arriba
 * todavía. Cualquier otro código (401, 500...) es un fallo real y no se
 * reintenta.
 */
export async function login(timeoutMs = 60_000): Promise<Session> {
  const deadline = Date.now() + timeoutMs;
  let lastStatus = 0;
  let lastBody = '';

  while (Date.now() < deadline) {
    const response = await fetch(`${API_URL}/api/admin/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: E2E_ADMIN.email, password: E2E_ADMIN.password }),
    }).catch(() => null);

    if (response && response.status !== 404) {
      if (!response.ok) {
        throw new Error(`POST /api/admin/auth/login devolvió ${response.status}: ${await response.text()}`);
      }

      const setCookie = response.headers.get('set-cookie');
      if (!setCookie) {
        throw new Error('El login no devolvió cookie de sesión.');
      }

      const body = (await response.json()) as { csrfToken: string };
      return { cookie: setCookie.split(';')[0], csrfToken: body.csrfToken };
    }

    if (response) {
      lastStatus = response.status;
      lastBody = await response.text();
    }

    await sleep(1000);
  }

  throw new Error(`El login no llegó a funcionar a tiempo (último intento: ${lastStatus} ${lastBody}).`);
}

/** Da de alta a {@link E2E_ADMIN_MENOR}. Idempotente: un 409 significa que ya estaba. */
export async function createLesserAdmin(session: Session): Promise<void> {
  const response = await fetch(`${API_URL}/api/admin/users`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Cookie: session.cookie,
      'X-CSRF-Token': session.csrfToken,
    },
    body: JSON.stringify({
      fullName: E2E_ADMIN_MENOR.fullName,
      email: E2E_ADMIN_MENOR.email,
      password: E2E_ADMIN_MENOR.password,
      role: 'admin',
      phone: null,
    }),
  });

  if (!response.ok && response.status !== 409) {
    throw new Error(`Crear el admin menor devolvió ${response.status}: ${await response.text()}`);
  }
}

/**
 * Activa un módulo y espera a que el host, que se detiene tras responder
 * (`Modules:RestartAfterActivation=true` en `.env.e2e`), vuelva a estar listo
 * antes de devolver el control. La sesión sobrevive: vive en
 * `core.admin_sessions`, no en memoria del proceso.
 *
 * ---
 *
 * **Esa promesa era mentira hasta el 3 de septiembre de 2026, y se notó con el
 * tercer módulo.** Aquí solo se llamaba a `waitApiReady()`, que da por buena la
 * API en cuanto un `fetch` no lanza — y mientras el proceso viejo siga
 * aceptando conexiones, eso contesta que sí **antes de que el reinicio
 * empiece**. Es lo mismo que ya advertía `global-setup.ts` sobre
 * `/api/setup/status`: relee la base en cada llamada, así que el proceso viejo
 * puede responder por el nuevo.
 *
 * Con dos módulos no se veía: el reinicio cabía en los cuatro segundos que
 * `fixtures/auth.ts` reintenta. Con tres dejó de caber, y la primera prueba de
 * la suite empezó a fallar por el arranque del arnés.
 *
 * **Ahora se le pregunta a Docker qué ejecución está corriendo**, antes y
 * después: si la identidad cambió, el reinicio ocurrió de verdad. No es una
 * espera más larga ni un sondeo más insistente — es la diferencia entre
 * estimar y saber.
 *
 * Y va aquí, no en quien llama: el día que M03 añada otra activación, recibe
 * la garantía sin tener que acordarse de pedirla.
 */
export async function activateModule(session: Session, code: string): Promise<void> {
  const antes = await serviceRuntimeIdentity('api');

  const response = await fetch(`${API_URL}/api/admin/modules/${code}/activate`, {
    method: 'POST',
    headers: { Cookie: session.cookie, 'X-CSRF-Token': session.csrfToken },
  });

  if (!response.ok) {
    throw new Error(`Activar '${code}' devolvió ${response.status}: ${await response.text()}`);
  }

  await waitServiceRestarted('api', antes);
  await waitApiReady();
}
