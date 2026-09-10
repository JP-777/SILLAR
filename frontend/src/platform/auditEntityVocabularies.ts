import { catalogAuditEntityVocabulary } from '../modules/catalog/routes';
import { cmsAuditEntityVocabulary } from '../modules/cms/cmsHome';
import { coreAuditEntityVocabulary } from '../modules/core/auditEntityVocabulary';
import { crmAuditEntityVocabulary } from '../modules/crm/routes';
import {
  auditEntityLabel,
  reportAuditEntityConflictInDevelopment,
  visibleAuditEntityLabelsFrom,
  type AuditEntityLabels,
  type AuditEntityVocabulary,
  type AuditEntityVocabularyConflictReporter,
} from './auditEntityVocabulary';

/**
 * Registro estático de contribuciones.
 *
 * La plataforma conoce qué contribuciones componen el producto, pero ninguna
 * asociación concreta entityType → etiqueta.
 */
export const AUDIT_ENTITY_VOCABULARIES: readonly AuditEntityVocabulary[] = [
  coreAuditEntityVocabulary,
  catalogAuditEntityVocabulary,
  cmsAuditEntityVocabulary,
  crmAuditEntityVocabulary,
];

const reportAuditEntityConflict: AuditEntityVocabularyConflictReporter = (
  conflict,
) => {
  reportAuditEntityConflictInDevelopment(import.meta.env.DEV, conflict);
};

/**
 * Etiquetas aportadas únicamente por módulos activos.
 *
 * Una clave conflictiva queda ausente siempre. En desarrollo además se
 * informa explícitamente; en producción la auditoría continúa funcionando y
 * esa clave degrada a su código técnico.
 */
export function visibleAuditEntityLabels(
  isActive: (code: string) => boolean,
): AuditEntityLabels {
  return visibleAuditEntityLabelsFrom(
    AUDIT_ENTITY_VOCABULARIES,
    isActive,
    reportAuditEntityConflict,
  );
}

export { auditEntityLabel };
export type { AuditEntityLabels, AuditEntityVocabulary };
