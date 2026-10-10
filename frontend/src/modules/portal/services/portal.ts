import { customerHttp } from '../../../shared/http/client';
import type {
  CustomerOrderDetail,
  CustomerTrackingDetail,
  PortalOverview,
} from './contracts';

export const portalService = {
  overview(limit: number, signal?: AbortSignal): Promise<PortalOverview> {
    return customerHttp.get<PortalOverview>('/portal/overview', {
      query: { limit },
      signal,
    });
  },

  order(orderCode: string, signal?: AbortSignal): Promise<CustomerOrderDetail> {
    return customerHttp.get<CustomerOrderDetail>(
      `/sales/my-orders/${encodeURIComponent(orderCode)}`,
      { signal },
    );
  },

  work(visibleCode: string, signal?: AbortSignal): Promise<CustomerTrackingDetail> {
    return customerHttp.get<CustomerTrackingDetail>(
      `/portal/work/${encodeURIComponent(visibleCode)}`,
      { signal },
    );
  },
};
