import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { isApiError, type ValidationErrors } from '../../../shared/http/errors';
import { Alert, Button, Card, Field, Input } from '../../../shared/ui';
import { submitPublicContact } from '../services/contactMessages';
import { useCustomerSession } from '../session';
import '../crm.css';

export function ContactPage() {
  const { customer } = useCustomerSession();
  const [fullName, setFullName] = useState(customer?.fullName ?? '');
  const [email, setEmail] = useState(customer?.email ?? '');
  const [phone, setPhone] = useState('');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrors({});
    setFailure(null);
    setSuccess(null);
    setSending(true);

    try {
      const accepted = await submitPublicContact({
        fullName,
        email,
        phone,
        subject,
        message,
      });
      setSuccess(accepted.message);
      setSubject('');
      setMessage('');
    } catch (caught) {
      if (isApiError(caught, 'ValidationFailed')) {
        setErrors(caught.errors ?? {});
        setFailure(caught.displayMessage);
      } else if (isApiError(caught) && caught.status === 429) {
        setFailure(
          'Has enviado varios mensajes seguidos. Inténtalo nuevamente dentro de un momento.',
        );
      } else {
        setFailure(
          isApiError(caught, 'Network')
            ? 'No se pudo contactar con el servidor.'
            : 'No se pudo enviar el mensaje.',
        );
      }
    } finally {
      setSending(false);
    }
  }

  return (
    <main id="contenido" className="crm-contact" tabIndex={-1}>
      <div className="crm-contact__header">
        <Link to="/">← Volver al inicio</Link>
        <h1>Contacto</h1>
        <p>
          Cuéntanos en qué podemos ayudarte. Necesitamos al menos un correo o
          un teléfono para poder responderte.
        </p>
      </div>

      <Card title="Envíanos un mensaje">
        <form className="pf-form" onSubmit={submit} noValidate>
          {customer && (
            <Alert tone="info">
              Estás enviando este mensaje desde tu cuenta de cliente.
            </Alert>
          )}
          {failure && <Alert tone="danger">{failure}</Alert>}
          {success && <Alert tone="success">{success}</Alert>}

          <Field label="Nombre completo" required error={errors.contacto?.[0]}>
            {(props) => (
              <Input
                {...props}
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
                autoComplete="name"
              />
            )}
          </Field>

          <div className="crm-contact__contact">
            <Field label="Correo" hint="Correo o teléfono: al menos uno.">
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

            <Field label="Teléfono" hint="Correo o teléfono: al menos uno.">
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
          </div>

          <Field label="Asunto" hint="Opcional">
            {(props) => (
              <Input
                {...props}
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
              />
            )}
          </Field>

          <Field label="Mensaje" required>
            {(props) => (
              <textarea
                {...props}
                className="ui-input crm-textarea"
                rows={7}
                value={message}
                onChange={(event) => setMessage(event.target.value)}
              />
            )}
          </Field>

          <Button type="submit" loading={sending}>
            Enviar mensaje
          </Button>
        </form>
      </Card>
    </main>
  );
}
