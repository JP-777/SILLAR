import { Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import { RequireRole } from '../../session';
import { ModulesPage } from './pages/ModulesPage';
import { UsersPage } from './pages/UsersPage';
import { ChangePasswordPage } from './pages/ChangePasswordPage';
import { SettingsPage } from './pages/SettingsPage';
import { AuditPage } from './pages/AuditPage';
import { MediaPage } from './pages/MediaPage';

/**
 * Lo que el módulo CORE aporta al panel.
 *
 * Es el patrón que seguirá cada módulo: exporta su navegación y sus rutas, y la
 * aplicación monta solo lo de los módulos activos.
 */
export const coreNavigation: ModuleNavigation = {
  moduleCode: 'core',
  group: 'Sistema',
  items: [
    { to: '/admin/modulos', label: 'Módulos', minimumRole: 'admin' },
    { to: '/admin/configuracion', label: 'Configuración', minimumRole: 'admin' },
    { to: '/admin/archivos', label: 'Archivos', minimumRole: 'editor' },
    // Solo `super_admin`. La entrada NO EXISTE para los demás roles: no aparece
    // deshabilitada ni tachada. Lo resuelve `visibleNavigation`, que filtra por
    // rol además de por módulo activo.
    { to: '/admin/usuarios', label: 'Usuarios', minimumRole: 'super_admin' },
    { to: '/admin/auditoria', label: 'Auditoría', minimumRole: 'super_admin' },
  ],
};

/**
 * Rutas del módulo.
 *
 * Se declaran sueltas para poder anidarlas bajo el armazón del panel. La
 * autorización real vive en el backend; estas guardas evitan enseñar lo que no
 * se puede usar.
 */
export const coreRoutes = (
  <>
    {/* Las guardas evitan la petición y enseñan la pantalla de acceso denegado
        a quien llegue escribiendo la ruta. No sustituyen a la autorización del
        backend, que es la que decide de verdad: estas son de presentación. */}
    <Route element={<RequireRole minimum="editor" />}>
      <Route path="archivos" element={<MediaPage />} />
    </Route>

    <Route element={<RequireRole minimum="admin" />}>
      <Route path="modulos" element={<ModulesPage />} />
      <Route path="configuracion" element={<SettingsPage />} />
    </Route>

    <Route element={<RequireRole minimum="super_admin" />}>
      <Route path="usuarios" element={<UsersPage />} />
      <Route path="auditoria" element={<AuditPage />} />
    </Route>

    {/* Sin rol mínimo: cambiar la contraseña propia es una acción sobre uno
        mismo y cualquiera con sesión puede hacerlo. */}
    <Route path="mi-contrasena" element={<ChangePasswordPage />} />
  </>
);
