import type { ReactNode } from 'react';
import { cmsFooter } from '../modules/cms/cmsFooter';

/**
 * Una contribución al pie público, aportada por un módulo.
 *
 * **Mismo patrón y misma dirección de dependencia que `HOME_SECTIONS`**: cada
 * módulo declara lo suyo y el armazón monta solo lo de los módulos activos.
 * La plataforma no conoce ningún endpoint: quién habla con el CMS es el CMS.
 */
export interface PublicFooterContribution {
  /** Código del módulo, el mismo que devuelve `/api/capabilities`. */
  readonly moduleCode: string;
  /** Lo que ese módulo pone en el pie. */
  readonly Component: () => ReactNode;
}

/**
 * Las contribuciones del pie, en orden.
 *
 * **El orden es el de este array, y es decisión de producto, no de cada
 * módulo**, por lo mismo que en la portada: con un campo `order`, dos módulos
 * pueden pelearse por el mismo hueco y nadie lo ve hasta que se instalan
 * juntos.
 */
export const FOOTER_CONTRIBUTIONS: readonly PublicFooterContribution[] = [cmsFooter];

/** Las contribuciones de los módulos activos, en el orden del array. */
export function visibleFooterContributions(
  isActive: (code: string) => boolean,
): PublicFooterContribution[] {
  return FOOTER_CONTRIBUTIONS.filter((contribucion) => isActive(contribucion.moduleCode));
}
