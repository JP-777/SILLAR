import type { ModuleNavigation } from '../../layout/navigation';

/** Navegación propia M10; la composición filtra capacidad y rol. */
export const reportingNavigation: ModuleNavigation = {
  moduleCode: 'reporting',
  group: 'Reportes',
  items: [{ to: '/admin/reportes', label: 'Pedidos', minimumRole: 'admin', operation: 'read' }],
};
