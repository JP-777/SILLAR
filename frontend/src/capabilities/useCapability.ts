import { useContext } from 'react';
import { CapabilitiesContext, type CapabilitiesValue } from './CapabilitiesProvider';

/** Acceso a los módulos activos de esta instalación. */
export function useCapability(): CapabilitiesValue {
  const value = useContext(CapabilitiesContext);

  if (!value) {
    throw new Error('useCapability se usó fuera de CapabilitiesProvider.');
  }

  return value;
}
