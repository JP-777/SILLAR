import { http } from '../../../shared/http/client';

/** Una configuración del sitio, tal como la lista el panel. */
export interface Setting {
  key: string;
  value: string;
  /** `text`, `number`, `boolean`, `url`, `email` o `json`. */
  valueType: string;
  description: string | null;
  isPublic: boolean;
  isActive: boolean;
  /** Sigue con el valor del seed: nadie la ha configurado todavía. */
  needsSetup: boolean;
  updatedAt: string;
}

export const settingsService = {
  list: () => http.get<Setting[]>('/admin/settings'),

  /**
   * Cambia el valor y, opcionalmente, la visibilidad.
   *
   * Omitir `isPublic` deja la visibilidad como estaba. Cambiarla exige
   * `super_admin`; cambiar solo el valor, `admin`.
   */
  update: (key: string, value: string, isPublic?: boolean) =>
    http.put<Setting>(`/admin/settings/${encodeURIComponent(key)}`, { value, isPublic }),
};

/** Grupos en los que se reparten las claves en la pantalla. */
export interface SettingGroup {
  readonly title: string;
  readonly description: string;
  readonly keys: readonly string[];
}

/**
 * Reparto por área.
 *
 * Vive en el frontend a propósito: **no se añade una columna a la base de datos
 * para agrupar una pantalla.** Si mañana el reparto cambia, cambia aquí y ya.
 *
 * Lo que no encaje en ningún grupo cae en «Otros ajustes», así que una clave
 * nueva de un módulo aparece igual aunque nadie la haya clasificado.
 */
export const SETTING_GROUPS: readonly SettingGroup[] = [
  {
    title: 'El negocio',
    description: 'Cómo se presenta tu negocio en la web.',
    keys: ['business_name', 'main_message', 'business_hours'],
  },
  {
    title: 'Contacto',
    description: 'Por dónde te escriben y te llaman los clientes.',
    keys: ['whatsapp_number', 'contact_email', 'contact_phone'],
  },
  {
    title: 'Ubicación',
    description: 'Dónde encontrarte.',
    keys: ['business_address', 'business_reference', 'google_maps_url'],
  },
  {
    title: 'Moneda',
    description: 'Cómo se muestran los precios.',
    keys: ['currency_code', 'currency_symbol'],
  },
];

/** Reparte las claves en sus grupos, dejando las desconocidas al final. */
export function groupSettings(settings: readonly Setting[]): { title: string; description: string; items: Setting[] }[] {
  const byKey = new Map(settings.map((setting) => [setting.key, setting]));
  const groups: { title: string; description: string; items: Setting[] }[] = [];

  for (const group of SETTING_GROUPS) {
    const items = group.keys
      .map((key) => byKey.get(key))
      .filter((setting): setting is Setting => setting !== undefined);

    for (const item of items) {
      byKey.delete(item.key);
    }

    if (items.length > 0) {
      groups.push({ title: group.title, description: group.description, items });
    }
  }

  const remaining = [...byKey.values()].sort((a, b) => a.key.localeCompare(b.key, 'es'));

  if (remaining.length > 0) {
    groups.push({
      title: 'Otros ajustes',
      description: 'Configuraciones aportadas por los módulos instalados.',
      items: remaining,
    });
  }

  return groups;
}
