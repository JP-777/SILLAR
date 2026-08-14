import { Alert, Button, Card } from '../shared/ui';
import './platform.css';

/**
 * El arranque no pudo completarse.
 *
 * Se llega aquí cuando `GET /api/capabilities` falla: sin saber qué módulos hay,
 * la aplicación no sabe qué montar y no puede continuar. Mejor decirlo que
 * enseñar una pantalla en blanco.
 */
export function PlatformErrorPage({ detail, onRetry }: { detail?: string; onRetry: () => void }) {
  return (
    <div className="pf-centered">
      <span className="pf-centered__brand">SILLAR</span>

      <div className="pf-centered__panel">
        <Card title="No se pudo cargar el sistema">
          <div className="pf-form">
            <Alert tone="danger">
              No se pudo consultar qué módulos están activos, así que la aplicación no puede
              continuar.
            </Alert>

            {detail && <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>{detail}</p>}

            <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
              Comprueba que el servicio esté levantado. Si acabas de activar un módulo, es posible
              que aún esté reiniciándose.
            </p>

            <Button onClick={onRetry}>Reintentar</Button>
          </div>
        </Card>
      </div>
    </div>
  );
}
