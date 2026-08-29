import { http } from '../../../shared/http/client';

export type CustomerAccessState =
  | 'active'
  | 'invited'
  | 'no_account'
  | 'deactivated'
  | 'blocked';

export interface AdminCustomerAccess {
  state: CustomerAccessState;
  since: string | null;
  emailVerified: boolean;
  invitationExpiresAt: string | null;
}

export interface AdminCustomerListItem {
  customerId: string;
  fullName: string;
  email: string;
  phone: string | null;
  documentType: string | null;
  documentNumber: string | null;
  isActive: boolean;
  access: AdminCustomerAccess;
}

export interface AdminCustomerAddress {
  customerAddressId: string;
  label: string | null;
  addressLine: string;
  district: string | null;
  province: string | null;
  department: string | null;
  reference: string | null;
  isPreferred: boolean;
  isActive: boolean;
}

export interface AdminCustomerDetail extends AdminCustomerListItem {
  internalNotes: string | null;
  deactivatedAt: string | null;
  blockedAt: string | null;
  reactivationRequestedAt: string | null;
  reactivationResolvedAt: string | null;
  addresses: AdminCustomerAddress[];
  createdAt: string;
  updatedAt: string;
}

export interface SaveAdminCustomerInput {
  fullName: string;
  email: string;
  phone: string;
  documentType: string;
  documentNumber: string;
  internalNotes: string;
}

export interface AdminCustomerInvitation {
  emailSent: boolean;
  message: string;
  invitationExpiresAt: string;
}

const BASE = '/admin/crm/customers';

export const adminCustomersService = {
  list(q?: string): Promise<AdminCustomerListItem[]> {
    return http.get<AdminCustomerListItem[]>(BASE, {
      query: { q: q?.trim() || undefined },
    });
  },

  get(customerId: string): Promise<AdminCustomerDetail> {
    return http.get<AdminCustomerDetail>(`${BASE}/${customerId}`);
  },

  create(input: SaveAdminCustomerInput): Promise<AdminCustomerDetail> {
    return http.post<AdminCustomerDetail>(BASE, payload(input));
  },

  update(
    customerId: string,
    input: SaveAdminCustomerInput,
  ): Promise<AdminCustomerDetail> {
    return http.put<AdminCustomerDetail>(
      `${BASE}/${customerId}`,
      payload(input),
    );
  },

  deactivate(customerId: string): Promise<AdminCustomerDetail> {
    return http.delete<AdminCustomerDetail>(`${BASE}/${customerId}`);
  },

  reactivate(customerId: string): Promise<AdminCustomerDetail> {
    return http.post<AdminCustomerDetail>(
      `${BASE}/${customerId}/reactivate`,
    );
  },

  invite(customerId: string): Promise<AdminCustomerInvitation> {
    return http.post<AdminCustomerInvitation>(
      `${BASE}/${customerId}/invite`,
    );
  },
};

function payload(input: SaveAdminCustomerInput) {
  return {
    fullName: input.fullName,
    email: input.email,
    phone: optional(input.phone),
    documentType: optional(input.documentType),
    documentNumber: optional(input.documentNumber),
    internalNotes: optional(input.internalNotes),
  };
}

function optional(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
