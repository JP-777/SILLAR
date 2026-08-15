import { ApiError, isApiError } from '../http/errors';

/**
 * De error tipado a frase.
 *
 * **Ninguna rama de este archivo produce «Ha ocurrido un error».** Es la regla
 * del §3 de la entrega 4a, y la razón es sencilla: un mensaje así no dice qué
 * pasó, no dice qué hacer, y convierte cualquier fallo en el mismo callejón.
 *
 * Cuando el servidor ya redactó la frase —los 409 de módulos y usuarios vienen
 * escritos para leerse— se usa la suya. El frontend no la reescribe: duplicar la
 * redacción es duplicar el sitio donde corregirla.
 */

/** Qué debe hacer la interfaz con un fallo. */
export type FailureKind =
  /** Mostrar la frase junto a la operación que falló. */
  | 'inline'
  /** Situar los mensajes en sus campos. */
  | 'validation'
  /** Pantalla de acceso denegado. */
  | 'forbidden'
  /** No mostrar nada: lo absorbe la reconexión global. */
  | 'silent';

export interface Failure {
  readonly kind: FailureKind;
  readonly message: string;
  /** Mensajes por campo, cuando el servidor dice cuál. */
  readonly fieldErrors: Record<string, string> | null;
}

/**
 * Traduce cualquier cosa que se pueda lanzar en algo que se puede mostrar.
 *
 * @param error Lo capturado.
 * @param context Qué se estaba intentando, para las frases genéricas. En
 *   minúscula y en infinitivo: «guardar el usuario», «activar el módulo».
 */
export function describe(error: unknown, context: string): Failure {
  if (!isApiError(error)) {
    return {
      kind: 'inline',
      message: `No se pudo ${context}. Vuelve a intentarlo.`,
      fieldErrors: null,
    };
  }

  switch (error.kind) {
    case 'Network':
      // La superposición de reconexión ya está en pantalla explicándolo. Añadir
      // aquí otro mensaje sería decir dos veces lo mismo.
      return { kind: 'silent', message: '', fieldErrors: null };

    case 'Unauthorized':
      // El cliente HTTP ya redirigió al login.
      return { kind: 'silent', message: '', fieldErrors: null };

    case 'Forbidden':
      return {
        kind: 'forbidden',
        message: error.detail
          ? `${error.message} ${error.detail}`
          : error.message,
        fieldErrors: null,
      };

    case 'ValidationFailed':
      return {
        kind: 'validation',
        message: error.displayMessage,
        fieldErrors: toFieldErrors(error),
      };

    case 'Conflict':
      // El backend redacta estos para leerse: «'demo_catalog' no puede
      // desactivarse porque estos módulos activos dependen de él…». Reescribirlo
      // aquí sería tener la misma frase en dos sitios.
      return { kind: 'inline', message: error.message, fieldErrors: null };

    case 'Locked':
      return {
        kind: 'inline',
        message: error.detail ? `${error.message} ${error.detail}` : error.message,
        fieldErrors: null,
      };

    case 'NotFound':
      return {
        kind: 'inline',
        message: `Eso ya no existe. Puede que alguien lo haya cambiado mientras tanto.`,
        fieldErrors: null,
      };

    case 'PayloadTooLarge':
      return { kind: 'inline', message: error.message, fieldErrors: null };

    case 'UnsupportedMediaType':
      return { kind: 'inline', message: error.message, fieldErrors: null };

    default:
      // Ni siquiera aquí: se dice qué se intentaba y qué hacer.
      return {
        kind: 'inline',
        message: `No se pudo ${context}. Si vuelve a pasar, revisa el registro del servidor.`,
        fieldErrors: null,
      };
  }
}

/** Toma el primer mensaje de cada campo, que es lo que cabe junto al control. */
function toFieldErrors(error: ApiError): Record<string, string> | null {
  if (!error.errors) {
    return null;
  }

  const result: Record<string, string> = {};

  for (const [field, messages] of Object.entries(error.errors)) {
    if (messages.length > 0) {
      result[field] = messages[0];
    }
  }

  return Object.keys(result).length > 0 ? result : null;
}
