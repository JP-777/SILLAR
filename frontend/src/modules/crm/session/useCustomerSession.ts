import { useContext } from 'react';
import {
  CustomerSessionContext,
  type CustomerSessionValue,
} from './CustomerSessionProvider';

export function useCustomerSession(): CustomerSessionValue {
  const value = useContext(CustomerSessionContext);

  if (!value) {
    throw new Error(
      'useCustomerSession se usó fuera de CustomerSessionProvider.',
    );
  }

  return value;
}
