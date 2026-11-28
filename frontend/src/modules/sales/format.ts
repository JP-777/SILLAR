export const orderStates: Record<string, string> = {
  pending_payment: 'Pendiente de pago', payment_to_verify: 'Pago por verificar', preparing: 'Preparando',
  ready_for_pickup: 'Listo para recoger', delivered: 'Entregado', expired: 'Vencido', cancelled: 'Cancelado',
};
export function amount(value: number, currency: string | null): string {
  if (!currency) return `${value.toLocaleString('es-PE', { minimumFractionDigits: 2 })} (moneda no registrada)`;
  return new Intl.NumberFormat('es-PE', { style: 'currency', currency }).format(value);
}
export function date(value: string): string {
  return new Intl.DateTimeFormat('es-PE', { timeZone: 'America/Lima', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
