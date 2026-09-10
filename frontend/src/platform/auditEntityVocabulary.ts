/**
 * Una asociación visible declarada por un módulo para un entityType auditado.
 *
 * Se conserva como entrada individual y no como propiedad de un objeto para
 * que dos declaraciones iguales sigan existiendo hasta llegar al compositor.
 */
export type AuditEntityLabelEntry = readonly [
  entityType: string,
  label: string,
];

/** Vocabulario visible aportado por un módulo. */
export interface AuditEntityVocabulary {
  readonly moduleCode: string;
  readonly entries: readonly AuditEntityLabelEntry[];
}

export type AuditEntityLabels = Readonly<Record<string, string>>;

export interface AuditEntityDeclaration {
  readonly moduleCode: string;
  readonly label: string;
}

export interface AuditEntityVocabularyConflict {
  readonly entityType: string;
  readonly declarations: readonly AuditEntityDeclaration[];
}

export type AuditEntityVocabularyConflictReporter = (
  conflict: AuditEntityVocabularyConflict,
) => void;

/**
 * Compone contribuciones sin resolver silenciosamente una colisión.
 *
 * Si una clave aparece más de una vez, incluso dentro de la misma
 * contribución, ninguna declaración gana: la clave se omite del resultado.
 */
export function composeAuditEntityVocabularies(
  vocabularies: readonly AuditEntityVocabulary[],
  reportConflict: AuditEntityVocabularyConflictReporter = () => undefined,
): AuditEntityLabels {
  const declarations = new Map<string, AuditEntityDeclaration[]>();

  for (const vocabulary of vocabularies) {
    for (const [entityType, label] of vocabulary.entries) {
      const current = declarations.get(entityType) ?? [];
      current.push({
        moduleCode: vocabulary.moduleCode,
        label,
      });
      declarations.set(entityType, current);
    }
  }

  const labels: Record<string, string> = {};

  for (const [entityType, candidates] of declarations) {
    if (candidates.length === 1) {
      labels[entityType] = candidates[0].label;
      continue;
    }

    reportConflict({
      entityType,
      declarations: candidates,
    });
  }

  return Object.freeze(labels);
}

/** Compone únicamente las contribuciones cuyos módulos están activos. */
export function visibleAuditEntityLabelsFrom(
  vocabularies: readonly AuditEntityVocabulary[],
  isActive: (moduleCode: string) => boolean,
  reportConflict: AuditEntityVocabularyConflictReporter = () => undefined,
): AuditEntityLabels {
  return composeAuditEntityVocabularies(
    vocabularies.filter((vocabulary) => isActive(vocabulary.moduleCode)),
    reportConflict,
  );
}

/** Etiqueta humana o código técnico exacto cuando la clave no está registrada. */
export function auditEntityLabel(
  entityType: string,
  labels: AuditEntityLabels,
): string {
  return labels[entityType] ?? entityType;
}

/**
 * Señala una inconsistencia únicamente durante desarrollo.
 *
 * La detección y omisión pertenecen al compositor; esta función solo decide
 * cuándo hacer visible el defecto.
 */
export function reportAuditEntityConflictInDevelopment(
  isDevelopment: boolean,
  conflict: AuditEntityVocabularyConflict,
  report: (message: string) => void = console.error,
): void {
  if (!isDevelopment) {
    return;
  }

  const modules = conflict.declarations
    .map((declaration) => declaration.moduleCode)
    .join(', ');

  report(
    `entityType de auditoría duplicado "${conflict.entityType}": ${modules}`,
  );
}
