import { useState, type FormEvent } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { describe, type Failure } from '../../../shared/errors/messages';
import { Alert, Button, Card, Field, Input } from '../../../shared/ui';
import { FailureAlert } from '../../../shared/ui/patterns';
import { MIN_LENGTH, requirements } from '../../../platform/password';
import { useSession } from '../../../session';
import { accountService } from '../services/users';

/**
 * Cambiar la contraseña propia.
 *
 * Vive fuera de la administración de usuarios porque es una acción sobre uno
 * mismo: cualquiera con sesión puede hacerlo, incluido un `editor`.
 */
export function ChangePasswordPage() {
  const { user } = useSession();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [done, setDone] = useState(false);

  const checks = requirements(newPassword, user?.email ?? '', user?.fullName ?? '');

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);

    try {
      await accountService.changePassword(currentPassword, newPassword);
      setDone(true);
      setCurrentPassword('');
      setNewPassword('');
    } catch (error) {
      setFailure(describe(error, 'cambiar la contraseña'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <PageContainer title="Cambiar mi contraseña">
      <div style={{ maxWidth: '480px' }}>
        <Card>
          <form onSubmit={submit} noValidate style={formStyle}>
            {done && (
              <Alert tone="success" title="Contraseña cambiada">
                Se cerraron tus demás sesiones. Esta sigue abierta.
              </Alert>
            )}

            <FailureAlert failure={failure} />

            {/* Se avisa ANTES de confirmar. Descubrir que se cerraron las demás
                sesiones después es una sorpresa; saberlo antes es el sentido de
                la operación. */}
            <Alert tone="info">
              Al cambiarla se <strong>cerrarán tus sesiones abiertas en otros dispositivos</strong>.
              Esta se mantiene.
            </Alert>

            <Field label="Contraseña actual" required>
              {(props) => (
                <Input
                  {...props}
                  type="password"
                  value={currentPassword}
                  onChange={(event) => setCurrentPassword(event.target.value)}
                  autoComplete="current-password"
                />
              )}
            </Field>

            <Field
              label="Contraseña nueva"
              required
              hint={
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
                <Input
                  {...props}
                  type="password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  minLength={MIN_LENGTH}
                  autoComplete="new-password"
                />
              )}
            </Field>

            <Button type="submit" loading={busy}>
              Cambiar contraseña
            </Button>
          </form>
        </Card>
      </div>
    </PageContainer>
  );
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
