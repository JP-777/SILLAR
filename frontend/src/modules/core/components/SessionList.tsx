import { useCallback, useState } from 'react';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, Card, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, type Column } from '../../../shared/ui/patterns';
import { usersService, type AdminSession } from '../services/users';

/**
 * Sesiones abiertas, con la posibilidad de cerrar una.
 *
 * Es lo que hace útil que las sesiones vivan en base de datos en vez de en un
 * token autocontenido, y lo que un negocio con personal rotando agradece: ver
 * quién tiene el panel abierto y poder cerrarlo.
 */
export function SessionList({ onRevoked }: { onRevoked: (message: string) => void }) {
  const load = useCallback(() => usersService.listSessions(), []);
  const { state, reload } = useResource(load, 'cargar las sesiones');

  const [pending, setPending] = useState<AdminSession | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  const sessions = state.status === 'ready' ? state.data : [];

  async function revoke() {
    if (!pending) {
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      await usersService.revokeSession(pending.id);
      onRevoked(`Se cerró la sesión de ${pending.email}.`);
      setPending(null);
      await reload();
    } catch (error) {
      setFailure(describe(error, 'cerrar la sesión'));
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  const columns: Column<AdminSession>[] = [
    { key: 'email', header: 'Usuario', render: (session) => session.email },
    {
      key: 'device',
      header: 'Dispositivo',
      render: (session) => (
        <span title={session.userAgent ?? ''}>{describeDevice(session.userAgent)}</span>
      ),
    },
    {
      key: 'ip',
      header: 'Dirección',
      render: (session) => session.ipAddress ?? <span style={subtle}>Desconocida</span>,
    },
    {
      key: 'lastSeen',
      header: 'Último uso',
      render: (session) =>
        new Date(session.lastSeenAt).toLocaleString('es-PE', {
          dateStyle: 'short',
          timeStyle: 'short',
        }),
    },
    {
      key: 'status',
      header: 'Estado',
      render: (session) =>
        session.revokedAt ? (
          <Badge tone="neutral">Cerrada</Badge>
        ) : (
          <Badge tone="success">Abierta</Badge>
        ),
    },
    {
      key: 'actions',
      header: 'Acciones',
      align: 'right',
      render: (session) =>
        session.revokedAt ? null : (
          <Button size="sm" variant="ghost" onClick={() => setPending(session)}>
            Cerrar sesión
          </Button>
        ),
    },
  ];

  return (
    <Card title="Sesiones" subtitle="Quién tiene el panel abierto ahora mismo.">
      <FailureAlert failure={failure} />

      <Table
        columns={columns}
        rows={sessions}
        rowKey={(session) => session.id}
        dimmed={(session) => session.revokedAt !== null}
        loading={state.status === 'loading'}
        empty={<EmptyState title="No hay sesiones registradas" />}
      />

      <ConfirmDialog
        open={pending !== null}
        title="Cerrar esta sesión"
        confirmLabel="Cerrar sesión"
        danger
        busy={busy}
        onConfirm={() => void revoke()}
        onCancel={() => setPending(null)}
      >
        <p>
          <strong>{pending?.email}</strong> tendrá que volver a entrar en ese dispositivo.
        </p>
        <p>Si es tu propia sesión, se cerrará también aquí.</p>
      </ConfirmDialog>
    </Card>
  );
}

const subtle = { color: 'var(--text-subtle)' };

/**
 * Resume el navegador declarado.
 *
 * Aproximado a propósito: el `User-Agent` no es fiable y solo sirve para que
 * alguien reconozca cuál de sus sesiones es cuál.
 */
function describeDevice(userAgent: string | null): string {
  if (!userAgent) {
    return 'Desconocido';
  }

  const browser = /Edg\//.test(userAgent)
    ? 'Edge'
    : /Chrome\//.test(userAgent)
      ? 'Chrome'
      : /Firefox\//.test(userAgent)
        ? 'Firefox'
        : /Safari\//.test(userAgent)
          ? 'Safari'
          : 'Otro navegador';

  const system = /Windows/.test(userAgent)
    ? 'Windows'
    : /Android/.test(userAgent)
      ? 'Android'
      : /iPhone|iPad/.test(userAgent)
        ? 'iOS'
        : /Mac OS/.test(userAgent)
          ? 'macOS'
          : /Linux/.test(userAgent)
            ? 'Linux'
            : '';

  return system ? `${browser} · ${system}` : browser;
}
