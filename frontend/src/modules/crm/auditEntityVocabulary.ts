import type { AuditEntityVocabulary } from '../../platform/auditEntityVocabularies';

/** Vocabulario de auditoría que pertenece a M04 · Clientes y contacto. */
export const crmAuditEntityVocabulary: AuditEntityVocabulary = {
  moduleCode: 'crm',
  labels: {
    contact_message: 'Mensaje de contacto',
    customer: 'Cliente',
    customer_invitation: 'Invitación de cliente',
  },
};
