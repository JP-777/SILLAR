import type { ReactNode } from 'react';
import type { PublicContactData } from './publicContact';

interface PublicContactDetailsProps {
  readonly contact: PublicContactData;
  readonly whatsappLabel?: string;
}

export function PublicContactDetails({
  contact,
  whatsappLabel = 'WhatsApp',
}: PublicContactDetailsProps) {
  const rows: { key: string; label: string; value: ReactNode }[] = [];

  if (contact.whatsappUrl && contact.whatsappNumber) {
    rows.push({
      key: 'whatsapp',
      label: 'WhatsApp',
      value: (
        <a
          href={contact.whatsappUrl}
          target="_blank"
          rel="noopener"
          className="pf-contact-details__whatsapp"
        >
          {whatsappLabel} · {contact.whatsappNumber}
        </a>
      ),
    });
  }

  if (contact.contactPhone) {
    const phoneHref = contact.contactPhone.replace(/[^\d+]/g, '');

    rows.push({
      key: 'phone',
      label: 'Teléfono',
      value: <a href={`tel:${phoneHref}`}>{contact.contactPhone}</a>,
    });
  }

  if (contact.contactEmail) {
    rows.push({
      key: 'email',
      label: 'Correo',
      value: <a href={`mailto:${contact.contactEmail}`}>{contact.contactEmail}</a>,
    });
  }

  if (contact.businessHours) {
    rows.push({
      key: 'hours',
      label: 'Horario',
      value: contact.businessHours,
    });
  }

  if (contact.businessAddress) {
    rows.push({
      key: 'address',
      label: 'Dirección',
      value: contact.businessAddress,
    });
  }

  if (contact.businessReference) {
    rows.push({
      key: 'reference',
      label: 'Referencia',
      value: contact.businessReference,
    });
  }

  if (contact.googleMapsUrl) {
    rows.push({
      key: 'maps',
      label: 'Mapa',
      value: (
        <a href={contact.googleMapsUrl} target="_blank" rel="noopener">
          Ver ubicación en Google Maps
        </a>
      ),
    });
  }

  if (rows.length === 0) {
    return null;
  }

  return (
    <dl className="pf-contact-details">
      {rows.map((row) => (
        <div className="pf-contact-details__item" key={row.key}>
          <dt>{row.label}</dt>
          <dd>{row.value}</dd>
        </div>
      ))}
    </dl>
  );
}
