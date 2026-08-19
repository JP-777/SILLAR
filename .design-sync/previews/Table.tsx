import { Badge, Table, type Column } from 'sillar-frontend';

interface Producto {
  id: string;
  nombre: string;
  sku: string;
  precio: string;
  activo: boolean;
}

const productos: Producto[] = [
  { id: '1', nombre: 'Cuaderno universitario cuadriculado Stanford A4 100 hojas', sku: 'CUAD-A4-100', precio: 'S/ 12.50', activo: true },
  { id: '2', nombre: 'Lapicero de gel punta fina Faber-Castell azul', sku: 'LAP-GEL-AZ', precio: 'S/ 3.20', activo: true },
  { id: '3', nombre: 'Mochila escolar reforzada Totto 20L', sku: 'MOCH-TOT-20', precio: 'S/ 89.90', activo: false },
];

const columnas: readonly Column<Producto>[] = [
  { key: 'nombre', header: 'Producto', render: (p) => p.nombre },
  { key: 'sku', header: 'SKU', render: (p) => p.sku },
  { key: 'precio', header: 'Precio', align: 'right', render: (p) => p.precio },
  {
    key: 'estado',
    header: 'Estado',
    render: (p) => <Badge tone={p.activo ? 'success' : 'neutral'}>{p.activo ? 'Activo' : 'De baja'}</Badge>,
  },
];

export function ConDatos() {
  return <Table columns={columnas} rows={productos} rowKey={(p) => p.id} dimmed={(p) => !p.activo} />;
}

export function Cargando() {
  return <Table columns={columnas} rows={[]} rowKey={(p) => p.id} loading />;
}

export function Vacio() {
  return <Table columns={columnas} rows={[]} rowKey={(p) => p.id} />;
}

export function ConPaginacion() {
  return (
    <Table
      columns={columnas}
      rows={productos}
      rowKey={(p) => p.id}
      pagination={{ page: 2, totalPages: 5, totalItems: 47, onChange: () => {} }}
    />
  );
}
