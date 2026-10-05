import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../../../shared/a11y/useDocumentTitle';
import { useDelayedFlag } from '../../../shared/hooks/useDelayedFlag';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Button, EmptyState, Spinner } from '../../../shared/ui';
import { ServiceImage } from '../components/ServiceImage';
import { formatServicePrice, servicesService } from '../services/services';
import '../services.css';

export function ServicesPage() {
  useDocumentTitle('Servicios');
  const load = useCallback(() => servicesService.listPublic(), []);
  const { state, reload } = useResource(load, 'cargar los servicios');
  const loading = useDelayedFlag(state.status === 'loading');
  return <main className="sv-public" id="contenido" tabIndex={-1}>
    <header><h1>Servicios</h1><p className="sv-lead">Conoce los servicios que ofrecemos.</p></header>
    {loading && <Spinner label="Cargando los servicios" />}
    {state.status === 'error' && <Alert tone="danger" title="No pudimos cargar los servicios">{state.failure.message}<Button variant="secondary" onClick={() => void reload()}>Volver a intentar</Button></Alert>}
    {state.status === 'ready' && state.data.length === 0 && <EmptyState title="Todavía no hay servicios publicados" description="Cuando haya alguno disponible aparecerá aquí." />}
    {state.status === 'ready' && state.data.length > 0 && <ul className="sv-grid">{state.data.map((item) => <li className="sv-card" key={item.id}><Link to={`/servicios/${item.slug}`}><ServiceImage src={item.imageUrl} alt={item.imageAltText} name={item.name} className="sv-card__image" /><div className="sv-card__body"><h2>{item.name}</h2>{item.shortDescription && <p>{item.shortDescription}</p>}<strong>{formatServicePrice(item.price)}</strong>{item.saleUnit && <span className="sv-muted">{item.saleUnit}</span>}</div></Link></li>)}</ul>}
  </main>;
}
