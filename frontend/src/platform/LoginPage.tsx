import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { isApiError } from '../shared/http/errors';
import { useSession } from '../session';
import { Alert, Button, Card, Field, Input } from '../shared/ui';
import './platform.css';

/** Acceso al panel. Correo y contraseña, nada más. */
export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useSession();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lockedUntil, setLockedUntil] = useState<string | null>(null);

  const returnTo = (location.state as { from?: string } | null)?.from ?? '/admin';

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setLockedUntil(null);
    setSubmitting(true);

    try {
      await login(email, password);
      navigate(returnTo, { replace: true });
    } catch (caught) {
      if (isApiError(caught, 'Locked')) {
        // El único mensaje específico, y solo lo ve quien acertó la contraseña:
        // el backend verifica la contraseña ANTES de mirar el bloqueo.
        setLockedUntil(caught.detail ?? caught.message);
      } else if (isApiError(caught, 'Network')) {
        setError('No se pudo contactar con el servidor.');
      } else {
        // Mismo mensaje exista o no la cuenta. Cualquier diferencia convertiría
        // este formulario en un comprobador de qué correos están registrados.
        setError('Correo o contraseña incorrectos.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="pf-centered">
      <span className="pf-centered__brand">SILLAR</span>

      <div className="pf-centered__panel">
        <Card title="Acceso al panel">
          <form className="pf-form" onSubmit={submit} noValidate>
            {error && <Alert tone="danger">{error}</Alert>}
            {lockedUntil && (
              <Alert tone="warning" title="Cuenta bloqueada temporalmente">
                {lockedUntil}
              </Alert>
            )}

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
        </Card>
      </div>
    </div>
  );
}
