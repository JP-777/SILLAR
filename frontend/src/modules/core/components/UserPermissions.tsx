import { useCallback, useState } from 'react';
import { Button, Field } from '../../../shared/ui';
import { Drawer, FailureAlert } from '../../../shared/ui/patterns';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { roleLabel } from '../../../session/roleVocabulary';
import { modulePermissionsService, type ModulePermission } from '../services/modulePermissions';
import type { AdminUser } from '../services/users';

export function UserPermissions({ user, onClose }: { user: AdminUser; onClose: () => void }) {
  const load = useCallback(() => modulePermissionsService.list(user.id), [user.id]);
  const { state, reload } = useResource(load, 'cargar los permisos');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [message, setMessage] = useState('');
  async function save(row: ModulePermission, value: string) {
    setBusy(true); setFailure(null); setMessage('');
    try {
      await modulePermissionsService.set(user.id, row.moduleCode, row.operationCode, value === 'inherit' ? null : value === 'allow');
      await reload(); window.dispatchEvent(new Event('sillar:permissions-changed'));
      setMessage(`Permiso de ${row.operationName.toLocaleLowerCase('es-PE')} guardado.`);
    } catch (error) { setFailure(describe(error, 'guardar el permiso')); }
    finally { setBusy(false); }
  }
  return <Drawer open title={`Permisos de ${user.fullName}`} onClose={() => { if (!busy) onClose(); }}
    description="Cada cambio se guarda y registra. Un permiso nunca supera las atribuciones del rol ni habilita un módulo apagado o sin licencia."
    footer={<Button variant="secondary" disabled={busy} onClick={onClose}>Cerrar</Button>}>
    <FailureAlert failure={failure} />
    {state.status === 'loading' && <p role="status">Cargando permisos…</p>}
    {state.status !== 'loading' && state.status !== 'ready' && <div role="alert"><p>No se pudieron cargar los permisos.</p><Button onClick={() => void reload()}>Reintentar</Button></div>}
    {state.status === 'ready' && state.data.length === 0 && <p>No hay operaciones configurables en esta instalación.</p>}
    {state.status === 'ready' && state.data.map(row => <Field key={`${row.moduleCode}:${row.operationCode}`}
      label={`${row.moduleName} · ${row.operationName}`}
      hint={`Rol mínimo: ${roleLabel(row.minimumRole)}. Con el rol y permiso actuales: ${row.effective ? 'permitido' : 'denegado'}.`}>
      {props => <select {...props} className="ui-input" disabled={busy} value={row.allowed === null ? 'inherit' : row.allowed ? 'allow' : 'deny'} onChange={event => void save(row, event.target.value)}>
        <option value="inherit">Heredar del rol</option><option value="allow">Permitir dentro del rol</option><option value="deny">Denegar</option>
      </select>}
    </Field>)}
    <p role="status">{message}</p>
  </Drawer>;
}
