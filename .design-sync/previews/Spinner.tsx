import { Spinner } from 'sillar-frontend';

export function Tamanos() {
  return (
    <div style={{ display: 'flex', gap: 20, alignItems: 'center' }}>
      <Spinner size="sm" />
      <Spinner size="md" />
      <Spinner size="lg" />
    </div>
  );
}

export function ConEtiqueta() {
  return <Spinner size="lg" label="Cargando archivos" />;
}
