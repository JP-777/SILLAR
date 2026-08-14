import type { Role } from '../session/SessionProvider';

/**
 * Una entrada del menú lateral.
 *
 * Las aporta cada módulo junto a sus rutas. La aplicación monta solo las de los
 * módulos activos, así que **no hay ninguna entrada escrita a mano** en el
 * armazón: un módulo inactivo no aparece deshabilitado ni tachado, sencillamente
 * no aparece.
 */
export interface NavItem {
  /** Ruta a la que lleva. */
  readonly to: string;
  /** Texto visible, en español. */
  readonly label: string;
  /** Rol mínimo para verla. Sin él, no se muestra. */
  readonly minimumRole?: Role;
}

/**
 * Lo que cada módulo exporta para integrarse en el panel.
 *
 * Cuando exista M01, traerá su `routes.ts` con una constante de este tipo y la
 * aplicación la recogerá si el módulo está activo. Hoy no hay ninguno: la lista
 * de abajo está vacía a propósito.
 */
export interface ModuleNavigation {
  /** Código del módulo, el mismo que devuelve `/api/capabilities`. */
  readonly moduleCode: string;
  /** Título del grupo en el menú. */
  readonly group: string;
  readonly items: readonly NavItem[];
}

/**
 * Navegación aportada por los módulos.
 *
 * Vacío hasta que exista el primer módulo con interfaz. Las pantallas de
 * administración de CORE llegan en su propia entrega, después de F-08, y se
 * añadirán aquí como un elemento más.
 */
export const MODULE_NAVIGATION: readonly ModuleNavigation[] = [];

/** Filtra la navegación por módulos activos y por rol. */
export function visibleNavigation(
  isActive: (code: string) => boolean,
  hasRole: (minimum: Role) => boolean,
): ModuleNavigation[] {
  return MODULE_NAVIGATION.filter((entry) => isActive(entry.moduleCode))
    .map((entry) => ({
      ...entry,
      items: entry.items.filter((item) => !item.minimumRole || hasRole(item.minimumRole)),
    }))
    .filter((entry) => entry.items.length > 0);
}
