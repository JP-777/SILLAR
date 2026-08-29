import { useState, type FormEvent } from 'react';
import { isApiError, type ValidationErrors } from '../../../shared/http/errors';
import { Alert, Button, Field, Input } from '../../../shared/ui';
import type {
  AdminCustomerDetail,
  SaveAdminCustomerInput,
} from '../services/adminCustomers';

interface AdminCustomerFormProps {
  customer?: AdminCustomerDetail;
  submitLabel: string;
  onSubmit: (input: SaveAdminCustomerInput) => Promise<void>;
  onCancel?: () => void;
}

export function AdminCustomerForm({
  customer,
  submitLabel,
  onSubmit,
  onCancel,
}: AdminCustomerFormProps) {
  const [fullName, setFullName] = useState(customer?.fullName ?? '');
  const [email, setEmail] = useState(customer?.email ?? '');
  const [phone, setPhone] = useState(customer?.phone ?? '');
  const [documentType, setDocumentType] = useState(
    customer?.documentType ?? '',
  );
  const [documentNumber, setDocumentNumber] = useState(
    customer?.documentNumber ?? '',
  );
  const [internalNotes, setInternalNotes] = useState(
    customer?.internalNotes ?? '',
  );
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrors({});
    setMessage(null);
    setSaving(true);

    try {
      await onSubmit({
        fullName,
        email,
        phone,
        documentType,
        documentNumber,
        internalNotes,
      });
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
      } else {
        setMessage(
          isApiError(caught, 'Conflict')
            ? caught.displayMessage
            : isApiError(caught, 'Network')
              ? 'No se pudo contactar con el servidor.'
              : 'No se pudo guardar la ficha.',
        );
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="pf-form" onSubmit={submit} noValidate>
      {message && <Alert tone="danger">{message}</Alert>}

      <Field label="Nombre completo" required error={errors.cliente?.[0]}>
        {(props) => (
          <Input
            {...props}
            value={fullName}
            onChange={(event) => setFullName(event.target.value)}
            autoComplete="name"
          />
        )}
      </Field>

      <Field label="Correo" required>
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

      <div className="crm-admin-form__document">
        <Field label="Tipo de documento">
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

      <Field
        label="Notas internas"
        hint="Solo las ve el personal. Nunca salen en la tienda."
      >
        {(props) => (
          <textarea
            {...props}
            className="ui-input crm-textarea"
            value={internalNotes}
            onChange={(event) => setInternalNotes(event.target.value)}
            rows={5}
          />
        )}
      </Field>

      <div className="crm-admin-form__actions">
        <Button type="submit" loading={saving}>
          {submitLabel}
        </Button>
        {onCancel && (
          <Button variant="secondary" onClick={onCancel}>
            Cancelar
          </Button>
        )}
      </div>
    </form>
  );
}
