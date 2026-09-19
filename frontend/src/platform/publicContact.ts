import type { PublicSettings } from './usePublicSettings';

const PENDING_MARKER = 'PENDIENTE_DEFINIR';

export interface PublicContactData {
  readonly whatsappNumber: string | null;
  readonly whatsappUrl: string | null;
  readonly contactEmail: string | null;
  readonly contactPhone: string | null;
  readonly businessAddress: string | null;
  readonly businessReference: string | null;
  readonly googleMapsUrl: string | null;
  readonly businessHours: string | null;
}

function publicValue(settings: PublicSettings, key: string): string | null {
  const value = settings[key]?.trim();

  if (!value || value === PENDING_MARKER) {
    return null;
  }

  return value;
}

export function whatsappUrl(number: string | null): string | null {
  if (!number) {
    return null;
  }

  const digits = number.replace(/\D/g, '');
  return digits.length > 0 ? `https://wa.me/${digits}` : null;
}

export function publicContactFromSettings(
  settings: PublicSettings,
): PublicContactData {
  const whatsappNumber = publicValue(settings, 'whatsapp_number');

  return {
    whatsappNumber,
    whatsappUrl: whatsappUrl(whatsappNumber),
    contactEmail: publicValue(settings, 'contact_email'),
    contactPhone: publicValue(settings, 'contact_phone'),
    businessAddress: publicValue(settings, 'business_address'),
    businessReference: publicValue(settings, 'business_reference'),
    googleMapsUrl: publicValue(settings, 'google_maps_url'),
    businessHours: publicValue(settings, 'business_hours'),
  };
}

export function hasPublicContact(contact: PublicContactData): boolean {
  return Boolean(
    contact.whatsappUrl
    || contact.contactEmail
    || contact.contactPhone
    || contact.businessAddress
    || contact.businessReference
    || contact.googleMapsUrl
    || contact.businessHours
  );
}
