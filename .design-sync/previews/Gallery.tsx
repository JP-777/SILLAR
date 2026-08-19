import { Badge, Button, EmptyState, Gallery } from 'sillar-frontend';

const THUMB =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='150'%3E%3Crect width='200' height='150' fill='%23E3DCD1'/%3E%3Cpath d='M60 100l25-30 20 22 15-16 20 24H60z' fill='%23A29886'/%3E%3Ccircle cx='75' cy='55' r='12' fill='%23A29886'/%3E%3C/svg%3E";

interface MediaAsset {
  id: string;
  name: string;
  size: string;
  dims: string;
  ownerModule: string;
  date: string;
  isOrphan: boolean;
  isActive: boolean;
}

const archivos: MediaAsset[] = [
  { id: '1', name: 'banner-portada.jpg', size: '212 KB', dims: '1600×600', ownerModule: 'catalogo', date: '18/08/2026', isOrphan: false, isActive: true },
  { id: '2', name: 'logo-sillar.png', size: '18 KB', dims: '512×512', ownerModule: 'core', date: '02/03/2026', isOrphan: false, isActive: true },
  { id: '3', name: 'promo-verano-2025.jpg', size: '340 KB', dims: '1200×800', ownerModule: 'portal', date: '11/12/2025', isOrphan: true, isActive: true },
];

export function ConArchivos() {
  return (
    <Gallery
      items={archivos}
      itemKey={(a) => a.id}
      render={(a) => (
        <article className="gal__item" data-orphan={a.isOrphan} data-dimmed={!a.isActive}>
          <div className="gal__thumb">
            <img src={THUMB} alt="" />
          </div>
          <div className="gal__body">
            <p className="gal__name">{a.name}</p>
            <p className="gal__meta">
              {a.size} · {a.dims}
            </p>
            <p className="gal__meta">
              {a.ownerModule} · {a.date}
            </p>
            {a.isOrphan && <Badge tone="warning">Su módulo ya no está</Badge>}
            <div className="gal__foot">
              <Button size="sm" variant="ghost">
                Copiar enlace
              </Button>
              <Button size="sm" variant="ghost">
                Eliminar
              </Button>
            </div>
          </div>
        </article>
      )}
    />
  );
}

export function Cargando() {
  return <Gallery items={[]} itemKey={(a: MediaAsset) => a.id} render={() => null} loading />;
}

export function Vacio() {
  return (
    <Gallery
      items={[]}
      itemKey={(a: MediaAsset) => a.id}
      render={() => null}
      empty={<EmptyState title="No hay archivos" description="Arrastra una foto arriba para empezar." />}
    />
  );
}
