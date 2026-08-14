import { useContext } from 'react';
import { SessionContext, type SessionValue } from './SessionProvider';

/** Acceso a la sesión administrativa. */
export function useSession(): SessionValue {
  const value = useContext(SessionContext);

  if (!value) {
    throw new Error('useSession se usó fuera de SessionProvider.');
  }

  return value;
}
