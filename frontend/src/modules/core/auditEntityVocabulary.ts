import type { AuditEntityVocabulary } from '../../platform/auditEntityVocabularies';

/** Vocabulario de auditoría que pertenece a CORE. */
export const coreAuditEntityVocabulary: AuditEntityVocabulary = {
  moduleCode: 'core',
  labels: {
    admin_session: 'Sesión',
    admin_user: 'Usuario',
    email: 'Correo',
    installation: 'Instalación',
    media_asset: 'Archivo',
    module: 'Módulo',
    setting: 'Ajuste',
  },
};
