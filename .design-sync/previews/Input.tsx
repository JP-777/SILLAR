import { Input } from 'sillar-frontend';

export function Normal() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, maxWidth: 320 }}>
      <Input placeholder="Buscar productos…" />
      <Input defaultValue="admin@sillar.pe" />
    </div>
  );
}

export function Invalido() {
  return (
    <div style={{ maxWidth: 320 }}>
      <Input defaultValue="CUAD-A4" invalid aria-invalid />
    </div>
  );
}

export function Deshabilitado() {
  return (
    <div style={{ maxWidth: 320 }}>
      <Input defaultValue="No editable" disabled />
    </div>
  );
}
