export type PortalSectionState = 'available' | 'empty' | 'unavailable' | 'error';

export interface PortalProfile {
  fullName: string;
  email: string;
  phone: string | null;
}

export interface PortalSection<T> {
  state: PortalSectionState;
  items: T[];
}

export interface CustomerOrderSummary {
  orderCode: string;
  status: string;
  totalAmount: number;
  lineCount: number;
  placedAt: string;
  paymentDueAt: string;
}

export interface CustomerTrackingSummary {
  visibleCode: string;
  currentStatus: string;
  receivedAt: string;
  promisedAt: string | null;
  lastStatusChangedAt: string;
}

export interface PortalOverview {
  profile: PortalProfile;
  orders: PortalSection<CustomerOrderSummary>;
  work: PortalSection<CustomerTrackingSummary>;
}

export interface CustomerOrderLine {
  productName: string;
  variantValue: string | null;
  saleUnit: string | null;
  quantity: number;
  unitPrice: number;
  subtotal: number;
}

export interface CustomerOrderDetail {
  orderCode: string;
  status: string;
  totalAmount: number;
  placedAt: string;
  paymentDueAt: string;
  lines: CustomerOrderLine[];
}

export interface CustomerTrackingWorkItem {
  serviceName: string;
  publicDescription: string | null;
  quantity: number;
  saleUnit: string | null;
}

export interface CustomerTrackingDetail extends CustomerTrackingSummary {
  items: CustomerTrackingWorkItem[];
}
