import { http } from '../../../shared/http/client';
import { describe, type Failure } from '../../../shared/errors/messages';
import { isApiError } from '../../../shared/http/errors';
import { isSourceFailure } from './sourceFailure';
import { exportAuditPortOver, readInstallationZone } from './coreSeam';
import type { OrderIndicatorsView } from './orderIndicators';
import { InconsistentIndicatorsError, verifyOrderIndicators } from './verifyOrderIndicators';
import {
  verifyOrderValue,
  verifyPaymentIndicators,
  type OrderValueView,
  type PaymentIndicatorsView,
} from './monetaryIndicators';

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

/** I02: valor de pedidos por moneda, verificado antes de publicarse. */
export async function loadOrderValue(start: string, end: string): Promise<OrderValueView> {
  const data = await http.get<OrderValueView>('/admin/reporting/orders/value', { query: { start, end } });
  return verifyOrderValue(data, { start, end });
}

/** I05–I07: cobros registrados, verificados antes de publicarse. */
export async function loadPaymentIndicators(start: string, end: string): Promise<PaymentIndicatorsView> {
  const data = await http.get<PaymentIndicatorsView>('/admin/reporting/payments', { query: { start, end } });
  return verifyPaymentIndicators(data, { start, end });
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

/**
 * Zona de la instalación según CORE, lista para el punto de conexión de
 * `installationZone.ts`. Sin conectar: hay que leerla antes de montar la pantalla.
 */
export function loadInstallationZoneObservation(): Promise<unknown> {
  return readInstallationZone(() => http.get<unknown>('/admin/reporting/installation-zone'));
}

/** Puerto de auditoría candidato. Sin conectar: `exportAuditPort` sigue en null. */
export const httpExportAuditPort = exportAuditPortOver(
  (call) => http.post<unknown>('/admin/reporting/orders/export-audit', call),
);
