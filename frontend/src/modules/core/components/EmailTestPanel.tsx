import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { isApiError } from '../../../shared/http/errors';
import { Alert, Badge, Button, Card, Field, Input, Spinner } from '../../../shared/ui';
import {
  settingsService,
  type EmailTestStatus,
} from '../services/settings';
import './settings.css';

export function EmailTestPanel({ defaultRecipient }: { defaultRecipient: string }) {
  const [recipient, setRecipient] = useState(defaultRecipient);
  const [status, setStatus] = useState<EmailTestStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [testing, setTesting] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const [result, setResult] = useState<{ success: boolean; message: string } | null>(null);

  const loadStatus = useCallback(async () => {
    setLoading(true);
    setFailure(null);

    try {
      setStatus(await settingsService.emailStatus());
    } catch (caught) {
      setFailure(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : 'No se pudo consultar el estado del correo saliente.',
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadStatus();
  }, [loadStatus]);

  async function testEmail(event: FormEvent) {
    event.preventDefault();
    setTesting(true);
    setFailure(null);
    setResult(null);

    try {
      const response = await settingsService.testEmail(recipient.trim());
      setResult(response);
      await loadStatus();
    } catch (caught) {
      setFailure(
        isApiError(caught)
          ? caught.displayMessage
          : 'No se pudo ejecutar la prueba SMTP.',
      );
    } finally {
      setTesting(false);
    }
  }

  return (
    <Card
      title="Diagnóstico de correo"
      subtitle="Comprueba la salida SMTP sin guardar ni mostrar la contraseña."
    >
      <div className="set-email-test">
        <Alert tone="info">
          La contraseña SMTP vive únicamente en la variable de entorno{' '}
          <code>SILLAR_SMTP_PASSWORD</code>. Nunca se guarda en la base de datos
          ni se devuelve al navegador.
        </Alert>

        {loading ? (
          <div className="set-email-test__loading">
            <Spinner label="Consultando estado SMTP" />
          </div>
        ) : status ? (
          <div className="set-email-test__status" aria-label="Estado de la última prueba SMTP">
            {status.neverTested ? (
              <Badge tone="neutral">Nunca probado</Badge>
            ) : status.lastSuccess ? (
              <Badge tone="success">Última prueba correcta</Badge>
            ) : (
              <Badge tone="danger">Última prueba fallida</Badge>
            )}

            {status.lastTestedAt && (
              <span className="set-row__hint">{formatDate(status.lastTestedAt)}</span>
            )}
          </div>
        ) : null}

        {failure && <Alert tone="danger">{failure}</Alert>}
        {result && (
          <Alert tone={result.success ? 'success' : 'danger'}>
            {result.message}
          </Alert>
        )}

        <form className="set-email-test__form" onSubmit={testEmail}>
          <Field label="Destinatario de prueba" hint="Solo se usa para este envío.">
            {(props) => (
              <Input
                {...props}
                type="email"
                value={recipient}
                onChange={(event) => setRecipient(event.target.value)}
                autoComplete="email"
              />
            )}
          </Field>

          <Button type="submit" loading={testing} disabled={recipient.trim() === ''}>
            Enviar correo de prueba
          </Button>
        </form>
      </div>
    </Card>
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('es-PE', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}
