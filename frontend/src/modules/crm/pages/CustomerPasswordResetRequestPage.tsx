import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { isApiError, type ValidationErrors } from '../../../shared/http/errors';
import { Alert, Button, Field, Input } from '../../../shared/ui';
import { requestCustomerPasswordReset } from '../services/customerAuth';
import { CustomerAuthShell } from '../components/CustomerAuthShell';

export function CustomerPasswordResetRequestPage() {
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrors({});
    setError(null);
    setSuccess(null);
    setSubmitting(true);

    try {
      const response = await requestCustomerPasswordReset(email);
      setSuccess(response.message);
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
      } else {
        setError(
          isApiError(caught, 'Network')
            ? 'No se pudo contactar con el servidor.'
            : 'No se pudo procesar la solicitud.',
        );
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <CustomerAuthShell
      title="Recuperar contraseña"
      footer={<Link to="/entrar">Volver a entrar</Link>}
    >
      <form className="pf-form" onSubmit={submit} noValidate>
        {success && <Alert tone="success">{success}</Alert>}
        {error && <Alert tone="danger">{error}</Alert>}

        <Field label="Correo" required error={errors.correo?.[0]}>
          {(props) => (
            <Input
              {...props}
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              autoFocus
            />
          )}
        </Field>

        <Button type="submit" size="lg" loading={submitting} block>
          Solicitar enlace
        </Button>
      </form>
    </CustomerAuthShell>
  );
}
