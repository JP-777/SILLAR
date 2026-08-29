import {
  createContext,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { customerHttp } from '../../../shared/http/client';
import {
  clearCustomerHttpSession,
  fetchCustomerSession,
  loginCustomer,
  logoutCustomer,
  type CustomerIdentity,
} from '../services/customerAuth';

export interface CustomerSessionValue {
  customer: CustomerIdentity | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<CustomerIdentity>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
  clear: () => void;
}

export const CustomerSessionContext =
  createContext<CustomerSessionValue | null>(null);

export function CustomerSessionProvider({
  initialCustomer,
  children,
}: {
  initialCustomer: CustomerIdentity | null;
  children: ReactNode;
}) {
  const [customer, setCustomer] =
    useState<CustomerIdentity | null>(initialCustomer);

  const clear = useCallback(() => {
    clearCustomerHttpSession();
    setCustomer(null);
  }, []);

  useEffect(() => {
    customerHttp.onUnauthorized(clear);

    return () => {
      customerHttp.onUnauthorized(null);
    };
  }, [clear]);

  const login = useCallback(async (email: string, password: string) => {
    const authenticated = await loginCustomer(email, password);
    setCustomer(authenticated);
    return authenticated;
  }, []);

  const logout = useCallback(async () => {
    try {
      await logoutCustomer();
    } finally {
      clear();
    }
  }, [clear]);

  const refresh = useCallback(async () => {
    setCustomer(await fetchCustomerSession());
  }, []);

  const value = useMemo<CustomerSessionValue>(
    () => ({
      customer,
      isAuthenticated: customer !== null,
      login,
      logout,
      refresh,
      clear,
    }),
    [customer, login, logout, refresh, clear],
  );

  return (
    <CustomerSessionContext value={value}>
      {children}
    </CustomerSessionContext>
  );
}
