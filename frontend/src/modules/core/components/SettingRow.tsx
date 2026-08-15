import { useState } from 'react';
import { Badge, Button, Input, Switch } from '../../../shared/ui';
import type { Setting } from '../services/settings';
import './settings.css';

/** Valor con el que el seed marca lo que el negocio debe completar. */
const PENDING = 'PENDIENTE_DEFINIR';

interface SettingRowProps {
  setting: Setting;
  /** Si quien mira puede cambiar la visibilidad. Solo `super_admin`. */
  canPublish: boolean;
  busy: boolean;
  error: string | null;
  onSave: (value: string) => void;
  onTogglePublic: () => void;
}

/**
 * Una configuración.
 *
 * El valor y la visibilidad se editan por separado a propósito: publicar un dato
 * es de otra naturaleza que corregir un teléfono, y mezclarlos en un único
 * «guardar» haría que se publicara algo de paso.
 */
export function SettingRow({
  setting,
  canPublish,
  busy,
  error,
  onSave,
  onTogglePublic,
}: SettingRowProps) {
  const pending = setting.value === PENDING;
  const [draft, setDraft] = useState(pending ? '' : setting.value);

  const changed = draft.trim() !== (pending ? '' : setting.value);

  return (
    <div className="set-row" data-pending={pending}>
      <div className="set-row__head">
        <div>
          {/* La descripción, no la clave: «whatsapp_number» no le dice nada a
              nadie, «Número de WhatsApp para pedidos» sí. */}
          <p className="set-row__label">{setting.description ?? setting.key}</p>
          <code className="set-row__key">{setting.key}</code>
        </div>

        {pending && <Badge tone="warning">Sin definir</Badge>}
      </div>

      <div className="set-row__control">
        <ValueInput
          setting={setting}
          value={draft}
          onChange={setDraft}
          disabled={busy}
        />

        <Button
          size="sm"
          onClick={() => onSave(draft.trim())}
          disabled={!changed || draft.trim() === ''}
          loading={busy}
        >
          Guardar
        </Button>
      </div>

      {error && (
        <p className="set-row__error" role="alert">
          {error}
        </p>
      )}

      <div className="set-row__visibility">
        <Switch
          checked={setting.isPublic}
          onChange={onTogglePublic}
          // Deshabilitado con la razón, no oculto: ocultarlo haría creer que el
          // dato no es público cuando puede serlo.
          disabled={!canPublish || busy}
          label={
            setting.isPublic
              ? 'Visible en la web pública'
              : 'Solo visible en el panel'
          }
        />

        {!canPublish && (
          <span className="set-row__hint">
            Cambiar esto exige el rol de administrador principal.
          </span>
        )}
      </div>
    </div>
  );
}

/** El control que corresponde al tipo declarado de la clave. */
function ValueInput({
  setting,
  value,
  onChange,
  disabled,
}: {
  setting: Setting;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
}) {
  const common = {
    value,
    disabled,
    onChange: (event: { target: { value: string } }) => onChange(event.target.value),
    'aria-label': setting.description ?? setting.key,
  };

  switch (setting.valueType) {
    case 'number':
      return <Input {...common} type="number" inputMode="decimal" />;

    case 'email':
      return <Input {...common} type="email" autoComplete="off" />;

    case 'url':
      return <Input {...common} type="url" placeholder="https://" />;

    case 'boolean':
      return (
        <select {...common} className="ui-input">
          <option value="true">Sí</option>
          <option value="false">No</option>
        </select>
      );

    case 'json':
      // Texto con validación de sintaxis, que la hace el servidor. Aquí solo se
      // le da sitio para escribir varias líneas.
      return (
        <textarea
          {...common}
          className="ui-input set-row__json"
          rows={4}
          spellCheck={false}
        />
      );

    default:
      return <Input {...common} type="text" />;
  }
}
