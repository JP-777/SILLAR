import { Badge, Button, Card } from 'sillar-frontend';

export function Simple() {
  return (
    <div style={{ maxWidth: 380 }}>
      <Card title="Catálogo">
        <p style={{ margin: 0, color: 'var(--text-muted)' }}>128 productos activos, 4 categorías.</p>
      </Card>
    </div>
  );
}

export function ConSubtitulo() {
  return (
    <div style={{ maxWidth: 380 }}>
      <Card title="Módulo Ventas" subtitle="Punto de venta y comprobantes">
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <Badge tone="success">Activo</Badge>
          <span style={{ color: 'var(--text-subtle)', fontSize: 13 }}>desde 12 mar 2026</span>
        </div>
      </Card>
    </div>
  );
}

export function ConAccion() {
  return (
    <div style={{ maxWidth: 380 }}>
      <Card title="Copia de seguridad" subtitle="Última ejecución: hace 3 horas">
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Button size="sm" variant="secondary">
            Ejecutar ahora
          </Button>
        </div>
      </Card>
    </div>
  );
}
