import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { isApiError } from '../../../shared/http/errors';
import { Alert, Button, Field, Input } from '../../../shared/ui';
import { useCustomerSession } from '../session';
import { CustomerAuthShell } from '../components/CustomerAuthShell';

export function CustomerLoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useCustomerSession();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const returnTo =
    (location.state as { from?: string } | null)?.from ?? '/mi-cuenta';

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      await login(email, password);
      navigate(returnTo, { replace: true });
    } catch (caught) {
      setError(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : 'Correo o contraseña incorrectos.',
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <CustomerAuthShell
      title="Entrar a mi cuenta"
      footer={
        <>
          <Link to="/crear-cuenta">Crear una cuenta</Link>
          <Link to="/recuperar-contrasena">Olvidé mi contraseña</Link>
        </>
      }
    >
      <form className="pf-form" onSubmit={submit} noValidate>
        {error && <Alert tone="danger">{error}</Alert>}

        <Field label="Correo" required>
          {(props) => (
            <Input
              {...props}
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="username"
              autoFocus
            />
          )}
        </Field>

        <Field label="Contraseña" required>
          {(props) => (
            <Input
              {...props}
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
            />
          )}
        </Field>

        <Button type="submit" size="lg" loading={submitting} block>
          Entrar
        </Button>
      </form>
    </CustomerAuthShell>
  );
}
