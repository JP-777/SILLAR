import { catalogAuditEntityVocabulary } from '../modules/catalog/auditEntityVocabulary';
import { cmsAuditEntityVocabulary } from '../modules/cms/auditEntityVocabulary';
import { coreAuditEntityVocabulary } from '../modules/core/auditEntityVocabulary';
import { crmAuditEntityVocabulary } from '../modules/crm/auditEntityVocabulary';

/**
 * Vocabulario visible de tipos de entidad aportado por un módulo.
 *
 * Igual que navegación, portada y pie, el módulo declara su parte y el
 * armazón decide cuáles participan según las capacidades de la instalación.
 * La auditoría sigue leyendo el código técnico que escribió el productor; esta
 * costura solo decide cómo presentarlo.
 */
export interface AuditEntityVocabulary {
  /** Código del módulo, el mismo que devuelve `/api/capabilities`. */
  readonly moduleCode: string;
  /** Etiquetas visibles para los `entityType` que pertenecen al módulo. */
  readonly labels: Readonly<Record<string, string>>;
}

/**
 * Vocabularios conocidos por este producto, en orden de composición.
 *
 * El orden pertenece al armazón, no a cada módulo. También decide precedencia
 * si dos módulos declarasen por error el mismo `entityType`: gana el primero,
 * de modo que el resultado sea determinista y el conflicto quede visible aquí.
 */
export const AUDIT_ENTITY_VOCABULARIES: readonly AuditEntityVocabulary[] = [
  coreAuditEntityVocabulary,
  catalogAuditEntityVocabulary,
  cmsAuditEntityVocabulary,
  crmAuditEntityVocabulary,
];

/** Vocabularios de los módulos activos, conservando el orden del armazón. */
export function visibleAuditEntityVocabularies(
  isActive: (code: string) => boolean,
): AuditEntityVocabulary[] {
  return AUDIT_ENTITY_VOCABULARIES.filter((vocabulary) => isActive(vocabulary.moduleCode));
}

/**
 * Etiqueta visible, o el código técnico tal cual cuando nadie lo declara.
 *
 * El fallback es deliberadamente honesto: nunca inventa «Desconocido».
 */
export function auditEntityLabel(
  entityType: string,
  vocabularies: readonly AuditEntityVocabulary[],
): string {
  for (const vocabulary of vocabularies) {
    const label = vocabulary.labels[entityType];
    if (label !== undefined) {
      return label;
    }
  }

  return entityType;
}
