import { http } from '../../../shared/http/client';
import { describe, type Failure } from '../../../shared/errors/messages';
import { isApiError } from '../../../shared/http/errors';
import { isSourceFailure } from './sourceFailure';
import type { OrderIndicatorsView } from './orderIndicators';
import { InconsistentIndicatorsError, verifyOrderIndicators } from './verifyOrderIndicators';

export { orderStates, type OrderIndicatorsView } from './orderIndicators';
export { InconsistentIndicatorsError } from './verifyOrderIndicators';

/**
 * Solo contrato administrativo M10; el frontend nunca consulta M03 directamente.
 * D8: la respuesta se valida aquí, antes de que ninguna pantalla la publique.
 */
export async function loadOrderIndicators(start: string, end: string): Promise<OrderIndicatorsView> {
  const data = await http.get<OrderIndicatorsView>('/admin/reporting/orders', { query: { start, end } });
  return verifyOrderIndicators(data, { start, end });
}

/**
 * Traduce un fallo de consulta de reportes a lo que ve la pantalla.
 *
 * D8: una respuesta incoherente conserva su propia frase; `describe()` la
 * convertiría en el genérico «No se pudo…» y se perdería el motivo. Lo mismo
 * con el fallo de ventas: su frase dice que no hay cifra, no que haya cero.
 * Lo usan la consulta principal y la comparación de cohortes.
 */
export function describeReportFailure(error: unknown, context: string): Failure {
  if (error instanceof InconsistentIndicatorsError) {
    return { kind: 'inline', message: error.message, fieldErrors: null, blockedBy: null };
  }
  if (isApiError(error) && isSourceFailure(error)) {
    return { kind: 'inline', message: error.displayMessage, fieldErrors: null, blockedBy: null };
  }
  return describe(error, context);
}
