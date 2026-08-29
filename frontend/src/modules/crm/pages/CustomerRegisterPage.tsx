import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { isApiError, type ValidationErrors } from '../../../shared/http/errors';
import { Alert, Button, Field, Input } from '../../../shared/ui';
import { registerCustomer } from '../services/customerAuth';
import { CustomerAuthShell } from '../components/CustomerAuthShell';

export function CustomerRegisterPage() {
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
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
      const response = await registerCustomer({ fullName, email, password, phone });
      setSuccess(response.message);
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
      } else {
        setError(
          isApiError(caught, 'Network')
            ? 'No se pudo contactar con el servidor.'
            : 'No se pudo procesar el registro.',
        );
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <CustomerAuthShell
      title="Crear mi cuenta"
      footer={<Link to="/entrar">Ya tengo una cuenta</Link>}
    >
      <form className="pf-form" onSubmit={submit} noValidate>
        {success && (
          <Alert tone="success" title="Solicitud procesada">
            {success} Si corresponde, revisa tu correo para verificar la cuenta.
          </Alert>
        )}
        {error && <Alert tone="danger">{error}</Alert>}

        <Field label="Nombre completo" required error={errors.nombre?.[0]}>
          {(props) => (
            <Input
              {...props}
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              autoComplete="name"
              autoFocus
            />
          )}
        </Field>

        <Field label="Correo" required error={errors.correo?.[0]}>
          {(props) => (
            <Input
              {...props}
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
            />
          )}
        </Field>

        <Field label="Teléfono" hint="Opcional">
          {(props) => (
            <Input
              {...props}
              type="tel"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              autoComplete="tel"
            />
          )}
        </Field>

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
            />
          )}
        </Field>

        <Button type="submit" size="lg" loading={submitting} block>
          Crear cuenta
        </Button>
      </form>
    </CustomerAuthShell>
  );
}
