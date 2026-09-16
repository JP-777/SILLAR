import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useCapability } from '../capabilities/useCapability';
import { useSession } from '../session';
import { roleLabel } from '../session/roleVocabulary';
import { usePublicSettings } from '../platform/usePublicSettings';
import { Button } from '../shared/ui';
import { Drawer } from '../shared/ui/patterns';
import { ThemeToggle } from '../shared/ui/ThemeToggle';
import { visibleNavigation } from './navigation';
import './layout.css';

/**
 * Armazón del panel.
 *
 * En escritorio conserva la navegación lateral. En ancho estrecho H29
 * reutiliza el Drawer compartido: no crea una segunda primitiva modal y,
 * por tanto, conserva la misma trampa y devolución de foco.
 */
export function AdminShell() {
  const { has, version } = useCapability();
  const { user, hasRole, logout } = useSession();
  const location = useLocation();
  const businessName = usePublicSettings().get('business_name');

  const groups = visibleNavigation(has, hasRole);
  const destinationCount =
    1 + groups.reduce((total, group) => total + group.items.length, 0);
  const usesDisclosures = destinationCount >= 6;

  const currentGroup =
    groups.find((group) =>
      group.items.some(
        (item) =>
          location.pathname === item.to ||
          location.pathname.startsWith(`${item.to}/`),
      ),
    )?.moduleCode ?? null;

  const [navigationOpen, setNavigationOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);
  const [expandedGroups, setExpandedGroups] = useState<ReadonlySet<string>>(
    () => new Set(),
  );

  const accountBox = useRef<HTMLDivElement>(null);
  const accountTrigger = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!navigationOpen || !usesDisclosures) {
      return;
    }

    setExpandedGroups(currentGroup ? new Set([currentGroup]) : new Set());
  }, [navigationOpen, usesDisclosures, currentGroup]);

  useEffect(() => {
    if (!accountOpen) {
      return;
    }

    function closeFromOutside(event: PointerEvent) {
      if (
        accountBox.current &&
        event.target instanceof Node &&
        !accountBox.current.contains(event.target)
      ) {
        setAccountOpen(false);
      }
    }

    function closeFromKeyboard(event: KeyboardEvent) {
      if (event.key !== 'Escape') {
        return;
      }

      setAccountOpen(false);
      requestAnimationFrame(() => accountTrigger.current?.focus());
    }

    document.addEventListener('pointerdown', closeFromOutside);
    document.addEventListener('keydown', closeFromKeyboard);

    return () => {
      document.removeEventListener('pointerdown', closeFromOutside);
      document.removeEventListener('keydown', closeFromKeyboard);
    };
  }, [accountOpen]);

  const displayBusiness =
    businessName && businessName !== 'PENDIENTE_DEFINIR'
      ? businessName
      : 'Negocio sin configurar';

  const fullName = user?.fullName?.trim() || 'Cuenta';
  const shortName = fullName.split(/\s+/)[0] || 'Cuenta';
  const initials =
    fullName
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase() || '—';

  function openNavigation() {
    setAccountOpen(false);
    setNavigationOpen(true);
  }

  function closeNavigation() {
    setNavigationOpen(false);
  }

  function toggleGroup(moduleCode: string) {
    setExpandedGroups((current) => {
      const next = new Set(current);

      if (next.has(moduleCode)) {
        next.delete(moduleCode);
      } else {
        next.add(moduleCode);
      }

      return next;
    });
  }

  return (
    <div className="ly-shell">
      <aside className="ly-sidebar">
        <nav className="ly-sidebar__nav" aria-label="Secciones del panel">
          <NavLink to="/admin" end className="ly-sidebar__link">
            Inicio
          </NavLink>

          {groups.map((group) => (
            <div key={group.moduleCode}>
              <p className="ly-sidebar__group">{group.group}</p>
              {group.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className="ly-sidebar__link"
                >
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
        <button
          type="button"
          className="ly-mobile-menu"
          aria-label={navigationOpen ? 'Cerrar navegación' : 'Abrir navegación'}
          aria-expanded={navigationOpen}
          onClick={navigationOpen ? closeNavigation : openNavigation}
        >
          <span className="ly-mobile-menu__glyph" aria-hidden="true">
            <span />
            <span />
            <span />
          </span>
        </button>

        <span className="ly-topbar__business">{displayBusiness}</span>

        <div className="ly-topbar__right">
          <div className="ly-topbar__user">
            <span className="ly-topbar__name">{user?.fullName}</span>
            <span className="ly-topbar__role">
              {user ? roleLabel(user.role) : ''}
            </span>
          </div>

          <ThemeToggle />

          <NavLink to="/admin/mi-contrasena" className="ly-topbar__link">
            Mi contraseña
          </NavLink>

          <Button variant="ghost" size="sm" onClick={() => void logout()}>
            Cerrar sesión
          </Button>
        </div>

        <div className="ly-mobile-account" ref={accountBox}>
          <button
            type="button"
            ref={accountTrigger}
            className="ly-mobile-account__trigger"
            aria-label="Abrir cuenta"
            aria-expanded={accountOpen}
            aria-controls="cuenta-estrecha"
            onClick={() => {
              setNavigationOpen(false);
              setAccountOpen((current) => !current);
            }}
          >
            <span className="ly-mobile-account__avatar" aria-hidden="true">
              {initials}
            </span>
            <span className="ly-mobile-account__name">{shortName}</span>
          </button>

          {accountOpen && (
            <div
              className="ly-account-panel"
              id="cuenta-estrecha"
              aria-label="Cuenta"
            >
              <div className="ly-account-panel__identity">
                <strong>{fullName}</strong>
                <span>{user ? roleLabel(user.role) : ''}</span>
              </div>

              <ThemeToggle />

              <NavLink
                to="/admin/mi-contrasena"
                className="ly-account-panel__item"
                onClick={() => setAccountOpen(false)}
              >
                Mi contraseña
                <span aria-hidden="true">›</span>
              </NavLink>

              <button
                type="button"
                className="ly-account-panel__item ly-account-panel__item--danger"
                onClick={() => {
                  setAccountOpen(false);
                  void logout();
                }}
              >
                Cerrar sesión
              </button>
            </div>
          )}
        </div>
      </header>

      <Drawer
        open={navigationOpen}
        side="left"
        title="Navegación"
        description={`${destinationCount} ${
          destinationCount === 1 ? 'destino' : 'destinos'
        } · ${usesDisclosures ? 'grupos plegables' : 'todos visibles'}`}
        onClose={closeNavigation}
        footer={
          <div className="ly-mobile-nav__footer">
            <span>SILLAR</span>
            <span>v{version}</span>
          </div>
        }
      >
        <nav className="ly-mobile-nav" aria-label="Secciones del panel">
          <NavLink
            to="/admin"
            end
            className="ly-mobile-nav__link"
            onClick={closeNavigation}
          >
            Inicio
          </NavLink>

          {groups.map((group) => {
            const expanded =
              !usesDisclosures || expandedGroups.has(group.moduleCode);
            const regionId = `navegacion-${group.moduleCode}`;

            return (
              <section className="ly-mobile-nav__group" key={group.moduleCode}>
                {usesDisclosures ? (
                  <button
                    type="button"
                    className="ly-mobile-nav__group-toggle"
                    aria-expanded={expanded}
                    aria-controls={regionId}
                    onClick={() => toggleGroup(group.moduleCode)}
                  >
                    <span>{group.group}</span>
                    <span className="ly-mobile-nav__chevron" aria-hidden="true">
                      ›
                    </span>
                  </button>
                ) : (
                  <p className="ly-mobile-nav__group-title">{group.group}</p>
                )}

                {expanded && (
                  <div
                    className="ly-mobile-nav__items"
                    id={usesDisclosures ? regionId : undefined}
                  >
                    {group.items.map((item) => (
                      <NavLink
                        key={item.to}
                        to={item.to}
                        className="ly-mobile-nav__link"
                        onClick={closeNavigation}
                      >
                        {item.label}
                      </NavLink>
                    ))}
                  </div>
                )}
              </section>
            );
          })}
        </nav>
      </Drawer>

      <main className="ly-main" id="contenido" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  );
}
