import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { http } from '../shared/http/client';
import { isApiError } from '../shared/http/errors';
import { Alert, Button, Card, Field, Input } from '../shared/ui';
import { MIN_LENGTH, requirements, strength } from './password';
import './platform.css';

interface SetupResponse {
  businessName: string;
  adminUserId: number;
  email: string;
}

const LICENSE_TYPES = [
  { value: 'trial', label: 'Prueba' },
  { value: 'subscription', label: 'Suscripción' },
  { value: 'perpetual', label: 'Perpetua' },
] as const;

/**
 * Asistente de instalación.
 *
 * Una pantalla, tres bloques. Al terminar **no inicia sesión**: devuelve al
 * login, porque encadenar instalación con sesión abierta mezcla dos flujos que
 * conviene mantener separados.
 */
export function SetupPage() {
  const navigate = useNavigate();

  const [businessName, setBusinessName] = useState('');
  const [licenseType, setLicenseType] = useState<string>('trial');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const checks = requirements(password, email, fullName);
  const score = strength(password);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      await http.post<SetupResponse>('/setup', {
        businessName,
        licenseType,
        admin: { fullName, email, password },
      });

      setDone(true);
    } catch (caught) {
      if (isApiError(caught, 'NotFound')) {
        // Ya estaba instalado: al login, sin explicaciones raras.
        navigate('/login', { replace: true });
        return;
      }

      setError(
        isApiError(caught)
          ? caught.displayMessage
          : 'No se pudo completar la instalación.',
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (done) {
    return (
      <div className="pf-centered">
        <span className="pf-centered__brand">SILLAR</span>
        <div className="pf-centered__panel">
          <Card title="Instalación completada">
            <div className="pf-form">
              <Alert tone="success">
                El sistema quedó instalado y se está reiniciando para empezar a funcionar.
              </Alert>
              <p>
                Ya puedes entrar con <strong>{email}</strong> y la contraseña que acabas de elegir.
              </p>
              <Button onClick={() => navigate('/login', { replace: true })}>
                Ir al acceso
              </Button>
            </div>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="pf-centered">
      <span className="pf-centered__brand">SILLAR</span>

      <div className="pf-centered__panel pf-centered__panel--wide">
        <Card
          title="Instalación"
          subtitle="Solo se hace una vez. Después, estos datos se cambian desde el panel."
        >
          <form className="pf-form" onSubmit={submit} noValidate>
            {error && <Alert tone="danger" title="No se pudo instalar">{error}</Alert>}

            <fieldset className="pf-form__section" style={{ border: 'none', margin: 0, padding: 0 }}>
              <legend className="pf-form__legend">El negocio</legend>

              <Field label="Nombre del negocio" required>
                {(props) => (
                  <Input
                    {...props}
                    value={businessName}
                    onChange={(event) => setBusinessName(event.target.value)}
                    maxLength={150}
                    autoComplete="organization"
                  />
                )}
              </Field>

              <Field label="Tipo de licencia" required>
                {(props) => (
                  <select
                    {...props}
                    className="ui-input"
                    value={licenseType}
                    onChange={(event) => setLicenseType(event.target.value)}
                  >
                    {LICENSE_TYPES.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                )}
              </Field>
            </fieldset>

            <fieldset className="pf-form__section" style={{ border: 'none', margin: 0, padding: 0 }}>
              <legend className="pf-form__legend">Primer administrador</legend>

              <Field label="Nombre completo" required>
                {(props) => (
                  <Input
                    {...props}
                    value={fullName}
                    onChange={(event) => setFullName(event.target.value)}
                    maxLength={150}
                    autoComplete="name"
                  />
                )}
              </Field>

              <Field label="Correo" hint="Será tu identificador para entrar." required>
                {(props) => (
                  <Input
                    {...props}
                    type="email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    maxLength={150}
                    autoComplete="email"
                  />
                )}
              </Field>

              <Field
                label="Contraseña"
                required
                hint={
                  // Los requisitos, ANTES de escribir. Nadie debería descubrirlos
                  // fallando.
                  <ul className="pf-requirements">
                    {checks.map((requirement) => (
                      <li key={requirement.text} data-met={requirement.met}>
                        {requirement.text}
                      </li>
                    ))}
                  </ul>
                }
              >
                {(props) => (
                  <>
                    <Input
                      {...props}
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(event) => setPassword(event.target.value)}
                      minLength={MIN_LENGTH}
                      autoComplete="new-password"
                    />
                    <div className="pf-strength" aria-hidden="true">
                      {[1, 2, 3, 4].map((level) => (
                        <span
                          key={level}
                          className="pf-strength__bar"
                          data-active={score >= level}
                        />
                      ))}
                    </div>
                  </>
                )}
              </Field>

              <button
                type="button"
                className="pf-inline-toggle"
                onClick={() => setShowPassword((visible) => !visible)}
              >
                {showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}
              </button>
            </fieldset>

            <Button type="submit" size="lg" loading={submitting} block>
              Instalar
            </Button>
          </form>
        </Card>
      </div>
    </div>
  );
}
