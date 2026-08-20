import type { Page } from '@playwright/test';
import { E2E_ADMIN, E2E_ADMIN_MENOR } from '../setup/api.js';

interface Credenciales {
  email: string;
  password: string;
}

/**
 * Abre sesión por API, sin pasar por el formulario: la mayoría de las
 * pruebas no tratan de la pantalla de login, y visitarla de más metería en
 * la cuenta de cada una un 401 de "todavía no hay sesión" en
 * `/api/admin/auth/me` — real, pero ajeno a lo que cada spec comprueba. La
 * cookie que deja el POST viaja igual al primer `page.goto` de la prueba:
 * `page.request` comparte el mismo contexto de navegador que `page`.
 *
 * Reintenta unas cuantas veces, y esto no es paranoia: el proxy de Vite
 * (`frontend/vite.config.ts`) devuelve **500 con cuerpo vacío** cuando su
 * primera petición reenviada a la API se corta —"socket hang up"—, cosa que
 * pasa si el contenedor acaba de reiniciarse. Sin reintento, una prueba
 * perfectamente sana falla por el arranque del proxy.
 */
async function entrar(page: Page, quien: Credenciales, rol: string): Promise<void> {
  let ultimoError = '';

  for (let intento = 1; intento <= 8; intento += 1) {
    const respuesta = await page.request
      .post('/api/admin/auth/login', { data: { email: quien.email, password: quien.password } })
      .catch((error: Error) => error);

    if (respuesta instanceof Error) {
      ultimoError = respuesta.message;
    } else if (respuesta.ok()) {
      return;
    } else {
      ultimoError = `${respuesta.status()} ${await respuesta.text()}`;

      // 401 son credenciales malas: reintentar no las va a arreglar.
      if (respuesta.status() === 401) {
        break;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, 500));
  }

  throw new Error(`No se pudo entrar como ${rol}: ${ultimoError}`);
}

/** Entra como el `super_admin` que crea la instalación. */
export function loginAsE2eAdmin(page: Page): Promise<void> {
  return entrar(page, E2E_ADMIN, 'super_admin');
}

/**
 * Entra como el segundo usuario, de rol `admin`.
 *
 * Hay reglas que solo se ven desde un rol que no lo puede todo: con el
 * `super_admin` no se puede observar un control deshabilitado por falta de
 * permiso, porque nunca lo está.
 */
export function loginComoAdminMenor(page: Page): Promise<void> {
  return entrar(page, E2E_ADMIN_MENOR, 'admin');
}
