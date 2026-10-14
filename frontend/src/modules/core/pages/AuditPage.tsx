import { useCallback, useMemo, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { useCapability } from '../../../capabilities/useCapability';
import { moduleDisplayName } from '../../../capabilities/moduleDisplayName';
import {
  auditEntityLabel,
  visibleAuditEntityLabels,
  type AuditEntityLabels,
} from '../../../platform/auditEntityVocabularies';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Badge, Button, Card, EmptyState, Field } from '../../../shared/ui';
import { DateFilterField } from '../../../shared/ui/DateFilterField';
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
import { modulesService } from '../services/modules';

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
  const entityLabels = visibleAuditEntityLabels(useCapability().has);
  const moduleCatalog = useResource(
    modulesService.list,
    'cargar los nombres de los módulos',
  );
  const moduleDisplayModules =
    moduleCatalog.state.status === 'ready'
      ? moduleCatalog.state.data
      : [];

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
      render: (entry) => <Entidad entry={entry} labels={entityLabels} />,
    },
    {
      key: 'summary',
      header: 'Resumen',
      render: (entry) => <Resumen entry={entry} labels={entityLabels} />,
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
          <DateFilterField
          label="Desde"
          value={filters.from}
          boundary="start"
          onChange={(from) => apply({ from })}
        />

          <DateFilterField
          label="Hasta"
          value={filters.to}
          boundary="end"
          onChange={(to) => apply({ to })}
        />

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
            <select
              {...props}
              className="ui-input"
              value={filters.moduleCode ?? ''}
              onChange={(event) =>
                apply({ moduleCode: event.target.value || undefined })
              }
            >
              <option value="">Todos</option>
              {moduleDisplayModules.map((item) => {
                  const label = moduleDisplayName(moduleDisplayModules, item.code);

                  return label ? (
                    <option key={item.code} value={item.code}>
                      {label}
                    </option>
                  ) : null;
                })}
            </select>
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
function Entidad({
  entry,
  labels,
}: {
  entry: AuditEntry;
  labels: AuditEntityLabels;
}) {
  if (!entry.entityType) {
    return <span style={subtle}>—</span>;
  }

  return (
    <div style={{ fontSize: '12.5px' }}>
      {auditEntityLabel(entry.entityType, labels)}
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
 * Presenta un evento de correo sin poner el destinatario delante del lector.
 *
 * **El registro original no se toca.** `email_send` conserva su `summary`
 * íntegro en `core.audit_log` y en el API, y esta celda lo ofrece completo bajo
 * «Ver texto original».
 *
 * **Lo que cambia es de dónde sale el texto visible: de aquí, no del dato.**
 * Ni la clase ni el desenlace se copian del resumen. La clase se busca en
 * `CLASES_DE_CORREO`; el desenlace, en `MOTIVOS_DE_CORREO`. Lo que no esté en
 * una de las dos listas no se recorta ni se resume — no sale.
 *
 * **Por qué no basta con limpiar el resumen.** `SmtpEmailSender` lo compone con
 * el destinatario dentro (`backend/Sillar.Core/Email/SmtpEmailSender.cs:101` y
 * `:128`) y, cuando falla, detrás va `exception.Message` (`:107`): texto libre
 * de un servidor ajeno. Cualquier regla que recorte ese texto acaba enseñando
 * el trozo que no previó. Componer desde listas cerradas no tiene ese borde.
 */
function Resumen({
  entry,
  labels,
}: {
  entry: AuditEntry;
  labels: AuditEntityLabels;
}) {
  if (!entry.summary) {
    return <span style={subtle}>—</span>;
  }

  if (entry.action !== 'email_send') {
    return entry.summary;
  }

  return (
    <div style={{ fontSize: '12.5px' }}>
      <div>{resumenVisibleDeCorreo(entry, labels)}</div>
      <details>
        <summary style={{ cursor: 'pointer' }}>Ver texto original</summary>
        <div style={{ marginTop: 'var(--s2)' }}>
          <div style={subtle}>Texto original</div>
          <div style={{ wordBreak: 'break-word' }}>{entry.summary}</div>
        </div>
      </details>
    </div>
  );
}

/**
 * Las clases de correo que el producto emite, y cómo se llaman en pantalla.
 *
 * **`OutgoingEmail.Kind` es un `string` sin más**
 * (`backend/Sillar.Core.Contracts/Email/IEmailSender.cs:8`): ni enumeración, ni
 * `CHECK`, ni constantes compartidas. Cualquier módulo puede pasar cualquier
 * cosa, y eso llega a `entity_id` tal cual (`SmtpEmailSender.cs:99` y `:126`).
 * Así que **el valor no se muestra: se busca**. Una clase que no esté aquí no
 * aparece en la celda, y la fila se presenta sin ella.
 *
 * Las cuatro de hoy, con su origen: `smtp_test`
 * (`Sillar.Core/Endpoints/SettingsEndpoints.cs:150`), y `email_verification`,
 * `password_reset` e `invitation`
 * (`Sillar.Modules.Crm/Authentication/CustomerEmailComposer.cs:25`, `:48`, `:70`).
 *
 * De paso deja de verse un identificador en inglés donde debía ir español.
 */
const CLASES_DE_CORREO = new Map<string, string>([
  ['smtp_test', 'Correo de prueba'],
  ['email_verification', 'Verificación de correo'],
  ['password_reset', 'Restablecer contraseña'],
  ['invitation', 'Invitación de cuenta'],
]);

/**
 * Cómo se nombra una fila cuya clase de correo no está en la lista.
 *
 * **No lleva una etiqueta escrita aquí.** La fila ya trae su `entityType`, y la
 * palabra con la que se nombra en español es propiedad del vocabulario de
 * auditoría —el tipo del correo lo aporta CORE, que está siempre activo—, así
 * que esta celda la pide igual que la columna «Entidad». Escribirla a mano era
 * recentralizar vocabulario, y la puerta lo paró: `test:audit-vocabulary`
 * cuenta los literales de etiqueta que este archivo puede contener, y para
 * «Correo» el cupo es cero.
 *
 * **Y degrada como degrada la plataforma**, no de otra manera: si un tipo no
 * está registrado, `auditEntityLabel` devuelve su código técnico
 * (`frontend/tests/auditEntityVocabulary.test.mjs:122-133`). Inventar aquí una
 * degradación distinta sería tener dos comportamientos para lo mismo.
 */
function claseDesconocidaDe(entry: AuditEntry, labels: AuditEntityLabels): string {
  return entry.entityType ? auditEntityLabel(entry.entityType, labels) : 'Evento';
}

/**
 * Los motivos que esta pantalla sabe nombrar, y la frase con la que los nombra.
 *
 * **Son los cuatro que redacta SILLAR** antes de hablar con el servidor
 * (`backend/Sillar.Core/Email/SmtpEmailSender.cs:26-56`): los únicos que el
 * administrador puede arreglar, y los únicos cuyo texto está bajo control del
 * producto. Cualquier otro motivo viene de MailKit y no se nombra.
 *
 * Si alguien cambia la redacción en C# sin tocar esta lista, el motivo deja de
 * reconocerse y la celda cae en la frase genérica: **se pierde un diagnóstico,
 * no se expone un correo.** La barrera falla cerrada.
 */
const MOTIVOS_DE_CORREO: readonly { readonly marca: string; readonly frase: string }[] = [
  { marca: 'Falta configurar smtp_server.', frase: 'falta configurar el servidor SMTP.' },
  { marca: 'smtp_port no contiene un puerto válido.', frase: 'el puerto SMTP no es válido.' },
  { marca: 'Falta configurar smtp_from.', frase: 'falta configurar el remitente.' },
  { marca: 'Falta la variable de entorno', frase: 'falta la contraseña SMTP en el entorno.' },
];

/**
 * Qué se dice cuando el motivo no es ninguno de los cuatro.
 *
 * Dice además dónde está el texto de verdad, que es el sitio al que se manda a
 * quien necesite leerlo: el detalle desplegable de esta misma fila.
 */
const MOTIVO_NO_RECONOCIDO = 'falló — abre el detalle para ver qué dijo el servidor.';

/** Y cuando el resumen no tiene la forma que escribe `SmtpEmailSender`. */
const RESUMEN_IRREGULAR = 'Evento de correo.';

function resumenVisibleDeCorreo(entry: AuditEntry, labels: AuditEntityLabels): string {
  const desenlace = desenlaceDeCorreo(entry.summary ?? '');

  if (desenlace === null) {
    return RESUMEN_IRREGULAR;
  }

  const claseDesconocida = claseDesconocidaDe(entry, labels);
  const clase = CLASES_DE_CORREO.get(entry.entityId?.trim() ?? '') ?? claseDesconocida;
  const visible = `${clase}: ${desenlace}`;

  // **Barrera final, y no mira cómo se clasificó.** Con la composición de
  // arriba no puede dispararse: todo lo que entra en `visible` sale de las dos
  // listas de este archivo, y ninguna lleva arroba. Se queda porque el día que
  // alguien vuelva a interpolar un dato aquí, esto es lo que lo detiene — y
  // porque el sabotaje lo demuestra: con la clasificación rota, fue el guardia
  // el que impidió la fuga.
  return visible.includes('@')
    ? `${claseDesconocida}: ${MOTIVO_NO_RECONOCIDO}`
    : visible;
}

/**
 * El desenlace, o `null` si el resumen no tiene la forma que escribe el envío.
 *
 * **Se reconoce la estructura completa, no un trozo del final.** La forma es
 * `Correo '<clase>' a '<destinatario>': <desenlace>`, y el desenlace empieza
 * tras el primer `': ` que sigue al destinatario. Un envío logrado es el que
 * tiene ahí `enviado.` **y nada más detrás**: así, un error cuyo texto libre
 * termine en «': enviado.» no puede hacerse pasar por un envío logrado.
 */
function desenlaceDeCorreo(summary: string): string | null {
  const inicio = "Correo '";
  const entreClaseYDestinatario = "' a '";
  const trasDestinatario = "': ";

  if (!summary.startsWith(inicio)) {
    return null;
  }

  const finClase = summary.indexOf(entreClaseYDestinatario, inicio.length);

  if (finClase < 0) {
    return null;
  }

  const empiezaDestinatario = finClase + entreClaseYDestinatario.length;
  const finDestinatario = summary.indexOf(trasDestinatario, empiezaDestinatario);

  if (finDestinatario < 0) {
    return null;
  }

  const resto = summary.slice(finDestinatario + trasDestinatario.length);

  if (resto === 'enviado.') {
    return 'enviado.';
  }

  if (!resto.startsWith('falló. ')) {
    return null;
  }

  const motivo = resto.slice('falló. '.length);
  const reconocido = MOTIVOS_DE_CORREO.find((m) => motivo.startsWith(m.marca));

  // Ni `motivo` ni `resto` vuelven de esta función por ningún camino.
  return reconocido ? `falló — ${reconocido.frase}` : MOTIVO_NO_RECONOCIDO;
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
