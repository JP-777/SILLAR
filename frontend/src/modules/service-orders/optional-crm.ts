import { http } from '../../shared/http/client';

/** CRM permite consultar clientes solo con rol admin+. En M05b siempre hay alta manual. */
export interface OrderCustomerOption {
  customerId: string;
  fullName: string;
  email: string;
  phone: string | null;
  isActive: boolean;
}

export function listAvailableCrmCustomers(search: string): Promise<OrderCustomerOption[]> {
  return http.get<OrderCustomerOption[]>('/admin/crm/customers', {query: {q: search.trim() || undefined}});
}
