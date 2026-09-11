import type { Role } from './SessionProvider';

export interface RoleVocabularyEntry {
  readonly label: string;
  readonly description: string;
}

export const ROLE_VOCABULARY: Readonly<Record<Role, RoleVocabularyEntry>> = {
  super_admin: {
    label: 'Administrador principal',
    description: 'gestiona usuarios y módulos',
  },
  admin: {
    label: 'Administrador',
    description: 'configura el negocio',
  },
  editor: {
    label: 'Editor',
    description: 'edita contenido y sube archivos',
  },
};

export function roleLabel(role: Role): string {
  return ROLE_VOCABULARY[role].label;
}

export function roleFormLabel(role: Role): string {
  const vocabulary = ROLE_VOCABULARY[role];
  return `${vocabulary.label} — ${vocabulary.description}`;
}
