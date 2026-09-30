import type { ModuleNavigation } from '../../layout/navigation';

/**
 * El grupo «Solicitudes» del menú. Se declara junto a las rutas (se reexporta
 * desde `routes.tsx`, único punto que importa la composición) y la aplicación
 * lo muestra **solo si la capacidad `b2b` está activa**.
 */
export const b2bNavigation: ModuleNavigation = {
  moduleCode: 'b2b',
  group: 'Solicitudes',
  items: [
    { to: '/admin/solicitudes/personalizadas', label: 'Personalizadas', minimumRole: 'editor' },
    { to: '/admin/solicitudes/institucionales', label: 'Por volumen', minimumRole: 'editor' },
    { to: '/admin/solicitudes/cotizaciones', label: 'Cotizaciones', minimumRole: 'editor' },
  ],
};
