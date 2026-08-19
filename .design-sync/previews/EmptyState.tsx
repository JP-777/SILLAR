import { Button, EmptyState } from 'sillar-frontend';

export function Simple() {
  return <EmptyState title="No hay archivos" />;
}

export function ConAccion() {
  return (
    <EmptyState
      title="Todavía no hay productos"
      description="Arrastra una foto arriba para empezar, o crea el primero a mano."
      action={<Button size="sm">Crear producto</Button>}
    />
  );
}
