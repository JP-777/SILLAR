import { useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Button, EmptyState, Spinner } from '../../../shared/ui';
import { ServiceImage } from '../components/ServiceImage';
import { formatServicePrice, servicesService } from '../services/services';
import '../services.css';

export function ServicePage() {
  const { slug = '' } = useParams();
  const load = useCallback(() => servicesService.getPublic(slug), [slug]);
  const { state, reload } = useResource(load, 'cargar el servicio');
  useDocumentTitle(state.status === 'ready' ? state.data.name : 'Servicio');
  if (useDelayedFlag(state.status === 'loading')) return <main className="sv-public" id="contenido"><Spinner label="Cargando el servicio" /></main>;
  if (state.status === 'error') {
    const missing = state.failure.message.startsWith('Eso ya no existe');
    return <main className="sv-public" id="contenido">{missing ? <EmptyState title="Este servicio no está disponible" description="Puede que se haya retirado o que su dirección haya cambiado." action={<Link to="/servicios">Ver los servicios publicados</Link>} /> : <Alert tone="danger" title="No pudimos cargar el servicio">{state.failure.message}<Button variant="secondary" onClick={() => void reload()}>Volver a intentar</Button></Alert>}</main>;
  }
  if (state.status !== 'ready') return <main className="sv-public" id="contenido" />;
  const item = state.data;
  return <main className="sv-public" id="contenido" tabIndex={-1}><nav><Link to="/servicios">Servicios</Link></nav><article className="sv-detail"><ServiceImage src={item.imageUrl} alt={item.imageAltText} name={item.name} className="sv-detail__image" /><div><h1>{item.name}</h1><p className="sv-price">{formatServicePrice(item.price)}</p>{item.saleUnit && <p className="sv-muted">{item.saleUnit}</p>}{item.shortDescription && <p className="sv-lead">{item.shortDescription}</p>}{item.description && <p className="sv-description">{item.description}</p>}</div></article></main>;
}
