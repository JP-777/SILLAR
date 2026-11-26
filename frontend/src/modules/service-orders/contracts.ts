export type ServiceOrderStatus = 'received' | 'in_progress' | 'ready' | 'completed' | 'cancelled';

export interface StaffSnapshot {
  displayName: string;
  adminUserId: number;
  homeNode: string;
}
export interface ServiceOrderStatusHistory {
  statusHistoryId: string;
  fromStatus: ServiceOrderStatus | null;
  toStatus: ServiceOrderStatus;
  occurredAt: string;
  performedBy: StaffSnapshot | null;
  originNode: string;
}
export interface ServiceOrderAssignmentHistory {
  assignmentEventId: string;
  action: string;
  assignee: StaffSnapshot;
  performedBy: StaffSnapshot;
  occurredAt: string;
  originNode: string;
}
export interface ServiceOrderItem {
  serviceOrderItemId: string;
  serviceSourceId: number;
  serviceSourceNode: string;
  serviceName: string;
  serviceSlug: string;
  serviceShortDescription: string | null;
  serviceDescription: string | null;
  showcasePrice: number | null;
  saleUnit: string | null;
  mediaAssetId: string | null;
  imageUrl: string | null;
  imageAltText: string | null;
  requestedDetails: string;
  quantity: number;
  agreedUnitPrice: number | null;
  sortOrder: number;
}
export interface ServiceOrderSummary {
  serviceOrderId: string;
  visibleCode: string;
  customerName: string;
  currentStatus: ServiceOrderStatus;
  receivedAt: string;
  promisedAt: string | null;
  currentAssignee: StaffSnapshot | null;
  hasPendingPrice: boolean;
  totalAmount: number | null;
  updatedAt: string;
}
export interface ServiceOrderDetail extends ServiceOrderSummary {
  customerId: string | null;
  customerPhone: string | null;
  customerEmail: string | null;
  receivedNotes: string | null;
  createdBy: StaffSnapshot;
  items: ServiceOrderItem[];
  statusHistory: ServiceOrderStatusHistory[];
  assignmentHistory: ServiceOrderAssignmentHistory[];
}
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNext: boolean;
}
export interface CreateServiceOrderLine {
  serviceId: number;
  requestedDetails: string;
  quantity: number;
  agreedUnitPrice: number | null;
}
export interface CreateServiceOrderRequest {
  idempotencyKey: string;
  customerId: string | null;
  customerName: string | null;
  customerPhone: string | null;
  customerEmail: string | null;
  receivedNotes: string | null;
  receivedAt: string | null;
  promisedAt: string | null;
  assignToMe: boolean;
  items: CreateServiceOrderLine[];
}
export interface UpdateServiceOrderLine {
  serviceOrderItemId: string | null;
  serviceId: number | null;
  requestedDetails: string;
  quantity: number;
  agreedUnitPrice: number | null;
}
export interface UpdateServiceOrderRequest {
  expectedUpdatedAt: string;
  customerId: string | null;
  customerName: string | null;
  customerPhone: string | null;
  customerEmail: string | null;
  receivedNotes: string | null;
  promisedAt: string | null;
  items: UpdateServiceOrderLine[];
}
export interface ServiceOrderTransitionResult {
  serviceOrderId: string;
  currentStatus: ServiceOrderStatus;
  historyEntry: ServiceOrderStatusHistory;
}
export interface ServiceOrderFilters {
  status?: ServiceOrderStatus;
  assigneeAdminUserId?: number;
  q?: string;
  receivedFrom?: string;
  receivedTo?: string;
  page?: number;
  pageSize?: number;
}
