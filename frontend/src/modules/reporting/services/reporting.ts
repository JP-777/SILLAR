import { http } from '../../../shared/http/client';
import type { OrderIndicatorsView } from './orderIndicators';
import { verifyOrderIndicators } from './verifyOrderIndicators';

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
