import type { ReactNode } from 'react';
import { Link, NavLink } from 'react-router-dom';

const links = [
  { to: '/mi-portal', label: 'Mi portal', end: true },
  { to: '/mi-portal/pedidos', label: 'Pedidos', end: false },
  { to: '/mi-portal/trabajos', label: 'Trabajos', end: false },
  { to: '/mi-cuenta', label: 'Mi cuenta', end: false },
] as const;

export function PortalShell({ children }: { children: ReactNode }) {
  return (
    <main id="contenido" className="portal-page" tabIndex={-1}>
      <nav className="portal-nav" aria-label="Navegación de Mi portal">
        {links.map((link) => (
          <NavLink
            key={link.to}
            to={link.to}
            end={link.end}
            className={({ isActive }) =>
              `portal-nav__link${isActive ? ' portal-nav__link--active' : ''}`
            }
          >
            {link.label}
          </NavLink>
        ))}
      </nav>
      {children}
    </main>
  );
}

export function PortalPageHeader({
  title,
  description,
  backTo,
  backLabel,
}: {
  title: string;
  description?: string;
  backTo?: string;
  backLabel?: string;
}) {
  return (
    <header className="portal-header">
      {backTo && backLabel && (
        <Link className="portal-back-link" to={backTo}>
          ← {backLabel}
        </Link>
      )}
      <h1>{title}</h1>
      {description && <p>{description}</p>}
    </header>
  );
}
