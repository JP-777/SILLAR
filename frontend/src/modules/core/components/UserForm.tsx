import { useState, type FormEvent } from 'react';
import { Button, Field, Input, Switch } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { describe, type Failure } from '../../../shared/errors/messages';
import { MIN_LENGTH, requirements } from '../../../platform/password';
import { ROLES, type Role } from '../../../session';
import { usersService, type AdminUser } from '../services/users';

const ROLE_LABELS: Record<Role, string> = {
  editor: 'Editor — edita contenido y sube archivos',
  admin: 'Administrador — configura el negocio',
  super_admin: 'Administrador principal — gestiona usuarios y módulos',
};

interface UserFormProps {
  open: boolean;
  /** Usuario a editar, o `null` para crear uno nuevo. */
  user: AdminUser | null;
  /** Identificador de quien está usando el panel, para las reglas sobre uno mismo. */
  currentUserId: number;
  onClose: () => void;
  onSaved: (user: AdminUser, message: string) => void;
}

/**
 * Alta y edición de un administrador, en panel lateral.
 *
 * La validación es **al enviar**, no en cada tecla: corregir a alguien mientras
 * escribe su correo es molestarle antes de que haya terminado.
 */
export function UserForm({ open, user, currentUserId, onClose, onSaved }: UserFormProps) {
  const editing = user !== null;
  const isSelf = editing && user.id === currentUserId;

  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [email, setEmail] = useState(user?.email ?? '');
  const [phone, setPhone] = useState(user?.phone ?? '');
  const [role, setRole] = useState<Role>(user?.role ?? 'editor');
  const [isActive, setIsActive] = useState(user?.isActive ?? true);
  const [password, setPassword] = useState('');

  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const roleChanged = editing && role !== user.role;
  const checks = requirements(password, email, fullName);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailure(null);
    setFieldErrors({});

    try {
      const trimmedPhone = phone.trim() === '' ? null : phone.trim();

      const saved = editing
        ? await usersService.update(user.id, { fullName, role, phone: trimmedPhone, isActive })
        : await usersService.create({ fullName, email, password, role, phone: trimmedPhone });

      onSaved(
        saved,
        editing ? `Se guardaron los cambios de ${saved.fullName}.` : `${saved.fullName} ya puede entrar.`,
      );
    } catch (error) {
      const described = describe(error, editing ? 'guardar el usuario' : 'crear el usuario');
      setFailure(described);

      // Los errores de validación se sitúan en su campo cuando el servidor dice
      // cuál; el resto se muestra arriba.
      if (described.fieldErrors) {
        setFieldErrors(described.fieldErrors);
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? `Editar ${user.fullName}` : 'Nuevo administrador'}
      description={
        editing ? 'El correo no se puede cambiar: es el identificador de acceso.' : undefined
      }
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Cancelar
          </Button>
          <Button type="submit" form="formulario-usuario" loading={busy}>
            {editing ? 'Guardar cambios' : 'Crear administrador'}
          </Button>
        </>
      }
    >
      <form id="formulario-usuario" onSubmit={submit} noValidate style={formStyle}>
        <FailureAlert failure={failure?.kind === 'inline' ? failure : null} />

        <Field label="Nombre completo" required error={fieldErrors.usuario ?? null}>
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

        <Field
          label="Correo"
          required
          hint={editing ? undefined : 'Será su identificador para entrar.'}
        >
          {(props) => (
            <Input
              {...props}
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              maxLength={150}
              disabled={editing}
              autoComplete="email"
            />
          )}
        </Field>

        <Field label="Teléfono">
          {(props) => (
            <Input
              {...props}
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              maxLength={30}
              autoComplete="tel"
            />
          )}
        </Field>

        <Field
          label="Rol"
          required
          hint={
            isSelf
              ? 'No puedes cambiar tu propio rol.'
              : roleChanged
                ? 'Al guardar se cerrarán las sesiones abiertas de esta persona.'
                : undefined
          }
        >
          {(props) => (
            <select
              {...props}
              className="ui-input"
              value={role}
              disabled={isSelf}
              onChange={(event) => setRole(event.target.value as Role)}
            >
              {ROLES.map((value) => (
                <option key={value} value={value}>
                  {ROLE_LABELS[value]}
                </option>
              ))}
            </select>
          )}
        </Field>

        {!editing && (
          <Field
            label="Contraseña"
            required
            // Los requisitos, ANTES de escribir.
            hint={
              <ul className="pf-requirements">
                {checks.map((requirement) => (
                  <li key={requirement.text} data-met={requirement.met}>
                    {requirement.text}
                  </li>
                ))}
              </ul>
            }
            error={fieldErrors.usuario ?? null}
          >
            {(props) => (
              <Input
                {...props}
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                minLength={MIN_LENGTH}
                autoComplete="new-password"
              />
            )}
          </Field>
        )}

        {editing && !isSelf && (
          <Switch
            checked={isActive}
            onChange={setIsActive}
            label={isActive ? 'Puede entrar' : 'No puede entrar'}
          />
        )}
      </form>
    </Drawer>
  );
}

const formStyle = { display: 'flex', flexDirection: 'column' as const, gap: 'var(--s4)' };
