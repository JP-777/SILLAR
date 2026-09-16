import { Alert, Button, Card } from '../shared/ui';
import { describirFalloDeArranque, type PasoDeArranque } from './fallos';
import './platform.css';

/**
 * El arranque no pudo completarse.
 *
 * **Dice solo lo que se sabe.** Antes afirmaba siempre «no se pudo consultar
 * qué módulos están activos» y aconsejaba revisar un módulo recién activado,
 * aunque lo que hubiera fallado fuese la primera pregunta del arranque o un
 * corte de red sin ningún módulo de por medio (H22). Ahora nombra la pregunta
 * que falló y de qué forma, y no atribuye causa. Ver `fallos.ts`.
 */
export function PlatformErrorPage({
  paso,
  error,
  onRetry,
}: {
  paso: PasoDeArranque;
  error: unknown;
  onRetry: () => void;
}) {
  const { que, como, pista } = describirFalloDeArranque(paso, error);

  return (
    <div className="pf-centered">
      <span className="pf-centered__brand">SILLAR</span>

      <div className="pf-centered__panel">
        <Card title="No se pudo cargar el sistema">
          <div className="pf-form">
            <Alert tone="danger">{que}</Alert>

            {como && <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>{como}</p>}

            {pista && <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>{pista}</p>}

            <Button onClick={onRetry}>Reintentar</Button>
          </div>
        </Card>
      </div>
    </div>
  );
}
