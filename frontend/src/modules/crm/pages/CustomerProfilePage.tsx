import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { isApiError, type ValidationErrors } from '../../../shared/http/errors';
import { Alert, Badge, Button, Card, Field, Input, Spinner } from '../../../shared/ui';
import {
  createCustomerAddress,
  deleteCustomerAddress,
  getCustomerProfile,
  setPreferredCustomerAddress,
  updateCustomerAddress,
  updateCustomerProfile,
  type CustomerAddress,
  type CustomerProfile,
  type SaveCustomerAddressInput,
} from '../services/customerProfile';
import { requestCustomerEmailVerification } from '../services/customerAuth';
import { useCustomerSession } from '../session';
import '../crm.css';

const EMPTY_ADDRESS: SaveCustomerAddressInput = {
  label: '',
  addressLine: '',
  district: '',
  province: '',
  department: '',
  reference: '',
  isPreferred: false,
};

export function CustomerProfilePage() {
  const navigate = useNavigate();
  const { logout, refresh } = useCustomerSession();
  const [profile, setProfile] = useState<CustomerProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [pageError, setPageError] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    setPageError(null);

    try {
      setProfile(await getCustomerProfile());
    } catch (caught) {
      setPageError(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : 'No se pudo cargar tu perfil.',
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function leave() {
    await logout();
    navigate('/', { replace: true });
  }

  if (loading) {
    return (
      <main id="contenido" className="crm-account crm-account--loading">
        <Spinner size="lg" label="Cargando perfil" />
      </main>
    );
  }

  if (!profile) {
    return (
      <main id="contenido" className="crm-account">
        <Alert tone="danger">{pageError ?? 'No se pudo cargar tu perfil.'}</Alert>
        <Button onClick={() => void load()}>Reintentar</Button>
      </main>
    );
  }

  return (
    <main id="contenido" className="crm-account" tabIndex={-1}>
      <header className="crm-account__header">
        <div>
          <Link to="/">← Volver a la tienda</Link>
          <h1>Mi cuenta</h1>
          <p className="crm-account__lead">
            Administra tus datos de contacto y direcciones de entrega.
          </p>
        </div>

        <Button variant="secondary" onClick={() => void leave()}>
          Cerrar sesión
        </Button>
      </header>

      {pageError && <Alert tone="danger">{pageError}</Alert>}

      <EmailStatus profile={profile} onReload={load} />

      <div className="crm-account__grid">
        <ProfileForm
          profile={profile}
          onSaved={async (saved) => {
            setProfile(saved);
            await refresh();
          }}
        />

        <AddressSection
          addresses={profile.addresses}
          onChanged={load}
        />
      </div>
    </main>
  );
}

function EmailStatus({
  profile,
  onReload,
}: {
  profile: CustomerProfile;
  onReload: () => Promise<void>;
}) {
  const [sending, setSending] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  if (profile.emailVerified) {
    return (
      <Alert tone="success" title="Correo verificado">
        Tu correo está verificado.
      </Alert>
    );
  }

  async function resend() {
    setSending(true);
    setMessage(null);

    try {
      const response = await requestCustomerEmailVerification();
      setMessage(response.message);
      await onReload();
    } catch (caught) {
      setMessage(
        isApiError(caught, 'Network')
          ? 'No se pudo contactar con el servidor.'
          : 'No se pudo procesar la solicitud.',
      );
    } finally {
      setSending(false);
    }
  }

  return (
    <Alert tone="warning" title="Correo pendiente de verificar">
      <span>
        Puedes usar tu cuenta, pero tu correo todavía no está verificado.
      </span>
      <Button
        variant="secondary"
        size="sm"
        loading={sending}
        onClick={() => void resend()}
      >
        Reenviar verificación
      </Button>
      {message && <span>{message}</span>}
    </Alert>
  );
}

function ProfileForm({
  profile,
  onSaved,
}: {
  profile: CustomerProfile;
  onSaved: (profile: CustomerProfile) => Promise<void>;
}) {
  const [fullName, setFullName] = useState(profile.fullName);
  const [email, setEmail] = useState(profile.email);
  const [phone, setPhone] = useState(profile.phone ?? '');
  const [documentType, setDocumentType] = useState(profile.documentType ?? '');
  const [documentNumber, setDocumentNumber] = useState(profile.documentNumber ?? '');
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrors({});
    setMessage(null);
    setSaving(true);

    try {
      const saved = await updateCustomerProfile({
        fullName,
        email,
        phone,
        documentType,
        documentNumber,
      });
      await onSaved(saved);
      setMessage('Tus datos fueron actualizados.');
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
      } else if (isApiError(caught, 'Conflict')) {
        setMessage(caught.displayMessage);
      } else {
        setMessage(
          isApiError(caught, 'Network')
            ? 'No se pudo contactar con el servidor.'
            : 'No se pudieron guardar los cambios.',
        );
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card title="Datos personales">
      <form className="pf-form" onSubmit={submit} noValidate>
        {message && <Alert tone="info">{message}</Alert>}

        <Field label="Nombre completo" required error={errors.nombre?.[0]}>
          {(props) => (
            <Input
              {...props}
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              autoComplete="name"
            />
          )}
        </Field>

        <Field label="Correo" required error={errors.correo?.[0]}>
          {(props) => (
            <Input
              {...props}
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
            />
          )}
        </Field>

        <Field label="Teléfono" hint="Opcional">
          {(props) => (
            <Input
              {...props}
              type="tel"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              autoComplete="tel"
            />
          )}
        </Field>

        <div className="crm-account__document">
          <Field label="Tipo de documento" error={errors.documento?.[0]}>
            {(props) => (
              <select
                {...props}
                className="ui-input"
                value={documentType}
                onChange={(event) => setDocumentType(event.target.value)}
              >
                <option value="">Sin documento</option>
                <option value="dni">DNI</option>
                <option value="ruc">RUC</option>
              </select>
            )}
          </Field>

          <Field label="Número de documento">
            {(props) => (
              <Input
                {...props}
                value={documentNumber}
                onChange={(event) => setDocumentNumber(event.target.value)}
              />
            )}
          </Field>
        </div>

        <Button type="submit" loading={saving}>
          Guardar datos
        </Button>
      </form>
    </Card>
  );
}

function AddressSection({
  addresses,
  onChanged,
}: {
  addresses: CustomerAddress[];
  onChanged: () => Promise<void>;
}) {
  const [draft, setDraft] = useState<SaveCustomerAddressInput>(EMPTY_ADDRESS);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  function reset() {
    setDraft(EMPTY_ADDRESS);
    setEditingId(null);
    setErrors({});
    setMessage(null);
  }

  function edit(address: CustomerAddress) {
    setEditingId(address.customerAddressId);
    setDraft({
      label: address.label ?? '',
      addressLine: address.addressLine,
      district: address.district ?? '',
      province: address.province ?? '',
      department: address.department ?? '',
      reference: address.reference ?? '',
      isPreferred: address.isPreferred,
    });
    setErrors({});
    setMessage(null);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrors({});
    setMessage(null);
    setSaving(true);

    try {
      if (editingId) {
        await updateCustomerAddress(editingId, draft);
      } else {
        await createCustomerAddress(draft);
      }

      reset();
      await onChanged();
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
      } else {
        setMessage(
          isApiError(caught, 'Network')
            ? 'No se pudo contactar con el servidor.'
            : 'No se pudo guardar la dirección.',
        );
      }
    } finally {
      setSaving(false);
    }
  }

  async function preferred(id: string) {
    setMessage(null);
    try {
      await setPreferredCustomerAddress(id);
      await onChanged();
    } catch {
      setMessage('No se pudo cambiar la dirección preferida.');
    }
  }

  async function remove(id: string) {
    setMessage(null);
    try {
      await deleteCustomerAddress(id);
      if (editingId === id) {
        reset();
      }
      await onChanged();
    } catch {
      setMessage('No se pudo eliminar la dirección.');
    }
  }

  return (
    <Card title="Direcciones">
      <div className="crm-addresses">
        {message && <Alert tone="danger">{message}</Alert>}

        {addresses.length === 0 ? (
          <p className="crm-addresses__empty">
            Todavía no tienes direcciones guardadas.
          </p>
        ) : (
          <div className="crm-addresses__list">
            {addresses.map((address) => (
              <article
                key={address.customerAddressId}
                className="crm-address"
                aria-label={`Dirección ${address.label ?? address.addressLine}`}
              >
                <div className="crm-address__heading">
                  <strong>{address.label ?? 'Dirección'}</strong>
                  {address.isPreferred && (
                    <Badge tone="success">Preferida</Badge>
                  )}
                </div>

                <p>{address.addressLine}</p>
                <p className="crm-address__secondary">
                  {[address.district, address.province, address.department]
                    .filter(Boolean)
                    .join(', ')}
                </p>

                {address.reference && (
                  <p className="crm-address__secondary">
                    Referencia: {address.reference}
                  </p>
                )}

                <div className="crm-address__actions">
                  {!address.isPreferred && (
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => void preferred(address.customerAddressId)}
                    >
                      Usar como preferida
                    </Button>
                  )}
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => edit(address)}
                  >
                    Editar
                  </Button>
                  <Button
                    size="sm"
                    variant="danger"
                    onClick={() => void remove(address.customerAddressId)}
                  >
                    Eliminar
                  </Button>
                </div>
              </article>
            ))}
          </div>
        )}

        <form className="pf-form crm-address-form" onSubmit={submit} noValidate>
          <h3>{editingId ? 'Editar dirección' : 'Añadir dirección'}</h3>

          <Field label="Etiqueta" hint="Ejemplo: Casa, Oficina">
            {(props) => (
              <Input
                {...props}
                value={draft.label}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    label: event.target.value,
                  }))
                }
              />
            )}
          </Field>

          <Field
            label="Dirección"
            required
            error={errors.direccion?.[0]}
          >
            {(props) => (
              <Input
                {...props}
                value={draft.addressLine}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    addressLine: event.target.value,
                  }))
                }
                autoComplete="street-address"
              />
            )}
          </Field>

          <div className="crm-address-form__location">
            <Field label="Distrito">
              {(props) => (
                <Input
                  {...props}
                  value={draft.district}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      district: event.target.value,
                    }))
                  }
                />
              )}
            </Field>
            <Field label="Provincia">
              {(props) => (
                <Input
                  {...props}
                  value={draft.province}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      province: event.target.value,
                    }))
                  }
                />
              )}
            </Field>
            <Field label="Departamento">
              {(props) => (
                <Input
                  {...props}
                  value={draft.department}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      department: event.target.value,
                    }))
                  }
                />
              )}
            </Field>
          </div>

          <Field label="Referencia" hint="Opcional">
            {(props) => (
              <Input
                {...props}
                value={draft.reference}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    reference: event.target.value,
                  }))
                }
              />
            )}
          </Field>

          <label className="crm-address-form__preferred">
            <input
              type="checkbox"
              checked={draft.isPreferred}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  isPreferred: event.target.checked,
                }))
              }
            />
            Marcar como preferida
          </label>

          <div className="crm-address-form__actions">
            <Button type="submit" loading={saving}>
              {editingId ? 'Guardar dirección' : 'Añadir dirección'}
            </Button>
            {editingId && (
              <Button variant="secondary" onClick={reset}>
                Cancelar edición
              </Button>
            )}
          </div>
        </form>
      </div>
    </Card>
  );
}
