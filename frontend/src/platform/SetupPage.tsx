import { useState, type FormEvent } from 'react';
import { http } from '../shared/http/client';
import { connection } from '../shared/http/connection';
import { isApiError } from '../shared/http/errors';
import { describirFalloDeInstalacion } from './fallos';
import { Alert, Button, Card, Field, Input } from '../shared/ui';
import { MIN_LENGTH, requirements, strength } from './password';
import './platform.css';

interface SetupResponse {
  businessName: string;
  adminUserId: number;
  email: string;
}

interface SetupStatus {
  setupRequired: boolean;
  migrationsPending?: boolean;
}

const LICENSE_TYPES = [
  { value: 'trial', label: 'Prueba' },
  { value: 'subscription', label: 'Suscripción' },
  { value: 'perpetual', label: 'Perpetua' },
] as const;

/**
 * La tabla de instalación no está donde el servidor esperaba encontrarla.
 * El asistente no intenta modificar una base que todavía no reconoce.
 */
export function MigrationsPendingPage({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="pf-centered">
      <span className="pf-centered__brand">SILLAR</span>

      <div className="pf-centered__panel">
        <Card title="Base de datos no preparada">
          <div className="pf-form">
            <Alert
              tone="warning"
              title="La instalación todavía no puede empezar"
            >
              No se encuentra la tabla de instalación en la base configurada.
            </Alert>

            <p>
              Comprueba que la conexión apunte a la base correcta y que las
              migraciones del despliegue estén aplicadas.
            </p>

            <Button onClick={onRetry}>Comprobar de nuevo</Button>
          </div>
        </Card>
      </div>
    </div>
  );
}

/**
 * Asistente de instalación.
 *
 * Una pantalla, tres bloques. Al terminar **no inicia sesión**: espera a que
 * `/api/setup/status` confirme el modo normal antes de abrir el acceso, porque
 * instalación y sesión son dos flujos distintos.
 */
export function SetupPage() {

  const [businessName, setBusinessName] = useState('');
  const [licenseType, setLicenseType] = useState<string>('trial');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  // Título y mensaje por separado: el mensaje sigue siendo `error`, y se pinta
  // dentro de `pf-server-message` para conservar los saltos del detalle (H-01).
  const [errorTitle, setErrorTitle] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [checkingReady, setCheckingReady] = useState(false);
  const [restartNotice, setRestartNotice] = useState<string | null>(null);

  const checks = requirements(password, email, fullName);
  const score = strength(password);

  async function enterWhenReady() {
    setRestartNotice(null);
    setCheckingReady(true);

    try {
      // En el acto, sin esperar al calendario del sondeo: si el servidor ya
      // volvió, la pregunta tiene que poder salir ahora (H08).
      await connection.probeNow();

      const status = await http.get<SetupStatus>('/setup/status', {
        allowUnauthorized: true,
      });

      if (status.setupRequired) {
        setRestartNotice(
          status.migrationsPending
            ? 'La base todavía no está preparada para abrir el acceso.'
            : 'El servidor todavía está terminando la instalación. Espera unos segundos y vuelve a intentarlo.',
        );
        return;
      }

      // Recarga deliberada: App reconstruye capacidades, sesión y
      // configuración desde el host que ya arrancó en modo normal.
      window.location.replace('/login');
    } catch (caught) {
      setRestartNotice(
        isApiError(caught, 'Network')
          ? 'El servidor se está reiniciando. Espera unos segundos y vuelve a intentarlo.'
          : isApiError(caught)
            ? caught.displayMessage
            : 'Todavía no se pudo confirmar que el servidor esté listo.',
      );
    } finally {
      setCheckingReady(false);
    }
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrorTitle(null);
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
        // El host pudo cambiar a modo normal entre esta pantalla y el POST.
        // Se conserva el flujo de instalación y se verifica antes de acceder.
        setDone(true);
        return;
      }

      // **Según lo que se sabe del fallo, no con un título fijo** (H03). Un 503
      // explicado por el instalador, un 5xx sin explicación y un corte de red
      // son situaciones distintas, y antes las tres salían bajo «No se pudo
      // instalar». Ver `fallos.ts`.
      const fallo = describirFalloDeInstalacion(caught);
      setErrorTitle(fallo.titulo);
      setError(fallo.mensaje);
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
              {restartNotice && <Alert tone="warning">{restartNotice}</Alert>}

            <Button
              onClick={() => void enterWhenReady()}
              loading={checkingReady}
            >
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
            {/* `pf-server-message`: el 503 de la instalación trae en su detalle una
                explicación en varias líneas —dos causas posibles y un comando—, y
                sin conservar los saltos se leía como un solo párrafo apelmazado. */}
            {errorTitle && (
              <Alert tone="danger" title={errorTitle}>
                {error && <span className="pf-server-message">{error}</span>}
              </Alert>
            )}

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
