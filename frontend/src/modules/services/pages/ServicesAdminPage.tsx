import { useCallback, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState } from '../../../shared/ui';
import { ConfirmDialog, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { ServiceForm } from '../components/ServiceForm';
import { formatServicePrice, servicesService, type AdminService, type PublicationState } from '../services/services';
import { reorderedIds } from '../state/reorder';
import '../services.css';

const labels: Record<PublicationState, string> = { Draft: 'Borrador', Published: 'Publicado', Archived: 'Archivado' };

export function ServicesAdminPage() {
  const load = useCallback(() => servicesService.listAdmin(), []);
  const { state, reload } = useResource(load, 'cargar los servicios');
  const [editing, setEditing] = useState<AdminService | null | undefined>(undefined);
  const [transition, setTransition] = useState<{ service: AdminService; action: 'publish' | 'unpublish' | 'archive' } | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const { toasts, show } = useToasts();
  const rows = state.status === 'ready' ? state.data : [];
  const showLoading = useDelayedFlag(state.status === 'loading');

  async function move(from: number, to: number) {
    setBusy(true); setFailure(null);
    try { await servicesService.reorder(reorderedIds(rows, from, to)); show('Se actualizó el orden de la vitrina.'); await reload(); }
    catch (error) { setFailure(describe(error, 'reordenar los servicios')); }
    finally { setBusy(false); }
  }

  async function applyTransition() {
    if (!transition) return;
    setBusy(true); setFailure(null);
    try {
      await servicesService[transition.action](transition.service.id);
      show('Se actualizó el estado editorial del servicio.'); setTransition(null); await reload();
    } catch (error) { setFailure(describe(error, 'cambiar el estado editorial')); }
    finally { setBusy(false); }
  }

  const columns: Column<AdminService>[] = [
    { key: 'service', header: 'Servicio', render: (item) => <div><strong>{item.name}</strong><div className="sv-muted">/servicios/{item.slug}</div></div> },
    { key: 'price', header: 'Precio', render: (item) => <div>{formatServicePrice(item.price)}{item.saleUnit && <div className="sv-muted">{item.saleUnit}</div>}</div> },
    { key: 'state', header: 'Estado', render: (item) => <Badge tone={item.publicationState === 'Published' ? 'success' : item.publicationState === 'Archived' ? 'neutral' : 'warning'}>{labels[item.publicationState]}</Badge> },
    { key: 'order', header: 'Orden', render: (item) => { const index = rows.findIndex((x) => x.id === item.id); return <div className="sv-actions"><Button size="sm" variant="secondary" disabled={busy || index === 0} onClick={() => void move(index, index - 1)} aria-label={`Subir ${item.name}`}>Subir</Button><Button size="sm" variant="secondary" disabled={busy || index === rows.length - 1} onClick={() => void move(index, index + 1)} aria-label={`Bajar ${item.name}`}>Bajar</Button></div>; } },
    { key: 'actions', header: 'Acciones', align: 'right', render: (item) => <div className="sv-actions"><Button size="sm" variant="secondary" onClick={() => setEditing(item)}>Editar</Button>{item.publicationState === 'Draft' && <Button size="sm" onClick={() => setTransition({ service: item, action: 'publish' })}>Publicar</Button>}{item.publicationState === 'Published' && <Button size="sm" variant="ghost" onClick={() => setTransition({ service: item, action: 'unpublish' })}>Despublicar</Button>}{item.publicationState !== 'Archived' && <Button size="sm" variant="ghost" onClick={() => setTransition({ service: item, action: 'archive' })}>Archivar</Button>}</div> },
  ];

  if (state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;
  return <PageContainer title="Servicios" description="Mantén y publica la vitrina de servicios permanentes." actions={<Button onClick={() => setEditing(null)}>Nuevo servicio</Button>}>
    <FailureAlert failure={failure} />
    {state.status === 'error' ? <><FailureAlert failure={state.failure} /><Button variant="secondary" onClick={() => void reload()}>Volver a intentar</Button></> :
      <Table columns={columns} rows={rows} rowKey={(item) => item.id} dimmed={(item) => item.publicationState === 'Archived'} loading={showLoading} empty={<EmptyState title="Todavía no hay servicios" description="Crea el primero para preparar la vitrina." action={<Button onClick={() => setEditing(null)}>Crear el primer servicio</Button>} />} />}
    {editing !== undefined && <ServiceForm service={editing} onClose={() => setEditing(undefined)} onSaved={(saved) => { setEditing(undefined); show(`Se guardó «${saved.name}».`); void reload(); }} />}
    <ConfirmDialog open={transition !== null} title={`${transition ? labelsForAction(transition.action) : ''} servicio`} confirmLabel={transition ? labelsForAction(transition.action) : 'Confirmar'} danger={transition?.action === 'archive'} busy={busy} onConfirm={() => void applyTransition()} onCancel={() => setTransition(null)}><p>El cambio afecta a la visibilidad pública según el estado elegido.</p></ConfirmDialog>
    <Toasts toasts={toasts} />
  </PageContainer>;
}

function labelsForAction(action: 'publish' | 'unpublish' | 'archive') { return action === 'publish' ? 'Publicar' : action === 'unpublish' ? 'Despublicar' : 'Archivar'; }
