import { connection } from './connection';
import { ApiError, kindOf, type ValidationErrors } from './errors';

/**
 * El único punto por el que sale una petición.
 *
 * Ningún componente llama a `fetch`. Aquí viven las tres cosas que no pueden
 * quedar al criterio de cada pantalla: la cookie de sesión, el token CSRF y el
 * respeto al estado de la conexión.
 */

/** Cabecera donde viaja el token CSRF. */
const CSRF_HEADER = 'X-CSRF-Token';

/** Métodos que solo leen y por tanto no necesitan token CSRF. */
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/**
 * Token CSRF de la sesión, en memoria.
 *
 * Ni `localStorage` ni `sessionStorage`: el token de sesión es una cookie
 * `httpOnly` que JavaScript no debe tocar, y guardar el CSRF en el
 * almacenamiento del navegador desharía parte de esa protección.
 *
 * Desde la entrega 2.1 el valor es estable durante toda la sesión y sobrevive a
 * un reinicio del host, porque se deriva de la identidad de la sesión (ADR-012).
 */
let csrfToken: string | null = null;

/** Qué hacer cuando el servidor dice que la sesión murió. */
let onUnauthorized: (() => void) | null = null;

export interface RequestOptions {
  method?: string;
  /** Cuerpo JSON. Se serializa aquí; no hay que pasar una cadena. */
  body?: unknown;
  /** Cuerpo de formulario, para subir archivos. */
  form?: FormData;
  /** Parámetros de consulta. Los `undefined` y `null` se omiten. */
  query?: Record<string, string | number | boolean | undefined | null>;
  signal?: AbortSignal;
  /**
   * Deja pasar un 401 sin limpiar la sesión ni redirigir.
   *
   * Solo para el arranque: preguntar «¿hay sesión?» y que la respuesta sea «no»
   * es el caso corriente, no un error.
   */
  allowUnauthorized?: boolean;
}

export const http = {
  /** Guarda el token CSRF de la sesión. */
  setCsrfToken(token: string | null): void {
    csrfToken = token;
  },

  get csrfToken(): string | null {
    return csrfToken;
  },

  /** Registra qué hacer ante un 401 inesperado. */
  onUnauthorized(handler: () => void): void {
    onUnauthorized = handler;
  },

  get<T>(path: string, options: RequestOptions = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'GET' });
  },

  post<T>(path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'POST', body });
  },

  put<T>(path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'PUT', body });
  },

  delete<T>(path: string, options: RequestOptions = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'DELETE' });
  },

  /** Sube un archivo. El token CSRF también hace falta aquí. */
  upload<T>(path: string, form: FormData, options: RequestOptions = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'POST', form });
  },
};

async function request<T>(path: string, options: RequestOptions): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase();

  // Durante un reinicio no sale nada. Fallarían todas y llenarían la consola de
  // ruido justo cuando alguien intenta entender qué pasa.
  if (connection.isDown) {
    throw new ApiError(
      'Network',
      0,
      'El servidor se está reiniciando. La operación no se envió.',
    );
  }

  const headers = new Headers({ Accept: 'application/json' });
  let body: BodyInit | undefined;

  if (options.form) {
    // Sin Content-Type: lo pone el navegador con la frontera del multipart, que
    // nosotros no conocemos.
    body = options.form;
  } else if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json');
    body = JSON.stringify(options.body);
  }

  // Subir un archivo es una escritura como cualquier otra: multipart no exime.
  if (!SAFE_METHODS.has(method) && csrfToken) {
    headers.set(CSRF_HEADER, csrfToken);
  }

  let response: Response;

  try {
    response = await fetch(buildUrl(path, options.query), {
      method,
      headers,
      body,
      // Mismo origen gracias al proxy de Vite en desarrollo y al mismo dominio
      // en producción. La cookie viaja sin ceremonia.
      credentials: 'same-origin',
      signal: options.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }

    // El servidor no respondió. Puede ser un reinicio que nadie anunció; el
    // sondeo lo distinguirá de una caída de verdad.
    connection.reportNetworkFailure();

    throw new ApiError('Network', 0, 'No se pudo contactar con el servidor.');
  }

  if (response.ok) {
    return (await readBody(response)) as T;
  }

  throw await toApiError(response, options.allowUnauthorized ?? false);
}

function buildUrl(path: string, query: RequestOptions['query']): string {
  const url = path.startsWith('/api') || path.startsWith('/media') ? path : `/api${path}`;

  if (!query) {
    return url;
  }

  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value));
    }
  }

  const serialized = params.toString();
  return serialized ? `${url}?${serialized}` : url;
}

async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined;
  }

  const contentType = response.headers.get('Content-Type') ?? '';

  return contentType.includes('json') ? await response.json() : await response.text();
}

/** Convierte una respuesta de error en un error tipado. */
async function toApiError(response: Response, allowUnauthorized: boolean): Promise<ApiError> {
  const problem = await readProblem(response);
  const kind = kindOf(response.status, problem.errors !== null);

  // La sesión murió. Se limpia y se vuelve al login, sin reintentar: reintentar
  // con una sesión muerta solo produce un bucle.
  //
  // Nótese que NO hay reintento ante 403. Con el token CSRF derivado de la
  // sesión (ADR-012) el valor es estable, así que un 403 ya no significa «el
  // token caducó»: significa que algo va mal de verdad, y esconderlo con un
  // reintento solo retrasaría el diagnóstico.
  if (kind === 'Unauthorized' && !allowUnauthorized) {
    csrfToken = null;
    onUnauthorized?.();
  }

  return new ApiError(kind, response.status, problem.title, problem.detail, problem.errors);
}

interface Problem {
  title: string;
  detail: string | null;
  errors: ValidationErrors | null;
}

async function readProblem(response: Response): Promise<Problem> {
  const fallback = `Error ${response.status}.`;

  try {
    const contentType = response.headers.get('Content-Type') ?? '';

    if (!contentType.includes('json')) {
      return { title: fallback, detail: null, errors: null };
    }

    const payload = (await response.json()) as {
      title?: string;
      detail?: string;
      errors?: ValidationErrors;
    };

    return {
      title: payload.title ?? fallback,
      detail: payload.detail ?? null,
      errors: payload.errors ?? null,
    };
  } catch {
    return { title: fallback, detail: null, errors: null };
  }
}
