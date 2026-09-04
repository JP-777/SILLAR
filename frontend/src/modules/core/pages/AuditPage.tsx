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
import { usersService } from '../services/users';

/**
 * Cómo se llama en castellano cada tipo de entidad que se audita.
 *
 * **Vive aquí, del lado de la lectura, y eso tiene fecha de caducidad.** Lo
 * correcto a la larga es que la etiqueta viaje con la escritura, porque quien
 * sabe cómo se llama un `product_item` es M01 y no esta pantalla. Se hace así
 * hoy para no meter vocabulario de cuatro módulos en `Sillar.Core.Contracts`
 * por una columna; el día que el mapa deje de caber de un vistazo, se mueve.
 * Anotado con su disparador en `PENDIENTES.md`.
 *
 * Las palabras no son traducciones libres: son **las que el panel ya usa**.
 * `social_link` es «Red social» porque el menú dice «Redes sociales», y
 * `media_asset` es «Archivo» porque la pantalla se llama «Archivos». Inventar
 * un segundo vocabulario para la auditoría obligaría a traducir dos veces al
 * leerla.
 */
const ENTIDADES: Readonly<Record<string, string>> = {
  // CORE
  admin_session: 'Sesión',
  admin_user: 'Usuario',
  email: 'Correo',
  installation: 'Instalación',
  media_asset: 'Archivo',
  module: 'Módulo',
  setting: 'Ajuste',
  // M01 · Catálogo
  brand: 'Marca',
  category: 'Categoría',
  product: 'Producto',
  product_image: 'Imagen de producto',
  product_item: 'Presentación',
  // M02 · Contenido web
  banner: 'Banner',
  featured_product: 'Producto destacado',
  featured_project: 'Trabajo destacado',
  promotion: 'Promoción',
  social_link: 'Red social',
  // M04 · Clientes y contacto
  contact_message: 'Mensaje de contacto',
  customer: 'Cliente',
  customer_invitation: 'Invitación de cliente',
};

/**
 * La etiqueta de un tipo, **o el tipo tal cual si no lo conocemos**.
 *
 * El caso desconocido no dice «Desconocido» ni se inventa una traducción: un
 * módulo nuevo que empiece a auditar aparecerá aquí con su código técnico, que
 * es feo y es cierto. Una etiqueta inventada sería bonita y falsa, y nadie
 * vendría a añadir la buena.
 */
function etiquetaDe(entityType: string): string {
  return ENTIDADES[entityType] ?? entityType;
}

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

  // **Los administradores, para poder filtrar por persona y no por número.**
  // Se piden una sola vez: `usersService.list` es estable, así que no entra en
  // el bucle de recargas del filtro. Vienen todos, también los dados de baja
  // (`AdminUserService.cs:41-46` no filtra por `IsActive`), y tiene que ser
  // así: lo que hizo alguien antes de que le dieran de baja **sigue en el
  // registro**, y es justo lo que se va a buscar.
  const { state: usuarios } = useResource(usersService.list, 'cargar los administradores');

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
      render: (entry) => <Entidad entry={entry} />,
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

          <Field label="Usuario" hint={usuarios.status === 'error' ? usuarios.failure.message : undefined}>
            {(props) => (
              <select
                {...props}
                className="ui-input"
                value={filters.adminUserId ?? ''}
                // Mientras la lista no esté, el desplegable solo ofrece
                // «Todos». **No se cae al campo numérico de antes**: un fallo
                // al cargar una ayuda no es motivo para volver a pedirle a
                // alguien que se sepa un identificador.
                disabled={usuarios.status !== 'ready'}
                onChange={(event) =>
                  apply({ adminUserId: event.target.value ? Number(event.target.value) : undefined })
                }
              >
                <option value="">Todos</option>
                {usuarios.status === 'ready' &&
                  usuarios.data.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.fullName} — {usuario.email}
                      {usuario.isActive ? '' : ' (inactivo)'}
                    </option>
                  ))}
              </select>
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
 * Qué se tocó, **y su identificador solo si alguien lo pide**.
 *
 * La columna enseñaba `entityType · entityId` en crudo, y desde la ADR-018 el
 * identificador de un medio es un `uuid`: la tabla acababa mostrando
 * `019fff83-a5d5-74b0-…` a la vista de cualquiera, contra la regla de que los
 * identificadores nunca se muestran al usuario.
 *
 * **No es una excepción a la regla, es la diferencia entre presentar y
 * responder.** Lo que la regla prohíbe es poner un identificador delante de
 * alguien que no lo pidió. Aquí no se presenta nada: se ofrece «Ver detalle»,
 * y quien está investigando una fila concreta lo despliega. El dato no se
 * acorta, no se transforma y no se pierde — sigue entero en la base, en la
 * entrada y en el API.
 *
 * `<details>` y no un botón propio: trae plegado/desplegado, foco y teclado
 * hechos por el navegador, y su contenido plegado **no está en el texto
 * renderizado**, así que la regla se cumple de verdad y no por ocultarlo con
 * CSS.
 */
function Entidad({ entry }: { entry: AuditEntry }) {
  if (!entry.entityType) {
    return <span style={subtle}>—</span>;
  }

  return (
    <div style={{ fontSize: '12.5px' }}>
      {etiquetaDe(entry.entityType)}
      {/* Sin identificador no hay detalle que ofrecer. Un desplegable que se
          abre para decir «—» promete algo que no tiene. */}
      {entry.entityId && (
        <details>
          <summary style={{ cursor: 'pointer' }}>Ver detalle</summary>
          <div style={{ marginTop: 'var(--s2)' }}>
            <div style={subtle}>Identificador</div>
            <code style={{ wordBreak: 'break-all' }}>{entry.entityId}</code>
          </div>
        </details>
      )}
    </div>
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
