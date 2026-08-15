import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { useSession, type Role } from '../../../session';
import { UserForm } from '../components/UserForm';
import { SessionList } from '../components/SessionList';
import { usersService, type AdminUser } from '../services/users';

const ROLE_LABELS: Record<Role, string> = {
  super_admin: 'Principal',
  admin: 'Administrador',
  editor: 'Editor',
};

/** Administración de usuarios. Solo `super_admin`. */
export function UsersPage() {
  const load = useCallback(() => usersService.list(), []);
  const { state, reload } = useResource(load, 'cargar los usuarios');
  const { user: me } = useSession();
  const { toasts, show } = useToasts();

  const [editing, setEditing] = useState<AdminUser | null>(null);
  const [creating, setCreating] = useState(false);
  const [pendingDeactivation, setPendingDeactivation] = useState<AdminUser | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  const users = state.status === 'ready' ? state.data : [];

  async function deactivate() {
    if (!pendingDeactivation) {
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      await usersService.deactivate(pendingDeactivation.id);
      show(`${pendingDeactivation.fullName} ya no puede entrar y se cerraron sus sesiones.`);
      setPendingDeactivation(null);
      await reload();
    } catch (error) {
      // Los tres rechazos —último principal, uno mismo, sin permiso— llegan como
      // 409 con la frase ya redactada por el backend.
      setFailure(describe(error, 'desactivar el usuario'));
      setPendingDeactivation(null);
    } finally {
      setBusy(false);
    }
  }

  const columns: Column<AdminUser>[] = [
    {
      key: 'name',
      header: 'Nombre',
      render: (user) => (
        <>
          <div style={{ fontWeight: 560 }}>{user.fullName}</div>
          <div style={{ fontSize: '12.5px', color: 'var(--text-subtle)' }}>{user.email}</div>
        </>
      ),
    },
    {
      key: 'role',
      header: 'Rol',
      render: (user) => <Badge tone="neutral">{ROLE_LABELS[user.role]}</Badge>,
    },
    {
      key: 'status',
      header: 'Estado',
      render: (user) =>
        user.isActive ? (
          <Badge tone="success">Activo</Badge>
        ) : (
          <Badge tone="neutral">Desactivado</Badge>
        ),
    },
    {
      key: 'lastLogin',
      header: 'Último acceso',
      render: (user) =>
        user.lastLoginAt ? (
          new Date(user.lastLoginAt).toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' })
        ) : (
          <span style={{ color: 'var(--text-subtle)' }}>Nunca</span>
        ),
    },
    {
      key: 'actions',
      header: 'Acciones',
      align: 'right',
      render: (user) => (
        <div style={{ display: 'inline-flex', gap: 'var(--s2)' }}>
          <Button size="sm" variant="secondary" onClick={() => setEditing(user)}>
            Editar
          </Button>
          {user.isActive && user.id !== me?.id && (
            <Button size="sm" variant="ghost" onClick={() => setPendingDeactivation(user)}>
              Desactivar
            </Button>
          )}
        </div>
      ),
    },
  ];

  if (state.status === 'forbidden') {
    return <ForbiddenPage minimum="super_admin" />;
  }

  return (
    <PageContainer
      title="Usuarios"
      description="Quién puede entrar al panel y con qué permisos."
      actions={<Button onClick={() => setCreating(true)}>Nuevo administrador</Button>}
    >
      <FailureAlert failure={failure} />

      <Table
        columns={columns}
        rows={users}
        rowKey={(user) => user.id}
        // Atenuados, no ocultos: es baja lógica, y desaparecerlos haría creer
        // que se borraron.
        dimmed={(user) => !user.isActive}
        loading={state.status === 'loading'}
        empty={
          <EmptyState
            title="No hay administradores"
            description="Algo va mal: debería existir al menos el que creó la instalación."
          />
        }
      />

      <SessionList onRevoked={show} />

      {(creating || editing) && (
        <UserForm
          open
          // La clave fuerza un formulario limpio al cambiar de usuario.
          key={editing?.id ?? 'nuevo'}
          user={editing}
          currentUserId={me?.id ?? 0}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSaved={(_, message) => {
            setCreating(false);
            setEditing(null);
            show(message);
            void reload();
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDeactivation !== null}
        title={`Desactivar ${pendingDeactivation?.fullName ?? ''}`}
        confirmLabel="Desactivar usuario"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setPendingDeactivation(null)}
      >
        <p>
          <strong>Se cerrarán sus sesiones abiertas</strong> y dejará de poder entrar al panel.
        </p>
        <p>La cuenta no se borra: sus registros de auditoría se conservan y puedes reactivarla.</p>
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}
