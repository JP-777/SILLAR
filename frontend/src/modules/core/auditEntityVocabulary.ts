import type { AuditEntityVocabulary } from '../../platform/auditEntityVocabulary';

/** Vocabulario de auditoría que pertenece a CORE. */
export const coreAuditEntityVocabulary: AuditEntityVocabulary = {
  moduleCode: 'core',
  entries: [
    ['admin_session', 'Sesión'],
    ['admin_user', 'Usuario'],
    ['email', 'Correo'],
    ['installation', 'Instalación'],
    ['installation_time_zone', 'Zona horaria del negocio'],
    ['media_asset', 'Archivo'],
    ['module', 'Módulo'],
    ['setting', 'Ajuste'],
  ],
};
