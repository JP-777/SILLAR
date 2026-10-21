import type { CreateServiceOrderRequest } from './contracts';

/** Ante una respuesta incierta, la misma clave solo se reenvía con el MISMO cuerpo. */
export function sameCreateAttempt(a: CreateServiceOrderRequest, b: CreateServiceOrderRequest): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

export function mayEditAfterFailure(kind: string | null): boolean {
  return kind === 'ValidationFailed' || kind === 'NotFound';
}
