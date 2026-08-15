import { useCallback, useMemo, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Badge, Button, Card, EmptyState, Field, Input } from '../../../shared/ui';
import { Table, type Column } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import {
  AUDIT_ACTIONS,
  actionLabel,
  auditService,
  type AuditEntry,
  type AuditQuery,
} from '../services/audit';

/**
 * Registro de auditoría.
 *
 * Tabla de lectura y nada más. **No ofrece ninguna acción**: ni editar, ni
 * borrar, ni exportar. La regla 15 del SPEC dice que no se edita ni se borra
 * desde el API, y una pantalla con un botón que insinúe lo contrario invita a
 * pedirlo.
 */
export function AuditPage() {
  const [filters, setFilters] = useState<AuditQuery>({});
  const [page, setPage] = useState(1);

  const query = useMemo<AuditQuery>(() => ({ ...filters, page }), [filters, page]);
  const load = useCallback(() => auditService.query(query), [query]);
  const { state } = useResource(load, 'cargar la auditoría');

  function apply(change: Partial<AuditQuery>) {
    setFilters((current) => ({ ...current, ...change }));
    setPage(1);
  }

  const columns: Column<AuditEntry>[] = [
    {
      key: 'when',
      header: 'Cuándo',
      render: (entry) => (
        <span style={{ whiteSpace: 'nowrap' }}>
          {new Date(entry.occurredAt).toLocaleString('es-PE', {
            dateStyle: 'short',
            timeStyle: 'medium',
          })}
        </span>
      ),
    },
    {
      key: 'who',
      header: 'Quién',
      render: (entry) => <Actor entry={entry} />,
    },
    {
      key: 'module',
      header: 'Módulo',
      render: (entry) => entry.moduleCode ?? <span style={subtle}>—</span>,
    },
    {
      key: 'action',
      header: 'Acción',
      render: (entry) => <Badge tone={toneOf(entry.action)}>{actionLabel(entry.action)}</Badge>,
    },
    {
      key: 'entity',
      header: 'Entidad',
      render: (entry) =>
        entry.entityType ? (
          <span style={{ fontSize: '12.5px' }}>
            {entry.entityType}
            {entry.entityId && <span style={subtle}> · {entry.entityId}</span>}
          </span>
        ) : (
          <span style={subtle}>—</span>
        ),
    },
    {
      key: 'summary',
      header: 'Resumen',
      render: (entry) => entry.summary ?? <span style={subtle}>—</span>,
    },
  ];

  if (state.status === 'forbidden') {
    return <ForbiddenPage minimum="super_admin" />;
  }

  const result = state.status === 'ready' ? state.data : null;

  return (
    <PageContainer
      title="Auditoría"
      description="Todo lo que se ha hecho en el panel, de lo más reciente a lo más antiguo. Solo se consulta: no se edita ni se borra."
    >
      <Card title="Filtros">
        <div style={filterGrid}>
          <Field label="Desde">
            {(props) => (
              <Input
                {...props}
                type="date"
                value={filters.from?.slice(0, 10) ?? ''}
                onChange={(event) =>
                  apply({ from: event.target.value ? `${event.target.value}T00:00:00Z` : undefined })
                }
              />
            )}
          </Field>

          <Field label="Hasta">
            {(props) => (
              <Input
                {...props}
                type="date"
                value={filters.to?.slice(0, 10) ?? ''}
                onChange={(event) =>
                  apply({ to: event.target.value ? `${event.target.value}T23:59:59Z` : undefined })
                }
              />
            )}
          </Field>

          <Field label="Usuario" hint="Su identificador numérico.">
            {(props) => (
              <Input
                {...props}
                type="number"
                value={filters.adminUserId ?? ''}
                onChange={(event) =>
                  apply({ adminUserId: event.target.value ? Number(event.target.value) : undefined })
                }
              />
            )}
          </Field>

          <Field label="Módulo">
            {(props) => (
              <Input
                {...props}
                value={filters.moduleCode ?? ''}
                placeholder="core, catalog…"
                onChange={(event) => apply({ moduleCode: event.target.value || undefined })}
              />
            )}
          </Field>

          <Field label="Acción">
            {(props) => (
              <select
                {...props}
                className="ui-input"
                value={filters.action ?? ''}
                onChange={(event) => apply({ action: event.target.value || undefined })}
              >
                <option value="">Todas</option>
                {AUDIT_ACTIONS.map((action) => (
                  <option key={action.value} value={action.value}>
                    {action.label}
                  </option>
                ))}
              </select>
            )}
          </Field>

          <div style={{ display: 'flex', alignItems: 'flex-end' }}>
            <Button
              variant="secondary"
              onClick={() => {
                setFilters({});
                setPage(1);
              }}
            >
              Limpiar filtros
            </Button>
          </div>
        </div>
      </Card>

      {state.status === 'error' && <Alert tone="danger">{state.failure.message}</Alert>}

      <Table
        columns={columns}
        rows={result?.items ?? []}
        rowKey={(entry) => entry.id}
        loading={state.status === 'loading'}
        empty={
          <EmptyState
            title="No hay registros con esos filtros"
            description="Prueba a ampliar el rango de fechas o a quitar alguno."
          />
        }
        pagination={
          result
            ? {
                page: result.page,
                totalPages: result.totalPages,
                totalItems: result.totalItems,
                onChange: setPage,
              }
            : undefined
        }
      />
    </PageContainer>
  );
}

/**
 * Quién actuó.
 *
 * Sin identificador pero con correo hay **dos situaciones distintas**, y
 * confundirlas sería mentir:
 *
 * - En un acceso fallido, el correo es sencillamente lo que alguien escribió.
 *   Puede no haber existido nunca. No se marca nada.
 * - En cualquier otra acción, para haberla hecho la cuenta tuvo que existir, así
 *   que si ya no está es que la eliminaron. Ahí sí se marca.
 *
 * Que el correo sobreviva es exactamente la razón por la que se guarda como
 * snapshot: sin él, borrar a alguien borraría la huella de lo que hizo.
 */
function Actor({ entry }: { entry: AuditEntry }) {
  if (!entry.adminUserEmail) {
    return <span style={subtle}>El sistema</span>;
  }

  const deleted = entry.adminUserId === null && entry.action !== 'login_failed';

  return (
    <span style={{ whiteSpace: 'nowrap' }}>
      {entry.adminUserEmail}
      {deleted && (
        <>
          {' '}
          <Badge tone="neutral">cuenta eliminada</Badge>
        </>
      )}
    </span>
  );
}

function toneOf(action: string): 'neutral' | 'success' | 'warning' | 'danger' {
  switch (action) {
    case 'login_failed':
      return 'danger';
    case 'delete':
    case 'deactivate':
      return 'warning';
    case 'create':
    case 'activate':
    case 'setup':
      return 'success';
    default:
      return 'neutral';
  }
}

const subtle = { color: 'var(--text-subtle)' };

const filterGrid = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
  gap: 'var(--s4)',
};
