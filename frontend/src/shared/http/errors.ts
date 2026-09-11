/**
 * Errores del API, como tipos y no como cadenas.
 *
 * Comparar mensajes de texto para decidir qué hacer es frágil y se rompe en
 * cuanto alguien mejora una redacción. Aquí cada situación tiene su tipo, y el
 * mensaje es solo para mostrar.
 */

/** Clase de error, para decidir qué hacer. */
export type ApiErrorKind =
  /** 401. La sesión murió o nunca existió. */
  | 'Unauthorized'
  /** 403. Falta rol, o falta el token CSRF. */
  | 'Forbidden'
  /** 404. */
  | 'NotFound'
  /** 409. La operación choca con el estado actual. */
  | 'Conflict'
  /** 423. La cuenta está bloqueada temporalmente. */
  | 'Locked'
  /** 413. El archivo pasa del tamaño permitido. */
  | 'PayloadTooLarge'
  /** 415. El contenido del archivo no sirve. */
  | 'UnsupportedMediaType'
  /** 400 con detalle de validación. */
  | 'ValidationFailed'
  /** El servidor no respondió. Puede ser un reinicio en curso. */
  | 'Network'
  /** Cualquier otra cosa, incluido el 500. */
  | 'Unexpected';

/** Errores de validación por campo, tal como los devuelve el backend. */
export type ValidationErrors = Record<string, string[]>;

/** Un fallo al hablar con el API. */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number;
  readonly detail: string | null;
  readonly errors: ValidationErrors | null;

  /**
   * Códigos que provocaron un conflicto, cuando el servidor los envía aparte.
   *
   * Los 409 de activación de módulos los incluyen para que la interfaz pueda
   * escribir los nombres visibles y enlazarlos a su tarjeta. El servidor explica
   * el motivo; enlazar es lo único que solo la interfaz puede hacer.
   */
  readonly blockedBy: string[] | null;

  constructor(
    kind: ApiErrorKind,
    status: number,
    message: string,
    detail: string | null = null,
    errors: ValidationErrors | null = null,
    blockedBy: string[] | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
    this.kind = kind;
    this.status = status;
    this.detail = detail;
    this.errors = errors;
    this.blockedBy = blockedBy;
  }

  /** Mensajes de validación en una sola lista, para mostrarlos juntos. */
  get validationMessages(): string[] {
    return this.errors ? Object.values(this.errors).flat() : [];
  }

  /**
   * La frase del servidor, entera: su `title` y, si lo trae, su `detail`.
   *
   * **Por qué las dos y no solo el título.** Los productores de este backend
   * escriben la frase principal en `title` y el complemento en `detail`: el
   * 423 dice en `detail` a qué hora vuelve a poder intentarse, y el 503 de la
   * instalación explica en `detail` que antes de migrar hay que comprobar a qué
   * base apunta la conexión. Quedarse con el título era enseñar la mitad del
   * mensaje, y a veces la mitad peligrosa: «No existe la tabla» sin «comprueba
   * PRIMERO la conexión» invita a migrar la base equivocada.
   *
   * **`null` cuando lo que trae el servidor no debe llegar a una pantalla**, y
   * son dos casos, los dos medidos:
   *
   *   - **500.** Ningún productor de la aplicación escribe un 500: sale siempre
   *     de una excepción no manejada, así que su texto es del framework —en
   *     inglés, y fuera de producción con el mensaje de la excepción—. Es
   *     texto para el registro, no para una persona.
   *   - **413, el `detail`.** `MediaEndpoints` lo rellena con el `Message` de
   *     la `BadHttpRequestException` de Kestrel: texto del framework, en
   *     inglés. Se enseña el título, que sí está escrito para leerse, y el
   *     `detail` se queda fuera hasta que el productor lo redacte.
   */
  get serverMessage(): string | null {
    if (this.status === 500) {
      return null;
    }

    if (this.kind === 'PayloadTooLarge' || !this.detail) {
      return this.message;
    }

    return `${this.message} ${this.detail}`;
  }

  /**
   * Lo que conviene enseñar a una persona cuando no hay contexto para más.
   *
   * La precedencia, de más a menos concreta:
   *
   *   1. el primer error de validación, que dice qué campo corregir;
   *   2. la frase del servidor, título y detalle (`serverMessage`);
   *   3. una frase propia, cuando el servidor no escribió nada presentable.
   */
  get displayMessage(): string {
    return (
      this.validationMessages[0]
      ?? this.serverMessage
      ?? 'El servidor no pudo completar la operación. Si vuelve a pasar, revisa el registro del servidor.'
    );
  }
}

/** Traduce un código de estado a su clase de error. */
export function kindOf(status: number, hasValidationErrors: boolean): ApiErrorKind {
  switch (status) {
    case 400:
      return hasValidationErrors ? 'ValidationFailed' : 'Unexpected';
    case 401:
      return 'Unauthorized';
    case 403:
      return 'Forbidden';
    case 404:
      return 'NotFound';
    case 409:
      return 'Conflict';
    case 413:
      return 'PayloadTooLarge';
    case 415:
      return 'UnsupportedMediaType';
    case 423:
      return 'Locked';
    default:
      return 'Unexpected';
  }
}

/** Comprueba si un valor es un error del API de la clase indicada. */
export function isApiError(value: unknown, kind?: ApiErrorKind): value is ApiError {
  return value instanceof ApiError && (kind === undefined || value.kind === kind);
}
