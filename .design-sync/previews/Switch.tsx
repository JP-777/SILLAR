import { Switch } from 'sillar-frontend';

export function Estados() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <Switch checked={true} onChange={() => {}} label="Módulo Catálogo activo" />
      <Switch checked={false} onChange={() => {}} label="Módulo Portal activo" />
    </div>
  );
}

export function Deshabilitado() {
  return <Switch checked={true} onChange={() => {}} label="CORE — siempre activo" disabled />;
}
