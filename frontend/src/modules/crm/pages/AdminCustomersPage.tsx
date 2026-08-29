import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { isApiError } from '../../../shared/http/errors';
import { Badge, Button, Card, EmptyState, Field, Input } from '../../../shared/ui';
import { Table, type Column } from '../../../shared/ui/patterns';
import { AdminCustomerForm } from '../components/AdminCustomerForm';
import {
  adminCustomersService,
  type AdminCustomerListItem,
  type CustomerAccessState,
  type SaveAdminCustomerInput,
} from '../services/adminCustomers';
import '../crm.css';

export function AdminCustomersPage() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [search, setSearch] = useState('');
  const [customers, setCustomers] = useState<AdminCustomerListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setFailure(null);

    try {
      setCustomers(await adminCustomersService.list(search));
    } catch (caught) {
      setFailure(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : 'No se pudo cargar la lista de clientes.',
      );
    } finally {
      setLoading(false);
    }
  }, [search]);

  useEffect(() => {
    void load();
  }, [load]);

  async function create(input: SaveAdminCustomerInput) {
    const created = await adminCustomersService.create(input);
    navigate(`/admin/clientes/${created.customerId}`);
  }

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    setSearch(q.trim());
  }

  const columns = useMemo<Column<AdminCustomerListItem>[]>(
    () => [
      {
        key: 'customer',
        header: 'Cliente',
        render: (customer) => (
          <div>
            <Link
              className="crm-admin-customer__name"
              to={`/admin/clientes/${customer.customerId}`}
            >
              {customer.fullName}
            </Link>
            <div className="crm-admin-customer__secondary">
              {customer.email}
            </div>
          </div>
        ),
      },
      {
        key: 'contact',
        header: 'Contacto',
        render: (customer) =>
          customer.phone ?? (
            <span className="crm-admin-customer__secondary">Sin teléfono</span>
          ),
      },
      {
        key: 'document',
        header: 'Documento',
        render: (customer) =>
          customer.documentNumber ? (
            `${customer.documentType?.toUpperCase() ?? ''} ${customer.documentNumber}`
          ) : (
            <span className="crm-admin-customer__secondary">Sin documento</span>
          ),
      },
      {
        key: 'access',
        header: 'Cuenta',
        render: (customer) => <AccessBadge state={customer.access.state} />,
      },
      {
        key: 'actions',
        header: 'Acciones',
        align: 'right',
        render: (customer) => (
          <Link
            className="crm-admin-customer__open"
            to={`/admin/clientes/${customer.customerId}`}
          >
            Ver ficha
          </Link>
        ),
      },
    ],
    [],
  );

  return (
    <PageContainer
      title="Clientes"
      description="Fichas, contacto, acceso a la tienda y direcciones de la clientela."
      actions={
        <Button onClick={() => setCreating((current) => !current)}>
          {creating ? 'Cerrar alta' : 'Nuevo cliente'}
        </Button>
      }
    >
      {failure && <div className="ui-alert ui-alert--danger" role="alert">{failure}</div>}

      {creating && (
        <Card
          title="Nueva ficha"
          subtitle="La ficha nace sin contraseña. Después puedes enviar una invitación."
        >
          <AdminCustomerForm
            submitLabel="Crear ficha"
            onSubmit={create}
            onCancel={() => setCreating(false)}
          />
        </Card>
      )}

      <form className="crm-admin-search" onSubmit={submitSearch}>
        <Field label="Buscar" hint="Por nombre, correo o documento.">
          {(props) => (
            <Input
              {...props}
              value={q}
              onChange={(event) => setQ(event.target.value)}
              placeholder="Nombre, correo o documento"
            />
          )}
        </Field>
        <Button type="submit" variant="secondary">
          Buscar
        </Button>
      </form>

      <Table
        columns={columns}
        rows={customers}
        rowKey={(customer) => customer.customerId}
        dimmed={(customer) => !customer.isActive}
        loading={loading}
        empty={
          search ? (
            <EmptyState
              title={`Ningún cliente coincide con «${search}»`}
              description="Prueba con menos palabras o con otro dato."
            />
          ) : (
            <EmptyState
              title="Todavía no hay clientes"
              description="Crea la primera ficha para empezar a registrar a tu clientela."
              action={
                <Button onClick={() => setCreating(true)}>
                  Crear primera ficha
                </Button>
              }
            />
          )
        }
      />
    </PageContainer>
  );
}

export function AccessBadge({ state }: { state: CustomerAccessState }) {
  switch (state) {
    case 'active':
      return <Badge tone="success">Cuenta activa</Badge>;
    case 'invited':
      return <Badge tone="warning">Invitada</Badge>;
    case 'deactivated':
      return <Badge tone="neutral">De baja</Badge>;
    case 'blocked':
      return <Badge tone="danger">Bloqueada</Badge>;
    default:
      return <Badge tone="neutral">Sin cuenta</Badge>;
  }
}
