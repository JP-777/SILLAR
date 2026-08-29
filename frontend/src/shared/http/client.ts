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
 * Estado privado de una población autenticada.
 *
 * Panel y tienda pueden estar abiertos simultáneamente en el mismo navegador:
 * cada uno conserva su CSRF y su reacción a 401 sin pisar al otro.
 */
interface SessionState {
  csrfToken: string | null;
  onUnauthorized: (() => void) | null;
}

const adminSessionState: SessionState = {
  csrfToken: null,
  onUnauthorized: null,
};

const customerSessionState: SessionState = {
  csrfToken: null,
  onUnauthorized: null,
};

const anonymousSessionState: SessionState = {
  csrfToken: null,
  onUnauthorized: null,
};

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

function createHttpClient(state: SessionState) {
  return {
    setCsrfToken(token: string | null): void {
      state.csrfToken = token;
    },

    get csrfToken(): string | null {
      return state.csrfToken;
    },

    onUnauthorized(handler: (() => void) | null): void {
      state.onUnauthorized = handler;
    },

    get<T>(path: string, options: RequestOptions = {}): Promise<T> {
      return request<T>(path, { ...options, method: 'GET' }, state);
    },

    post<T>(path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
      return request<T>(path, { ...options, method: 'POST', body }, state);
    },

    put<T>(path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
      return request<T>(path, { ...options, method: 'PUT', body }, state);
    },

    delete<T>(path: string, options: RequestOptions = {}): Promise<T> {
      return request<T>(path, { ...options, method: 'DELETE' }, state);
    },

    upload<T>(path: string, form: FormData, options: RequestOptions = {}): Promise<T> {
      return request<T>(path, { ...options, method: 'POST', form }, state);
    },
  };
}

export const http = createHttpClient(adminSessionState);
export const customerHttp = createHttpClient(customerSessionState);
export const anonymousHttp = createHttpClient(anonymousSessionState);

async function request<T>(
  path: string,
  options: RequestOptions,
  state: SessionState,
): Promise<T> {
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
  if (!SAFE_METHODS.has(method) && state.csrfToken) {
    headers.set(CSRF_HEADER, state.csrfToken);
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

  throw await toApiError(
    response,
    options.allowUnauthorized ?? false,
    state,
  );
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
async function toApiError(
  response: Response,
  allowUnauthorized: boolean,
  state: SessionState,
): Promise<ApiError> {
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
    state.csrfToken = null;
    state.onUnauthorized?.();
  }

  return new ApiError(
    kind,
    response.status,
    problem.title,
    problem.detail,
    problem.errors,
    problem.blockedBy,
  );
}

interface Problem {
  title: string;
  detail: string | null;
  errors: ValidationErrors | null;
  blockedBy: string[] | null;
}

async function readProblem(response: Response): Promise<Problem> {
  const fallback = `No se pudo completar la operación (${response.status}).`;
  const empty: Problem = { title: fallback, detail: null, errors: null, blockedBy: null };

  try {
    const contentType = response.headers.get('Content-Type') ?? '';

    if (!contentType.includes('json')) {
      return empty;
    }

    const payload = (await response.json()) as {
      title?: string;
      detail?: string;
      errors?: ValidationErrors;
      blockedBy?: unknown;
    };

    return {
      title: payload.title ?? fallback,
      detail: payload.detail ?? null,
      errors: payload.errors ?? null,
      blockedBy: readCodes(payload.blockedBy),
    };
  } catch {
    return empty;
  }
}

/**
 * Lee la lista de códigos si viene y tiene la forma esperada.
 *
 * Se comprueba en vez de confiar: si el servidor cambia la forma, la interfaz
 * cae al mensaje en lugar de romperse a mitad de pintar.
 */
function readCodes(value: unknown): string[] | null {
  if (!Array.isArray(value)) {
    return null;
  }

  const codes = value.filter((item): item is string => typeof item === 'string' && item.length > 0);

  return codes.length > 0 ? codes : null;
}
