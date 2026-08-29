import { anonymousHttp, customerHttp } from '../../../shared/http/client';

const CUSTOMER_CSRF_COOKIE = 'sillar_tienda_csrf';

export interface CustomerIdentity {
  customerId: string;
  fullName: string;
  email: string;
  emailVerified: boolean;
}

interface CustomerLoginResponse {
  customer: CustomerIdentity;
  csrfToken: string;
}

export async function fetchCustomerSession(): Promise<CustomerIdentity | null> {
  const customer = await customerHttp.get<CustomerIdentity | null>(
    '/customer/auth/me',
    { allowUnauthorized: true },
  );

  if (!customer) {
    customerHttp.setCsrfToken(null);
    return null;
  }

  customerHttp.setCsrfToken(readCookie(CUSTOMER_CSRF_COOKIE));
  return customer;
}

export async function loginCustomer(
  email: string,
  password: string,
): Promise<CustomerIdentity> {
  const response = await anonymousHttp.post<CustomerLoginResponse>(
    '/customer/auth/login',
    { email, password },
  );

  customerHttp.setCsrfToken(response.csrfToken);
  return response.customer;
}

export async function logoutCustomer(): Promise<void> {
  await customerHttp.post<void>('/customer/auth/logout');
}

export function clearCustomerHttpSession(): void {
  customerHttp.setCsrfToken(null);
}

function readCookie(name: string): string | null {
  const prefix = `${encodeURIComponent(name)}=`;

  for (const part of document.cookie.split(';')) {
    const candidate = part.trim();

    if (candidate.startsWith(prefix)) {
      return decodeURIComponent(candidate.slice(prefix.length));
    }
  }

  return null;
}

export interface CustomerOperationResponse {
  message: string;
}

export interface CustomerRegisterInput {
  fullName: string;
  email: string;
  password: string;
  phone: string;
}

export function registerCustomer(
  input: CustomerRegisterInput,
): Promise<CustomerOperationResponse> {
  return anonymousHttp.post<CustomerOperationResponse>(
    '/customer/auth/register',
    {
      fullName: input.fullName,
      email: input.email,
      password: input.password,
      phone: input.phone.trim() || null,
    },
  );
}

export function requestCustomerPasswordReset(
  email: string,
): Promise<CustomerOperationResponse> {
  return anonymousHttp.post<CustomerOperationResponse>(
    '/customer/auth/password-reset/request',
    { email },
  );
}

export function confirmCustomerPasswordReset(
  token: string,
  newPassword: string,
): Promise<void> {
  return anonymousHttp.post<void>(
    '/customer/auth/password-reset/confirm',
    { token, newPassword },
  );
}

export function confirmCustomerEmailVerification(token: string): Promise<void> {
  return anonymousHttp.post<void>(
    '/customer/auth/email-verification/confirm',
    { token },
  );
}

export function acceptCustomerInvitation(
  token: string,
  password: string,
): Promise<void> {
  return anonymousHttp.post<void>(
    '/customer/auth/invitation/accept',
    { token, password },
  );
}
