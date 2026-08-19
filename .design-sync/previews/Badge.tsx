import { Badge } from 'sillar-frontend';

export function Tonos() {
  return (
    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
      <Badge tone="neutral">De baja</Badge>
      <Badge tone="success">Activo</Badge>
      <Badge tone="warning">Su módulo ya no está</Badge>
      <Badge tone="danger">Vencido</Badge>
    </div>
  );
}

export function EnContexto() {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <span style={{ fontWeight: 560 }}>banner-portada.jpg</span>
      <Badge tone="warning">Su módulo ya no está</Badge>
    </div>
  );
}
