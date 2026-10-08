import { http } from '../../../shared/http/client';
import type {
  AddTrackingNoteRequest,
  SetTrackingDueRequest,
  SetTrackingPriorityRequest,
  TrackingBoard,
  TrackingMutation,
  TrackingNote,
  TrackingOrderDetail,
  TrackingScope,
  TrackingTransitionResult,
  TransitionTrackingStatusRequest,
} from './contracts';

const base = '/admin/tracking';

export const trackingService = {
  board: (scope: TrackingScope, page = 1, pageSize = 20) =>
    http.get<TrackingBoard>(`${base}/board`, { query: { scope, page, pageSize } }),

  detail: (serviceOrderId: string) =>
    http.get<TrackingOrderDetail>(`${base}/orders/${serviceOrderId}`),

  setPriority: (serviceOrderId: string, request: SetTrackingPriorityRequest) =>
    http.put<TrackingMutation>(`${base}/orders/${serviceOrderId}/priority`, request),

  setDue: (serviceOrderId: string, request: SetTrackingDueRequest) =>
    http.put<TrackingMutation>(`${base}/orders/${serviceOrderId}/due`, request),

  addNote: (serviceOrderId: string, request: AddTrackingNoteRequest) =>
    http.post<TrackingNote>(`${base}/orders/${serviceOrderId}/notes`, request),

  deactivateNote: (trackingNoteId: string) =>
    http.delete<TrackingNote>(`${base}/notes/${trackingNoteId}`),

  transition: (serviceOrderId: string, request: TransitionTrackingStatusRequest) =>
    http.put<TrackingTransitionResult>(`${base}/orders/${serviceOrderId}/status`, request),
};
