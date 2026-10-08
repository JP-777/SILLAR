/** Tipos uno a uno con los DTO administrativos de M06 Seguimiento. */

export type TrackingScope = 'open' | 'closed';

export interface TrackingStaff {
  displayName: string;
  adminUserId: number;
  homeNode: string;
}

export interface TrackingBoardCard {
  serviceOrderId: string;
  visibleCode: string;
  customerName: string;
  currentStatus: string;
  receivedAt: string;
  promisedAt: string | null;
  currentAssignee: TrackingStaff | null;
  boardPriority: number | null;
  pinned: boolean;
  internalDueAt: string | null;
  orderUpdatedAt: string;
}

export interface TrackingBoardColumn {
  status: string;
  displayName: string;
  displayOrder: number;
  isTerminal: boolean;
  legalTargetStatuses: string[];
  cards: TrackingBoardCard[];
}

export interface TrackingBoardPagination {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNext: boolean;
}

export interface TrackingBoard {
  columns: TrackingBoardColumn[];
  pagination: TrackingBoardPagination | null;
}

export interface TrackingWorkItem {
  serviceOrderItemId: string;
  serviceName: string;
  saleUnit: string | null;
  quantity: number;
  requestedDetails: string;
}

export interface TrackingStatusHistory {
  statusHistoryId: string;
  fromStatus: string | null;
  toStatus: string;
  occurredAt: string;
  performedBy: TrackingStaff | null;
  originNode: string;
}

export interface TrackingNote {
  trackingNoteId: string;
  body: string;
  author: TrackingStaff | null;
  createdAt: string;
  isActive: boolean;
}

export interface TrackingOrderReadOnly {
  serviceOrderId: string;
  visibleCode: string;
  customerName: string;
  currentStatus: string;
  receivedAt: string;
  promisedAt: string | null;
  items: TrackingWorkItem[];
  currentAssignee: TrackingStaff | null;
  statusHistory: TrackingStatusHistory[];
  updatedAt: string;
}

export interface TrackingEditable {
  boardPriority: number | null;
  pinned: boolean;
  internalDueAt: string | null;
  notes: TrackingNote[];
}

export interface TrackingOrderDetail {
  order: TrackingOrderReadOnly;
  tracking: TrackingEditable;
}

export interface TrackingMutation {
  serviceOrderId: string;
  boardPriority: number | null;
  pinned: boolean;
  internalDueAt: string | null;
  updatedAt: string;
}

export interface TrackingTransitionResult {
  serviceOrderId: string;
  currentStatus: string;
  historyEntry: TrackingStatusHistory;
}

export interface SetTrackingPriorityRequest {
  boardPriority: number | null;
  pinned: boolean;
  orderedPeerIds?: string[] | null;
}

export interface SetTrackingDueRequest {
  internalDueAt: string | null;
}

export interface AddTrackingNoteRequest {
  body: string;
}

export interface TransitionTrackingStatusRequest {
  expectedStatus: string;
  targetStatus: string;
}
