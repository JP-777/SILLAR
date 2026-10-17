import { http } from '../../../shared/http/client';

/** Los siete estados admitidos por M03, siempre estados actuales. */
export const orderStates = [
  { code: 'pending_payment', label: 'Pendiente de pago' },
  { code: 'payment_to_verify', label: 'Pago por verificar' },
  { code: 'preparing', label: 'En preparación' },
  { code: 'ready_for_pickup', label: 'Listo para recojo' },
  { code: 'delivered', label: 'Entregado' },
  { code: 'expired', label: 'Vencido' },
  { code: 'cancelled', label: 'Cancelado' },
] as const;

export interface OrderIndicatorsView {
  status: 'source_unavailable' | 'empty' | 'with_data';
  startInclusive: string;
  endExclusive: string;
  observedAtUtc: string | null;
  i01CreatedOrders: number | null;
  i03CurrentStatusCounts: { status: string; count: number }[] | null;
  i04CurrentlyExpiredOrders: number | null;
  activeOrders: number | null;
  inactiveOrders: number | null;
}

/** Solo contrato administrativo M10; el frontend nunca consulta M03 directamente. */
export function loadOrderIndicators(start: string, end: string): Promise<OrderIndicatorsView> {
  return http.get<OrderIndicatorsView>('/admin/reporting/orders', { query: { start, end } });
}
