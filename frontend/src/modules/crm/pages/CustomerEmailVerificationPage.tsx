import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { isApiError } from '../../../shared/http/errors';
import { Alert, Button } from '../../../shared/ui';
import { confirmCustomerEmailVerification } from '../services/customerAuth';
import { useCustomerSession } from '../session';
import { CustomerAuthShell } from '../components/CustomerAuthShell';

export function CustomerEmailVerificationPage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const { refresh } = useCustomerSession();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function confirm() {
    if (!token) {
      setError('El enlace de verificación no contiene un token válido.');
      return;
    }

    setError(null);
    setSubmitting(true);

    try {
      await confirmCustomerEmailVerification(token);
      await refresh();
      setSuccess(true);
    } catch (caught) {
      setError(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : 'El enlace no es válido o ya fue utilizado.',
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <CustomerAuthShell
      title="Verificar correo"
      footer={<Link to="/entrar">Ir a mi cuenta</Link>}
    >
      <div className="pf-form">
        {success ? (
          <Alert tone="success">Tu correo quedó verificado.</Alert>
        ) : (
          <>
            {error && <Alert tone="danger">{error}</Alert>}
            <p>Confirma que deseas verificar el correo asociado a esta cuenta.</p>
            <Button
              type="button"
              size="lg"
              loading={submitting}
              onClick={() => void confirm()}
              block
            >
              Verificar correo
            </Button>
          </>
        )}
      </div>
    </CustomerAuthShell>
  );
}
