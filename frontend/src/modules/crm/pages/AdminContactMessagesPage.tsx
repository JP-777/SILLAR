import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { isApiError } from '../../../shared/http/errors';
import { Badge, EmptyState } from '../../../shared/ui';
import { Table, type Column } from '../../../shared/ui/patterns';
import {
  adminContactMessagesService,
  type AdminContactMessageListItem,
} from '../services/contactMessages';
import '../crm.css';

export function AdminContactMessagesPage() {
  const [includeInactive, setIncludeInactive] = useState(false);
  const [messages, setMessages] = useState<AdminContactMessageListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setFailure(null);

    try {
      setMessages(await adminContactMessagesService.list(includeInactive));
    } catch (caught) {
      setFailure(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : 'No se pudo cargar la bandeja de mensajes.',
      );
    } finally {
      setLoading(false);
    }
  }, [includeInactive]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns = useMemo<Column<AdminContactMessageListItem>[]>(
    () => [
      {
        key: 'sender',
        header: 'Remitente',
        render: (message) => (
          <div>
            <Link
              className="crm-admin-customer__name"
              to={`/admin/mensajes/${message.contactMessageId}`}
            >
              {message.fullName}
            </Link>
            <div className="crm-admin-customer__secondary">
              {message.email ?? message.phone ?? 'Sin dato de contacto'}
            </div>
          </div>
        ),
      },
      {
        key: 'subject',
        header: 'Asunto',
        render: (message) =>
          message.subject ?? (
            <span className="crm-admin-customer__secondary">Sin asunto</span>
          ),
      },
      {
        key: 'link',
        header: 'Origen',
        render: (message) =>
          message.customerId ? (
            <Badge tone="success">Cliente</Badge>
          ) : (
            <Badge tone="neutral">Visitante</Badge>
          ),
      },
      {
        key: 'date',
        header: 'Fecha',
        render: (message) => formatDate(message.createdAt),
      },
      {
        key: 'status',
        header: 'Estado',
        render: (message) =>
          message.isActive ? (
            <Badge tone="success">Activo</Badge>
          ) : (
            <Badge tone="neutral">De baja</Badge>
          ),
      },
      {
        key: 'actions',
        header: 'Acciones',
        align: 'right',
        render: (message) => (
          <Link
            className="crm-admin-customer__open"
            to={`/admin/mensajes/${message.contactMessageId}`}
          >
            Ver mensaje
          </Link>
        ),
      },
    ],
    [],
  );

  return (
    <PageContainer
      title="Mensajes"
      description="Consultas enviadas desde el formulario público de contacto."
    >
      {failure && (
        <div className="ui-alert ui-alert--danger" role="alert">
          {failure}
        </div>
      )}

      <label className="crm-admin-toggle">
        <input
          type="checkbox"
          checked={includeInactive}
          onChange={(event) => setIncludeInactive(event.target.checked)}
        />
        Incluir mensajes dados de baja
      </label>

      <Table
        columns={columns}
        rows={messages}
        rowKey={(message) => message.contactMessageId}
        dimmed={(message) => !message.isActive}
        loading={loading}
        empty={
          <EmptyState
            title={
              includeInactive
                ? 'No hay mensajes de contacto'
                : 'No hay mensajes activos'
            }
            description="Los mensajes enviados desde la web aparecerán aquí."
          />
        }
      />
    </PageContainer>
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('es-PE', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}
