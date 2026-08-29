import { Link } from 'react-router-dom';
import { Card } from '../../../shared/ui';
import '../../../platform/platform.css';

export function CustomerAuthShell({
  title,
  children,
  footer,
}: {
  title: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
}) {
  return (
    <main id="contenido" className="pf-centered">
      <Link to="/" className="pf-centered__brand" aria-label="Volver al inicio">
        SILLAR
      </Link>

      <div className="pf-centered__panel">
        <Card title={title}>
          {children}
          {footer && <div className="pf-form">{footer}</div>}
        </Card>
      </div>
    </main>
  );
}
