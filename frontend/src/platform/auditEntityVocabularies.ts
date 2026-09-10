import { catalogAuditEntityVocabulary } from '../modules/catalog/routes';
import { cmsAuditEntityVocabulary } from '../modules/cms/cmsHome';
import { coreAuditEntityVocabulary } from '../modules/core/auditEntityVocabulary';
import { crmAuditEntityVocabulary } from '../modules/crm/routes';
import {
  auditEntityLabel,
  composeAuditEntityVocabularies,
  type AuditEntityLabels,
  type AuditEntityVocabulary,
} from './auditEntityVocabulary';

/**
 * Registro estático de contribuciones.
 *
 * La plataforma conoce qué contribuciones componen el producto, pero ninguna
 * etiqueta concreta. Las asociaciones pertenecen a cada módulo.
 */
export const AUDIT_ENTITY_VOCABULARIES: readonly AuditEntityVocabulary[] = [
  coreAuditEntityVocabulary,
  catalogAuditEntityVocabulary,
  cmsAuditEntityVocabulary,
  crmAuditEntityVocabulary,
];

/**
 * Compone únicamente el vocabulario de los módulos activos.
 *
 * Una fila histórica de un módulo apagado cae deliberadamente al código
 * técnico, porque su contribución deja de participar.
 */
export function visibleAuditEntityLabels(
  isActive: (code: string) => boolean,
): AuditEntityLabels {
  return composeAuditEntityVocabularies(
    AUDIT_ENTITY_VOCABULARIES.filter((vocabulary) =>
      isActive(vocabulary.moduleCode)),
  );
}

export { auditEntityLabel };
export type { AuditEntityLabels, AuditEntityVocabulary };
