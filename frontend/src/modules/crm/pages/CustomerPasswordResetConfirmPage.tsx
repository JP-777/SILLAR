import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { isApiError, type ValidationErrors } from '../../../shared/http/errors';
import { Alert, Button, Field, Input } from '../../../shared/ui';
import { confirmCustomerPasswordReset } from '../services/customerAuth';
import { CustomerAuthShell } from '../components/CustomerAuthShell';

export function CustomerPasswordResetConfirmPage() {
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
      setError('El enlace de recuperación no contiene un token válido.');
      return;
    }

    if (password !== confirm) {
      setError('Las contraseñas no coinciden.');
      return;
    }

    setSubmitting(true);

    try {
      await confirmCustomerPasswordReset(token, password);
      setSuccess(true);
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
      } else {
        setError(
          isApiError(caught, 'Network')
            ? 'No se pudo contactar con el servidor.'
            : 'El enlace no es válido o ya fue utilizado.',
        );
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <CustomerAuthShell
      title="Restablecer contraseña"
      footer={<Link to="/entrar">Ir a iniciar sesión</Link>}
    >
      <form className="pf-form" onSubmit={submit} noValidate>
        {success && (
          <Alert tone="success">
            Contraseña actualizada. Ya puedes entrar con la nueva contraseña.
          </Alert>
        )}
        {error && <Alert tone="danger">{error}</Alert>}

        <Field
          label="Nueva contraseña"
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
          Guardar contraseña
        </Button>
      </form>
    </CustomerAuthShell>
  );
}
