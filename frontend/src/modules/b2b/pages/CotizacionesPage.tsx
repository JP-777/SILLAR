import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { fetchPublicSettings } from '../../../platform/usePublicSettings';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, EmptyState, Field } from '../../../shared/ui';
import { FailureAlert, Table, type Column } from '../../../shared/ui/patterns';
import { presentacionDeEstado } from '../logica/cotizacion';
import { formatearFecha, formatearImporte } from '../logica/formato';
import { cotizaciones } from '../services/b2b';
import type { CotizacionEnBandeja, EstadoCotizacion } from '../services/contratos';
import '../b2b.css';

const ESTADOS: Record<EstadoCotizacion, string> = {
  borrador: 'Borrador', enviada: 'Enviada', aprobada: 'Aprobada', pagada: 'Pagada', anulada: 'Anulada',
};

/**
 * Bandeja de cotizaciones (A6), como estructura para llegar a A8/A9. La
 * identidad del cliente espera la costura de CRM: no se pinta `customerId`.
 */
export function CotizacionesPage() {
  const [filtro, setFiltro] = useState<EstadoCotizacion | ''>('');
  const cargar = useCallback(() => cotizaciones.listar(filtro || undefined), [filtro]);
  const { state } = useResource(cargar, 'cargar las cotizaciones');
  const ajustes = useResource(fetchPublicSettings, 'cargar la moneda');
  const moneda = ajustes.state.status === 'ready' ? ajustes.state.data.currency_code ?? null : null;

  if (state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;

  const columnas: Column<CotizacionEnBandeja>[] = [
    { key: 'numero', header: 'Número', render: (c) => <Link to={`/admin/solicitudes/cotizaciones/${c.id}`}>{c.quoteNumber}</Link> },
    {
      key: 'estado',
      header: 'Estado',
      render: (c) => {
        const p = presentacionDeEstado(c);
        return (
          <span className="b2b-fila">
            <Badge>{p.etiqueta}</Badge>
            {p.condicion && <Badge tone="warning">{p.condicion}</Badge>}
          </span>
        );
      },
    },
    { key: 'total', header: 'Total', align: 'right', render: (c) => formatearImporte(c.totalAmount, moneda) },
    { key: 'fecha', header: 'Creada', render: (c) => formatearFecha(c.createdAt) },
  ];

  return (
    <PageContainer title="Cotizaciones" description="Se crean desde una solicitud y se envían desde tu propio WhatsApp.">
      <div className="b2b-pila">
        <Field label="Estado">
          {(props) => (
            <select className="ui-input" {...props} value={filtro} onChange={(e) => setFiltro(e.target.value as EstadoCotizacion | '')}>
              <option value="">Todos</option>
              {(Object.keys(ESTADOS) as EstadoCotizacion[]).map((e) => <option key={e} value={e}>{ESTADOS[e]}</option>)}
            </select>
          )}
        </Field>
        {state.status === 'error' && <FailureAlert failure={state.failure} />}
        <Table
          columns={columnas}
          rows={state.status === 'ready' ? state.data.filter((c) => c.isActive) : []}
          rowKey={(c) => c.id}
          loading={state.status === 'loading'}
          empty={<EmptyState title={filtro ? 'Ninguna cotización en ese estado' : 'Todavía no hay cotizaciones'} description={filtro ? 'Prueba con otro estado.' : 'Se crean desde una solicitud personalizada o por volumen.'} />}
        />
      </div>
    </PageContainer>
  );
}
