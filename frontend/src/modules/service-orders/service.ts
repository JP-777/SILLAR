import { http } from '../../shared/http/client';
import type {
  CreateServiceOrderRequest,
  PagedResult,
  ServiceOrderDetail,
  ServiceOrderFilters,
  ServiceOrderSummary,
  ServiceOrderTransitionResult,
  UpdateServiceOrderRequest,
  ServiceOrderStatus,
} from './contracts';

const base = '/admin/service-orders';
const orderPath = (id: string) => `${base}/${encodeURIComponent(id)}`;

/** Cliente exclusivo de M05b. La cookie y CSRF los gestiona shared/http/client. */
export const serviceOrdersService = {
  list: (filters: ServiceOrderFilters = {}) =>
    http.get<PagedResult<ServiceOrderSummary>>(base, { query: { ...filters } }),

  detail: (id: string) =>
    http.get<ServiceOrderDetail>(orderPath(id)),

  /** Conservar la misma idempotencyKey si se reintenta un envío incierto. */
  create: (request: CreateServiceOrderRequest) =>
    http.post<ServiceOrderDetail>(base, request),

  update: (id: string, request: UpdateServiceOrderRequest) =>
    http.put<ServiceOrderDetail>(orderPath(id), request),

  take: (id: string, expectedUpdatedAt: string) =>
    http.post<ServiceOrderDetail>(`${orderPath(id)}/take`, { expectedUpdatedAt }),

  unassign: (id: string, expectedUpdatedAt: string) =>
    http.post<ServiceOrderDetail>(`${orderPath(id)}/unassign`, { expectedUpdatedAt }),

  /** El servidor decide transiciones legales y permiso admin+ para cancelar. */
  transition: (id: string, expectedStatus: ServiceOrderStatus, targetStatus: ServiceOrderStatus) =>
    http.post<ServiceOrderTransitionResult>(`${orderPath(id)}/transition`, {
      expectedStatus,
      targetStatus,
    }),
};

/** Subconjunto del DTO público de M05a; no se comparte ninguna entidad EF. */
export interface PublicServiceChoice { id: number; name: string; price: number | null }
