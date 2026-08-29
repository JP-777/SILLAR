import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { isApiError, type ValidationErrors } from '../../../shared/http/errors';
import { Alert, Button, Field, Input } from '../../../shared/ui';
import { acceptCustomerInvitation } from '../services/customerAuth';
import { CustomerAuthShell } from '../components/CustomerAuthShell';

export function CustomerInvitationPage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrors({});
    setError(null);

    if (!token) {
      setError('La invitación no contiene un token válido.');
      return;
    }

    if (password !== confirm) {
      setError('Las contraseñas no coinciden.');
      return;
    }

    setSubmitting(true);

    try {
      await acceptCustomerInvitation(token, password);
      setSuccess(true);
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
      } else {
        setError(
          isApiError(caught, 'Network')
            ? 'No se pudo contactar con el servidor.'
            : 'La invitación no es válida o ya fue utilizada.',
        );
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <CustomerAuthShell
      title="Activar cuenta"
      footer={<Link to="/entrar">Ir a iniciar sesión</Link>}
    >
      <form className="pf-form" onSubmit={submit} noValidate>
        {success && (
          <Alert tone="success">
            Cuenta activada. Ya puedes iniciar sesión.
          </Alert>
        )}
        {error && <Alert tone="danger">{error}</Alert>}

        <Field
          label="Contraseña"
          required
          hint="Usa al menos 12 caracteres."
          error={errors.contrasena?.[0]}
        >
          {(props) => (
            <Input
              {...props}
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="new-password"
              autoFocus
            />
          )}
        </Field>

        <Field label="Repite la contraseña" required>
          {(props) => (
            <Input
              {...props}
              type="password"
              value={confirm}
              onChange={(event) => setConfirm(event.target.value)}
              autoComplete="new-password"
            />
          )}
        </Field>

        <Button
          type="submit"
          size="lg"
          loading={submitting}
          disabled={success}
          block
        >
          Activar cuenta
        </Button>
      </form>
    </CustomerAuthShell>
  );
}
