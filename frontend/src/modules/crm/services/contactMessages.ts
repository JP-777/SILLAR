import { anonymousHttp, http } from '../../../shared/http/client';

export interface PublicContactInput {
  fullName: string;
  email: string;
  phone: string;
  subject: string;
  message: string;
}

export interface PublicContactAccepted {
  message: string;
}

export interface AdminContactMessageListItem {
  contactMessageId: number;
  customerId: string | null;
  fullName: string;
  email: string | null;
  phone: string | null;
  subject: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface AdminContactMessageDetail extends AdminContactMessageListItem {
  message: string;
  updatedAt: string;
}

export function submitPublicContact(
  input: PublicContactInput,
): Promise<PublicContactAccepted> {
  return anonymousHttp.post<PublicContactAccepted>('/contact', {
    fullName: input.fullName,
    email: optional(input.email),
    phone: optional(input.phone),
    subject: optional(input.subject),
    message: input.message,
  });
}

export const adminContactMessagesService = {
  list(includeInactive = false): Promise<AdminContactMessageListItem[]> {
    return http.get<AdminContactMessageListItem[]>(
      '/admin/crm/contact-messages',
      { query: { includeInactive } },
    );
  },

  get(contactMessageId: number): Promise<AdminContactMessageDetail> {
    return http.get<AdminContactMessageDetail>(
      `/admin/crm/contact-messages/${contactMessageId}`,
    );
  },

  deactivate(contactMessageId: number): Promise<AdminContactMessageDetail> {
    return http.delete<AdminContactMessageDetail>(
      `/admin/crm/contact-messages/${contactMessageId}`,
    );
  },
};

function optional(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
