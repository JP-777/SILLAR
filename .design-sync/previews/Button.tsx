import { Button } from 'sillar-frontend';

export function Variantes() {
  return (
    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
      <Button variant="primary">Guardar cambios</Button>
      <Button variant="secondary">Cancelar</Button>
      <Button variant="ghost">Ver detalle</Button>
      <Button variant="danger">Eliminar usuario</Button>
    </div>
  );
}

export function Tamanos() {
  return (
    <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
      <Button size="sm">Pequeño</Button>
      <Button size="md">Mediano</Button>
      <Button size="lg">Grande</Button>
    </div>
  );
}

export function Estados() {
  return (
    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
      <Button loading>Guardando…</Button>
      <Button disabled>No disponible</Button>
      <Button variant="secondary" disabled>
        Cancelar
      </Button>
    </div>
  );
}

export function AnchoCompleto() {
  return (
    <div style={{ maxWidth: 320 }}>
      <Button block>Activar módulo Catálogo</Button>
    </div>
  );
}
