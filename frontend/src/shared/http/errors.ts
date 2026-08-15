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

  /** Lo que conviene enseñar a una persona. */
  get displayMessage(): string {
    const fromValidation = this.validationMessages[0];
    return fromValidation ?? this.message;
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
