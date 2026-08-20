import { NavLink, Outlet } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { useSession } from '../session';
import { usePublicSettings } from '../platform/usePublicSettings';
import { Button } from '../shared/ui';
import { ThemeToggle } from '../shared/ui/ThemeToggle';
import { visibleNavigation } from './navigation';
import './layout.css';

const ROLE_LABELS: Record<string, string> = {
  super_admin: 'Administrador principal',
  admin: 'Administrador',
  editor: 'Editor',
};

/**
 * Armazón del panel.
 *
 * La identidad sigue `MARCA.md` §6: el tema es siempre el de SILLAR, «SILLAR»
 * va discreto en el pie de la barra lateral y el nombre del negocio en la
 * superior. El logo del cliente no entra aquí.
 *
 * El motivo es práctico: el panel es lo que se demuestra al vender, y una
 * captura no puede filtrar quién es el cliente.
 */
export function AdminShell() {
  const { has, version } = useCapability();
  const { user, hasRole, logout } = useSession();
  const businessName = usePublicSettings().get('business_name');

  const groups = visibleNavigation(has, hasRole);

  return (
    <div className="ly-shell">
      <aside className="ly-sidebar">
        <nav className="ly-sidebar__nav" aria-label="Secciones del panel">
          <NavLink to="/admin" end className="ly-sidebar__link">
            Inicio
          </NavLink>

          {/* Construido desde las capacidades. Ninguna entrada escrita a mano:
              un módulo inactivo no aparece. */}
          {groups.map((group) => (
            <div key={group.moduleCode}>
              <p className="ly-sidebar__group">{group.group}</p>
              {group.items.map((item) => (
                <NavLink key={item.to} to={item.to} className="ly-sidebar__link">
                  {item.label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div className="ly-sidebar__footer">
          <span className="ly-sidebar__brand">SILLAR</span>
          <span className="ly-sidebar__version">v{version}</span>
        </div>
      </aside>

      <header className="ly-topbar">
        <span className="ly-topbar__business">
          {businessName && businessName !== 'PENDIENTE_DEFINIR'
            ? businessName
            : 'Negocio sin configurar'}
        </span>

        <div className="ly-topbar__right">
          <div className="ly-topbar__user">
            <span className="ly-topbar__name">{user?.fullName}</span>
            <span className="ly-topbar__role">
              {user ? (ROLE_LABELS[user.role] ?? user.role) : ''}
            </span>
          </div>

          <ThemeToggle />

          {/* Cambiar la contraseña cuelga de aquí y no del menú lateral: es una
              acción sobre uno mismo, no administración de otros. */}
          <NavLink to="/admin/mi-contrasena" className="ly-topbar__link">
            Mi contraseña
          </NavLink>

          <Button variant="ghost" size="sm" onClick={() => void logout()}>
            Cerrar sesión
          </Button>
        </div>
      </header>

      {/* `tabIndex={-1}` va **en el marcado**, no puesto por `RouteFocus`.
          Ése no actúa en la carga inicial —y hace bien—, así que en la
          primera pantalla el `main` no era enfocable y «Saltar al contenido»
          cambiaba el hash sin mover el foco: el salto no funcionaba
          justamente la vez que más se usa. No entra en el recorrido de Tab:
          -1 solo lo hace enfocable por código o por fragmento. */}
      <main className="ly-main" id="contenido" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  );
}
