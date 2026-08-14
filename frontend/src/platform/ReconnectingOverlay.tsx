import { useEffect, useState } from 'react';
import { connection, type ConnectionStatus } from '../shared/http/connection';
import { Button, Spinner } from '../shared/ui';
import './platform.css';

/**
 * Se muestra mientras el servidor no responde.
 *
 * Se monta **una sola vez, en la raíz de la aplicación**, no dentro de la
 * pantalla de módulos. El estado que lee vive fuera de React, así que sigue ahí
 * aunque el usuario navegue a otra parte mientras el host se reinicia.
 *
 * El mensaje es honesto: nada de barras de progreso falsas. Se dice cuánto lleva
 * y qué pasa.
 */
export function ReconnectingOverlay() {
  const [status, setStatus] = useState<ConnectionStatus>(connection.current);

  useEffect(() => connection.subscribe(setStatus), []);

  if (status.state === 'online') {
    return null;
  }

  const failed = status.state === 'failed';

  return (
    <div
      className="pf-overlay"
      role="alertdialog"
      aria-modal="true"
      aria-labelledby="reconexion-titulo"
      aria-describedby="reconexion-mensaje"
    >
      <div className="pf-overlay__panel">
        {!failed && <Spinner size="lg" label="Reconectando" />}

        <h2 className="pf-overlay__title" id="reconexion-titulo">
          {failed ? 'No se pudo reconectar' : 'Aplicando el cambio'}
        </h2>

        <p className="pf-overlay__message" id="reconexion-mensaje">
          {failed
            ? 'El servidor lleva más de un minuto sin responder. Comprueba que el servicio esté ' +
              'supervisado y que se relance solo tras detenerse.'
            : 'El sistema se está reiniciando, esto tarda unos segundos. Tu sesión sigue abierta.'}
        </p>

        {!failed && status.elapsedSeconds > 0 && (
          <p className="pf-overlay__elapsed">{status.elapsedSeconds} s</p>
        )}

        {failed && (
          <Button onClick={() => connection.retryNow()} variant="secondary">
            Reintentar
          </Button>
        )}
      </div>
    </div>
  );
}
