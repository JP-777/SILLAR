import type { ReactNode } from 'react';
import { catalogHome } from '../modules/catalog/routes';
import { cmsHome } from '../modules/cms/cmsHome';
import { crmHome } from '../modules/crm/routes';

/**
 * Una sección de la portada pública, aportada por un módulo.
 *
 * **Mismo patrón que el menú del panel** (`layout/navigation.ts`): cada módulo
 * declara lo suyo junto a sus rutas y el armazón monta solo lo de los módulos
 * activos. Así **no hay ningún código de módulo escrito a mano** en la portada:
 * un módulo inactivo no aparece vacío ni con un aviso de que falta, sencillamente
 * no aparece.
 *
 * Y una diferencia con el menú que conviene decir en voz alta, porque si no
 * parece un descuido cuando alguien la encuentre: una entrada de menú es un
 * dato —una ruta y un texto—, mientras que **una sección de portada es
 * interfaz**, así que cada módulo trae su propio componente y el armazón lo
 * importa. Es lo mismo que ya hace con `catalogNavigation`, y es lo que abarata
 * la costura.
 */
export interface HomeSection {
  /** Código del módulo, el mismo que devuelve `/api/capabilities`. */
  readonly moduleCode: string;
  /** Lo que ese módulo pinta en la portada. */
  readonly Component: () => ReactNode;
}

/**
 * Las secciones de la portada, en orden.
 *
 * **El orden es el de este array, y es decisión de producto, no de cada
 * módulo.** Un módulo no debería poder declarar que va por encima de otro:
 * quien decide qué se ve primero es quien monta el producto, y eso es el
 * armazón. Por eso no hay ningún campo `order` — con uno, dos módulos pueden
 * pelearse por el mismo hueco y nadie lo ve hasta que se instalan juntos.
 */
export const HOME_SECTIONS: readonly HomeSection[] = [cmsHome, catalogHome, crmHome];

/** Las secciones de los módulos activos, en el orden del array. */
export function visibleHomeSections(isActive: (code: string) => boolean): HomeSection[] {
  return HOME_SECTIONS.filter((section) => isActive(section.moduleCode));
}
