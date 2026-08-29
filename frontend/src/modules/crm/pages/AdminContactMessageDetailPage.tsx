import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { isApiError } from '../../../shared/http/errors';
import { Alert, Badge, Button, Card, Spinner } from '../../../shared/ui';
import { ConfirmDialog } from '../../../shared/ui/patterns';
import {
  adminContactMessagesService,
  type AdminContactMessageDetail,
} from '../services/contactMessages';
import '../crm.css';

export function AdminContactMessageDetailPage() {
  const contactMessageId = Number(useParams().contactMessageId ?? '');
  const [message, setMessage] = useState<AdminContactMessageDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setFailure(null);

    try {
      setMessage(await adminContactMessagesService.get(contactMessageId));
    } catch (caught) {
      setFailure(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : isApiError(caught, 'NotFound')
            ? 'El mensaje ya no existe.'
            : 'No se pudo cargar el mensaje.',
      );
    } finally {
      setLoading(false);
    }
  }, [contactMessageId]);

  useEffect(() => {
    if (Number.isFinite(contactMessageId) && contactMessageId > 0) {
      void load();
    } else {
      setFailure('El identificador del mensaje no es válido.');
      setLoading(false);
    }
  }, [contactMessageId, load]);

  async function deactivate() {
    setBusy(true);
    setFailure(null);

    try {
      setMessage(
        await adminContactMessagesService.deactivate(contactMessageId),
      );
    } catch (caught) {
      setFailure(
        isApiError(caught)
          ? caught.displayMessage
          : 'No se pudo dar de baja el mensaje.',
      );
    } finally {
      setBusy(false);
      setConfirming(false);
    }
  }

  if (loading) {
    return (
      <div className="crm-admin-detail__loading">
        <Spinner size="lg" label="Cargando mensaje" />
      </div>
    );
  }

  if (!message) {
    return (
      <PageContainer title="Mensaje de contacto">
        <Alert tone="danger">
          {failure ?? 'No se pudo cargar el mensaje.'}
        </Alert>
        <Link to="/admin/mensajes">Volver a mensajes</Link>
      </PageContainer>
    );
  }

  return (
    <PageContainer
      title={message.subject ?? 'Mensaje sin asunto'}
      description={`Enviado por ${message.fullName}`}
      actions={
        message.isActive ? (
          <Button variant="danger" onClick={() => setConfirming(true)}>
            Dar de baja
          </Button>
        ) : (
          <Badge tone="neutral">De baja</Badge>
        )
      }
    >
      <Link to="/admin/mensajes">← Volver a mensajes</Link>

      {failure && <Alert tone="danger">{failure}</Alert>}

      <div className="crm-contact-detail__grid">
        <Card title="Mensaje">
          <p className="crm-contact-detail__message">{message.message}</p>
        </Card>

        <Card title="Remitente">
          <dl className="crm-admin-meta">
            <div>
              <dt>Nombre</dt>
              <dd>{message.fullName}</dd>
            </div>
            <div>
              <dt>Correo</dt>
              <dd>{message.email ?? 'No indicado'}</dd>
            </div>
            <div>
              <dt>Teléfono</dt>
              <dd>{message.phone ?? 'No indicado'}</dd>
            </div>
            <div>
              <dt>Origen</dt>
              <dd>
                {message.customerId ? (
                  <Link to={`/admin/clientes/${message.customerId}`}>
                    Ficha de cliente
                  </Link>
                ) : (
                  'Visitante'
                )}
              </dd>
            </div>
            <div>
              <dt>Recibido</dt>
              <dd>{formatDate(message.createdAt)}</dd>
            </div>
          </dl>
        </Card>
      </div>

      <ConfirmDialog
        open={confirming}
        title="Dar de baja el mensaje"
        confirmLabel="Dar de baja"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setConfirming(false)}
      >
        <p>
          El mensaje se conserva para trazabilidad, pero dejará de aparecer en
          la bandeja activa.
        </p>
      </ConfirmDialog>
    </PageContainer>
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('es-PE', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}
