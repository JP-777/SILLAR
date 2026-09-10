/**
 * Vocabulario visible aportado por un módulo para los entityType de auditoría.
 *
 * Los productores siguen escribiendo únicamente el código técnico. Esta
 * estructura pertenece al lado de lectura/presentación.
 */
export interface AuditEntityVocabulary {
  readonly moduleCode: string;
  readonly labels: Readonly<Record<string, string>>;
}

export type AuditEntityLabels = Readonly<Record<string, string>>;

/**
 * Compone contribuciones modulares en una única tabla de consulta.
 *
 * Una clave repetida es un defecto de configuración, no una precedencia:
 * resolverla por orden escondería qué módulo declaró algo que no le pertenece.
 */
export function composeAuditEntityVocabularies(
  vocabularies: readonly AuditEntityVocabulary[],
): AuditEntityLabels {
  const labels: Record<string, string> = {};
  const owners = new Map<string, string>();

  for (const vocabulary of vocabularies) {
    for (const [entityType, label] of Object.entries(vocabulary.labels)) {
      const previousModule = owners.get(entityType);

      if (previousModule !== undefined) {
        throw new Error(
          `entityType de auditoría duplicado "${entityType}": ` +
            `${previousModule} y ${vocabulary.moduleCode}`,
        );
      }

      owners.set(entityType, vocabulary.moduleCode);
      labels[entityType] = label;
    }
  }

  return Object.freeze(labels);
}

/** Etiqueta humana o, si nadie la declaró, el código técnico exacto. */
export function auditEntityLabel(
  entityType: string,
  labels: AuditEntityLabels,
): string {
  return labels[entityType] ?? entityType;
}
