import { isApiError } from '../../../shared/http/errors.ts';

/**
 * Ventas está activo pero falló al responder. El endpoint de M10 lo dice con un
 * 503 explicado; un 503 sin cuerpo (un proxy, un reinicio) no es esto, y un 500
 * tampoco: ninguno de los dos dice que la cifra no exista.
 */
export function isSourceFailure(error: unknown): boolean {
  return isApiError(error) && error.status === 503 && error.explainedByServer;
}
