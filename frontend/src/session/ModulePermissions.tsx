import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Outlet } from 'react-router-dom';
import { http } from '../shared/http/client';
import { Button } from '../shared/ui';

type State = { userId: number | undefined; status: 'loading' | 'ready' | 'error'; permissions: ReadonlySet<string> };
type Value = { status: State['status']; hasOperation: (module: string, operation: string) => boolean; refresh: () => void };
const Context = createContext<Value | null>(null);

/** Solo orienta la interfaz: cada operación vuelve a autorizar en el servidor. */
export function ModulePermissionsProvider({ userId, children }: { userId: number | undefined; children: ReactNode }) {
  const [revision, setRevision] = useState(0);
  const [state, setState] = useState<State>({ userId, status: 'loading', permissions: new Set() });
  const refresh = useCallback(() => setRevision(v => v + 1), []);
  useEffect(() => {
    const controller = new AbortController();
    if (userId === undefined) {
      setState({ userId, status: 'ready', permissions: new Set() });
      return () => controller.abort();
    }
    setState({ userId, status: 'loading', permissions: new Set() });
    void http.get<unknown>('/admin/module-permissions', { signal: controller.signal }).then(data => {
      if (!Array.isArray(data)) throw new Error('Respuesta de permisos no válida.');
      const permissions = new Set<string>();
      for (const row of data) {
        if (!row || typeof row.moduleCode !== 'string' || !Array.isArray(row.operations)
          || row.operations.some((op: unknown) => typeof op !== 'string')) throw new Error('Respuesta de permisos no válida.');
        for (const op of row.operations) permissions.add(`${row.moduleCode}:${op}`);
      }
      if (!controller.signal.aborted) setState({ userId, status: 'ready', permissions });
    }).catch(() => { if (!controller.signal.aborted) setState({ userId, status: 'error', permissions: new Set() }); });
    return () => controller.abort();
  }, [userId, revision]);
  useEffect(() => {
    window.addEventListener('focus', refresh);
    window.addEventListener('sillar:permissions-changed', refresh);
    return () => { window.removeEventListener('focus', refresh); window.removeEventListener('sillar:permissions-changed', refresh); };
  }, [refresh]);
  const value = useMemo<Value>(() => ({
    status: state.userId === userId ? state.status : 'loading', refresh,
    hasOperation: (module, operation) => state.userId === userId && state.status === 'ready' && state.permissions.has(`${module}:${operation}`),
  }), [state, userId, refresh]);
  return <Context value={value}>{children}</Context>;
}
export function useModulePermissions(): Value {
  const value = useContext(Context);
  if (!value) throw new Error('Falta el proveedor de permisos del panel.');
  return value;
}
export function RequireModuleOperation({ module, operation }: { module: string; operation: string }) {
  const { status, hasOperation, refresh } = useModulePermissions();
  if (status === 'loading') return <p role="status">Comprobando permisos…</p>;
  if (status === 'error') return <div role="alert"><p>No se pudieron comprobar tus permisos.</p><Button onClick={refresh}>Reintentar</Button></div>;
  if (!hasOperation(module, operation)) return <p role="alert">Tu cuenta no tiene permiso para esta operación.</p>;
  return <Outlet />;
}
