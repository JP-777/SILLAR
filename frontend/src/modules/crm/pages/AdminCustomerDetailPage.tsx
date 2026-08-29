import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { isApiError } from '../../../shared/http/errors';
import { Alert, Badge, Button, Card, EmptyState, Spinner } from '../../../shared/ui';
import { ConfirmDialog } from '../../../shared/ui/patterns';
import { AdminCustomerForm } from '../components/AdminCustomerForm';
import { AccessBadge } from './AdminCustomersPage';
import {
  adminCustomersService,
  type AdminCustomerDetail,
  type SaveAdminCustomerInput,
} from '../services/adminCustomers';
import '../crm.css';

export function AdminCustomerDetailPage() {
  const { customerId = '' } = useParams();
  const [customer, setCustomer] = useState<AdminCustomerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [confirmingDeactivate, setConfirmingDeactivate] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setFailure(null);

    try {
      setCustomer(await adminCustomersService.get(customerId));
    } catch (caught) {
      setFailure(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : isApiError(caught, 'NotFound')
            ? 'La ficha ya no existe.'
            : 'No se pudo cargar la ficha.',
      );
    } finally {
      setLoading(false);
    }
  }, [customerId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function save(input: SaveAdminCustomerInput) {
    const updated = await adminCustomersService.update(customerId, input);
    setCustomer(updated);
    setMessage('Ficha actualizada.');
  }

  async function invite() {
    setBusy(true);
    setMessage(null);
    try {
      const result = await adminCustomersService.invite(customerId);
      setMessage(result.message);
      await load();
    } catch (caught) {
      setMessage(
        isApiError(caught)
          ? caught.displayMessage
          : 'No se pudo emitir la invitación.',
      );
    } finally {
      setBusy(false);
    }
  }

  async function deactivate() {
    setBusy(true);
    setMessage(null);
    try {
      setCustomer(await adminCustomersService.deactivate(customerId));
      setMessage('Cliente dado de baja. Sus sesiones quedaron revocadas.');
    } catch (caught) {
      setMessage(
        isApiError(caught)
          ? caught.displayMessage
          : 'No se pudo dar de baja al cliente.',
      );
    } finally {
      setBusy(false);
      setConfirmingDeactivate(false);
    }
  }

  async function reactivate() {
    setBusy(true);
    setMessage(null);
    try {
      setCustomer(await adminCustomersService.reactivate(customerId));
      setMessage('Cliente reactivado.');
    } catch (caught) {
      setMessage(
        isApiError(caught)
          ? caught.displayMessage
          : 'No se pudo reactivar al cliente.',
      );
    } finally {
      setBusy(false);
    }
  }

  if (loading) {
    return (
      <div className="crm-admin-detail__loading">
        <Spinner size="lg" label="Cargando cliente" />
      </div>
    );
  }

  if (!customer) {
    return (
      <PageContainer title="Cliente">
        <Alert tone="danger">{failure ?? 'No se pudo cargar la ficha.'}</Alert>
        <Link to="/admin/clientes">Volver a clientes</Link>
      </PageContainer>
    );
  }

  const canInvite =
    customer.isActive &&
    (customer.access.state === 'no_account' ||
      customer.access.state === 'invited');

  return (
    <PageContainer
      title={customer.fullName}
      description={customer.email}
      actions={
        <div className="crm-admin-detail__top-actions">
          {customer.isActive ? (
            <Button
              variant="danger"
              onClick={() => setConfirmingDeactivate(true)}
            >
              Dar de baja
            </Button>
          ) : (
            <Button loading={busy} onClick={() => void reactivate()}>
              Reactivar
            </Button>
          )}
        </div>
      }
    >
      <Link to="/admin/clientes">← Volver a clientes</Link>

      {failure && <Alert tone="danger">{failure}</Alert>}
      {message && <Alert tone="info">{message}</Alert>}

      <div className="crm-admin-detail__grid">
        <Card title="Datos y notas internas">
          <AdminCustomerForm
            key={customer.updatedAt}
            customer={customer}
            submitLabel="Guardar cambios"
            onSubmit={save}
          />
        </Card>

        <div className="crm-admin-detail__side">
          <Card title="Estado de la cuenta">
            <div className="crm-admin-status">
              <AccessBadge state={customer.access.state} />

              <dl className="crm-admin-meta">
                <div>
                  <dt>Ficha</dt>
                  <dd>{customer.isActive ? 'Activa' : 'De baja'}</dd>
                </div>
                <div>
                  <dt>Correo</dt>
                  <dd>
                    {customer.access.emailVerified
                      ? 'Verificado'
                      : 'Sin verificar'}
                  </dd>
                </div>
                {customer.access.since && (
                  <div>
                    <dt>Desde</dt>
                    <dd>{formatDate(customer.access.since)}</dd>
                  </div>
                )}
                {customer.access.invitationExpiresAt && (
                  <div>
                    <dt>Invitación vence</dt>
                    <dd>{formatDate(customer.access.invitationExpiresAt)}</dd>
                  </div>
                )}
              </dl>

              {canInvite && (
                <Button
                  variant="secondary"
                  loading={busy}
                  onClick={() => void invite()}
                >
                  {customer.access.state === 'invited'
                    ? 'Reenviar invitación'
                    : 'Enviar invitación'}
                </Button>
              )}
            </div>
          </Card>

          <Card title="Direcciones">
            {customer.addresses.length === 0 ? (
              <p className="crm-admin-customer__secondary">
                No hay direcciones registradas.
              </p>
            ) : (
              <div className="crm-admin-addresses">
                {customer.addresses.map((address) => (
                  <article
                    key={address.customerAddressId}
                    className="crm-admin-address"
                  >
                    <div className="crm-admin-address__title">
                      <strong>{address.label ?? 'Dirección'}</strong>
                      {address.isPreferred && (
                        <Badge tone="success">Preferida</Badge>
                      )}
                      {!address.isActive && (
                        <Badge tone="neutral">De baja</Badge>
                      )}
                    </div>
                    <span>{address.addressLine}</span>
                    <span className="crm-admin-customer__secondary">
                      {[address.district, address.province, address.department]
                        .filter(Boolean)
                        .join(', ')}
                    </span>
                  </article>
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>

      <Card title="Historial de pedidos">
        <EmptyState
          title="Módulo de pedidos no instalado"
          description="Cuando M03 esté disponible, su historial aparecerá en este espacio. La ficha de cliente funciona de forma independiente."
        />
      </Card>

      <ConfirmDialog
        open={confirmingDeactivate}
        title={`Dar de baja a ${customer.fullName}`}
        confirmLabel="Dar de baja"
        danger
        busy={busy}
        onConfirm={() => void deactivate()}
        onCancel={() => setConfirmingDeactivate(false)}
      >
        <p>
          La ficha se conserva, pero el cliente no podrá entrar a la tienda y
          sus sesiones abiertas serán revocadas.
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
