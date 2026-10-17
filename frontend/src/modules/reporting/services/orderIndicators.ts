/**
 * Forma de los indicadores de pedidos que entrega M10.
 *
 * Vive aparte de `reporting.ts` para que el validador y el servicio compartan
 * la misma lista de estados y el mismo tipo sin importarse el uno al otro.
 */

/** Los siete estados admitidos por M03, siempre estados actuales. */
export const orderStates = [
  { code: 'pending_payment', label: 'Pendiente de pago' },
  { code: 'payment_to_verify', label: 'Pago por verificar' },
  { code: 'preparing', label: 'En preparación' },
  // Igual que M03 (SPEC §9.0) y Portal: el cliente y el personal ven la misma frase.
  { code: 'ready_for_pickup', label: 'Listo para recoger' },
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
