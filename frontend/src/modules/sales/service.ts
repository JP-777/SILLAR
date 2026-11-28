import { http } from '../../shared/http/client';

export interface OrderSummary {
  orderCode: string; status: string; customerName: string; totalAmount: number;
  currencyCode: string | null; placedAt: string; paymentDueAt: string; version: number; needsAttention: boolean;
}
export interface Payment {
  method: string; amount: number; currencyCode: string | null; reference: string | null;
  registeredAt: string; registeredBy: string; wasLate: boolean; canFulfill: boolean | null;
}
export interface OrderDetail {
  order: OrderSummary; customerEmail: string; customerPhone: string | null;
  lines: { productName: string; variantValue: string | null; saleUnit: string | null; quantity: number; unitPrice: number; subtotal: number }[];
  payments: Payment[];
  history: { fromStatus: string | null; toStatus: string; changedAt: string; changedBy: string | null; reason: string | null }[];
}
export interface ManualPayment { requestId: string; method: string; amount: number; reference: string | null; canFulfill: boolean }
interface OperationResult { success: boolean; replayed: boolean }
const base = '/admin/sales/orders';
const path = (code: string) => `${base}/${encodeURIComponent(code)}`;
export const sales = {
  list: (status: string, attention: boolean, signal: AbortSignal) => http.get<OrderSummary[]>(base, { query: { status, attention }, signal }),
  detail: (code: string, signal: AbortSignal) => http.get<OrderDetail>(path(code), { signal }),
  pay: (code: string, input: ManualPayment) => http.post<OperationResult>(`${path(code)}/payments`, input),
  change: (code: string, action: string, expectedVersion: number, reason: string | null) =>
    http.post<OperationResult>(`${path(code)}/transitions`, { action, expectedVersion, reason }),
};
