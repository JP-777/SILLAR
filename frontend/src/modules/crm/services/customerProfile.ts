import { customerHttp } from '../../../shared/http/client';

export interface CustomerAddress {
  customerAddressId: string;
  label: string | null;
  addressLine: string;
  district: string | null;
  province: string | null;
  department: string | null;
  reference: string | null;
  isPreferred: boolean;
}

export interface CustomerProfile {
  customerId: string;
  fullName: string;
  email: string;
  phone: string | null;
  documentType: string | null;
  documentNumber: string | null;
  emailVerified: boolean;
  addresses: CustomerAddress[];
}

export interface UpdateCustomerProfileInput {
  fullName: string;
  email: string;
  phone: string;
  documentType: string;
  documentNumber: string;
}

export interface SaveCustomerAddressInput {
  label: string;
  addressLine: string;
  district: string;
  province: string;
  department: string;
  reference: string;
  isPreferred: boolean;
}

export function getCustomerProfile(): Promise<CustomerProfile> {
  return customerHttp.get<CustomerProfile>('/customer/profile');
}

export function updateCustomerProfile(
  input: UpdateCustomerProfileInput,
): Promise<CustomerProfile> {
  return customerHttp.put<CustomerProfile>('/customer/profile', {
    fullName: input.fullName,
    email: input.email,
    phone: optional(input.phone),
    documentType: optional(input.documentType),
    documentNumber: optional(input.documentNumber),
  });
}

export function createCustomerAddress(
  input: SaveCustomerAddressInput,
): Promise<CustomerAddress> {
  return customerHttp.post<CustomerAddress>(
    '/customer/profile/addresses',
    addressBody(input),
  );
}

export function updateCustomerAddress(
  customerAddressId: string,
  input: SaveCustomerAddressInput,
): Promise<CustomerAddress> {
  return customerHttp.put<CustomerAddress>(
    `/customer/profile/addresses/${customerAddressId}`,
    addressBody(input),
  );
}

export function setPreferredCustomerAddress(
  customerAddressId: string,
): Promise<CustomerAddress> {
  return customerHttp.post<CustomerAddress>(
    `/customer/profile/addresses/${customerAddressId}/preferred`,
  );
}

export function deleteCustomerAddress(
  customerAddressId: string,
): Promise<void> {
  return customerHttp.delete<void>(
    `/customer/profile/addresses/${customerAddressId}`,
  );
}

function addressBody(input: SaveCustomerAddressInput) {
  return {
    label: optional(input.label),
    addressLine: input.addressLine,
    district: optional(input.district),
    province: optional(input.province),
    department: optional(input.department),
    reference: optional(input.reference),
    isPreferred: input.isPreferred,
  };
}

function optional(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
