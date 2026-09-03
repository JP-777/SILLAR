import type { CSSProperties } from 'react';
import { Link } from 'react-router-dom';
import { useHomeContribution, type EstadoAporte } from '../../platform/homeContributions';
import type { HomeSection } from '../../platform/homeSections';
import { useDelayedFlag } from '../../shared/hooks/useDelayedFlag';
import { Alert, Badge, Button, Spinner } from '../../shared/ui';
import { formatFeaturedProductPrice } from './components/featuredProductPresentation';
import { publicBannersService, type PublicBanner } from './services/banners';
import {
  publicFeaturedProductsService,
  type PublicFeaturedProduct,
} from './services/featuredProducts';
import {
  publicFeaturedProjectsService,
  type PublicFeaturedProject,
} from './services/featuredProjects';
import { publicPromotionsService, type PublicPromotion } from './services/promotions';
import { useResource, type ResourceState } from '../../shared/hooks/useResource';

/** La única contribución de M02 a la portada; el registro central decide su posición. */
export const cmsHome: HomeSection = {
  moduleCode: 'cms',
  Component: CmsHomeSection,
};

/**
 * En qué queda un bloque, para que la portada sepa si pintó algo.
 *
 * **Un fallo cuenta como contenido**, y no es un descuido: `FailedBlock` pinta
 * una sección con su título y su aviso, así que la portada **no está vacía**.
 * Enseñar además «todavía no hay contenido publicado» encima de un error sería
 * dar dos explicaciones distintas del mismo hueco, y la falsa debajo.
 */
function aporteDe<T>(state: ResourceState<readonly T[]>): EstadoAporte {
  if (state.status === 'loading') {
    return 'cargando';
  }

  if (state.status === 'ready') {
    return state.data.length === 0 ? 'vacio' : 'con-contenido';
  }

  return 'con-contenido';
}

/** Compone los cuatro bloques públicos de CMS sin convertirlos en cuatro HomeSection. */
function CmsHomeSection() {
  return (
    <>
      <BannersBlock />
      <PromotionsBlock />
      <FeaturedProductsBlock />
      <FeaturedProjectsBlock />
    </>
  );
}

function BannersBlock() {
  const { state, reload } = useResource(publicBannersService.list, 'cargar los banners publicados');
  const showLoading = useDelayedFlag(state.status === 'loading');

  // Se declara **antes de cualquier salida temprana**: un hook no puede
  // quedar detrás de un `return`, y además el estado que hay que declarar es
  // justamente el que provoca esas salidas.
  useHomeContribution(aporteDe(state));

  if (state.status === 'loading') {
    return showLoading
      ? <LoadingBlock id="cms-banners" title="Novedades" label="Cargando banners" />
      : null;
  }

  if (state.status === 'error' || state.status === 'forbidden') {
    return (
      <FailedBlock
        id="cms-banners"
        title="Novedades"
        message={state.failure.message}
        onRetry={reload}
      />
    );
  }

  if (state.data.length === 0) return null;

  return (
    <section aria-labelledby="cms-banners-title" style={sectionStyle}>
      <h2 id="cms-banners-title">Novedades</h2>
      <ul style={wideListStyle}>
        {state.data.map((banner, index) => (
          <BannerCard key={banner.id} banner={banner} eager={index === 0} />
        ))}
      </ul>
    </section>
  );
}

function BannerCard({ banner, eager }: { banner: PublicBanner; eager: boolean }) {
  return (
    <li style={cardStyle}>
      <article>
        <picture>
          {banner.imageMobileUrl && (
            <source media="(max-width: 640px)" srcSet={banner.imageMobileUrl} />
          )}
          <img
            src={banner.imageDesktopUrl}
            alt={banner.altText}
            loading={eager ? 'eager' : 'lazy'}
            style={wideImageStyle}
          />
        </picture>
        <div style={bodyStyle}>
          {banner.title && <h3 style={titleStyle}>{banner.title}</h3>}
          {banner.subtitle && <p style={textStyle}>{banner.subtitle}</p>}
          <OptionalContentLink url={banner.linkUrl} label={banner.linkLabel} />
        </div>
      </article>
    </li>
  );
}

function PromotionsBlock() {
  const { state, reload } = useResource(
    publicPromotionsService.list,
    'cargar las promociones publicadas',
  );
  const showLoading = useDelayedFlag(state.status === 'loading');

  // Se declara **antes de cualquier salida temprana**: un hook no puede
  // quedar detrás de un `return`, y además el estado que hay que declarar es
  // justamente el que provoca esas salidas.
  useHomeContribution(aporteDe(state));

  if (state.status === 'loading') {
    return showLoading
      ? <LoadingBlock id="cms-promotions" title="Promociones" label="Cargando promociones" />
      : null;
  }

  if (state.status === 'error' || state.status === 'forbidden') {
    return (
      <FailedBlock
        id="cms-promotions"
        title="Promociones"
        message={state.failure.message}
        onRetry={reload}
      />
    );
  }

  if (state.data.length === 0) return null;

  return (
    <section aria-labelledby="cms-promotions-title" style={sectionStyle}>
      <h2 id="cms-promotions-title">Promociones</h2>
      <ul style={gridStyle}>
        {state.data.map((promotion) => (
          <PromotionCard key={promotion.id} promotion={promotion} />
        ))}
      </ul>
    </section>
  );
}

function PromotionCard({ promotion }: { promotion: PublicPromotion }) {
  return (
    <li style={cardStyle}>
      <article>
        {promotion.imageUrl && (
          <img
            src={promotion.imageUrl}
            alt={promotion.altText ?? ''}
            loading="lazy"
            style={cardImageStyle}
          />
        )}
        <div style={bodyStyle}>
          {promotion.badgeText && <Badge>{promotion.badgeText}</Badge>}
          {promotion.title && <h3 style={titleStyle}>{promotion.title}</h3>}
          {promotion.subtitle && <p style={subtitleStyle}>{promotion.subtitle}</p>}
          {promotion.description && <p style={textStyle}>{promotion.description}</p>}
          <OptionalContentLink url={promotion.linkUrl} label={promotion.linkLabel} />
        </div>
      </article>
    </li>
  );
}

function FeaturedProductsBlock() {
  const { state, reload } = useResource(
    publicFeaturedProductsService.list,
    'cargar los productos destacados',
  );
  const showLoading = useDelayedFlag(state.status === 'loading');

  // Se declara **antes de cualquier salida temprana**: un hook no puede
  // quedar detrás de un `return`, y además el estado que hay que declarar es
  // justamente el que provoca esas salidas.
  useHomeContribution(aporteDe(state));

  if (state.status === 'loading') {
    return showLoading
      ? (
          <LoadingBlock
            id="cms-featured-products"
            title="Productos destacados"
            label="Cargando productos destacados"
          />
        )
      : null;
  }

  if (state.status === 'error' || state.status === 'forbidden') {
    return (
      <FailedBlock
        id="cms-featured-products"
        title="Productos destacados"
        message={state.failure.message}
        onRetry={reload}
      />
    );
  }

  if (state.data.length === 0) return null;

  return (
    <section aria-labelledby="cms-featured-products-title" style={sectionStyle}>
      <h2 id="cms-featured-products-title">Productos destacados</h2>
      <ul style={gridStyle}>
        {state.data.map((product) => (
          <FeaturedProductCard key={product.id} product={product} />
        ))}
      </ul>
    </section>
  );
}

function FeaturedProductCard({ product }: { product: PublicFeaturedProduct }) {
  const name = product.productSlug
    ? <Link to={`/producto/${product.productSlug}`}>{product.productName}</Link>
    : product.productName;
  const price = formatFeaturedProductPrice(product.productPrice);

  return (
    <li style={cardStyle}>
      <article>
        {product.imageUrl && (
          <img src={product.imageUrl} alt="" loading="lazy" style={cardImageStyle} />
        )}
        <div style={bodyStyle}>
          {product.productCategory && <p style={eyebrowStyle}>{product.productCategory}</p>}
          <h3 style={titleStyle}>{name}</h3>
          <p style={priceStyle}>
            {product.productPriceVaries && product.productPrice !== null ? `Desde ${price}` : price}
          </p>
        </div>
      </article>
    </li>
  );
}

function FeaturedProjectsBlock() {
  const { state, reload } = useResource(
    publicFeaturedProjectsService.list,
    'cargar los trabajos destacados',
  );
  const showLoading = useDelayedFlag(state.status === 'loading');

  // Se declara **antes de cualquier salida temprana**: un hook no puede
  // quedar detrás de un `return`, y además el estado que hay que declarar es
  // justamente el que provoca esas salidas.
  useHomeContribution(aporteDe(state));

  if (state.status === 'loading') {
    return showLoading
      ? (
          <LoadingBlock
            id="cms-featured-projects"
            title="Trabajos destacados"
            label="Cargando trabajos destacados"
          />
        )
      : null;
  }

  if (state.status === 'error' || state.status === 'forbidden') {
    return (
      <FailedBlock
        id="cms-featured-projects"
        title="Trabajos destacados"
        message={state.failure.message}
        onRetry={reload}
      />
    );
  }

  if (state.data.length === 0) return null;

  return (
    <section aria-labelledby="cms-featured-projects-title" style={sectionStyle}>
      <h2 id="cms-featured-projects-title">Trabajos destacados</h2>
      <ul style={gridStyle}>
        {state.data.map((project) => (
          <FeaturedProjectCard key={project.id} project={project} />
        ))}
      </ul>
    </section>
  );
}

function FeaturedProjectCard({ project }: { project: PublicFeaturedProject }) {
  return (
    <li style={cardStyle}>
      <article>
        <img src={project.imageUrl} alt={project.altText} loading="lazy" style={cardImageStyle} />
        <div style={bodyStyle}>
          <h3 style={titleStyle}>{project.title}</h3>
          {project.description && <p style={textStyle}>{project.description}</p>}
        </div>
      </article>
    </li>
  );
}

function OptionalContentLink({ url, label }: { url: string | null; label: string | null }) {
  if (!url || !label) return null;
  return <a href={url}>{label}</a>;
}

function LoadingBlock({ id, title, label }: { id: string; title: string; label: string }) {
  return (
    <section aria-labelledby={`${id}-title`} style={sectionStyle}>
      <h2 id={`${id}-title`}>{title}</h2>
      <Spinner label={label} />
    </section>
  );
}

function FailedBlock({
  id,
  title,
  message,
  onRetry,
}: {
  id: string;
  title: string;
  message: string;
  onRetry: () => Promise<void>;
}) {
  return (
    <section aria-labelledby={`${id}-title`} style={sectionStyle}>
      <h2 id={`${id}-title`}>{title}</h2>
      <Alert tone="danger">{message}</Alert>
      <Button variant="secondary" onClick={() => void onRetry()}>Volver a intentar</Button>
    </section>
  );
}

const sectionStyle: CSSProperties = {
  display: 'grid',
  gap: 'var(--s4)',
  marginBlock: 'var(--s8)',
};

const gridStyle: CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 15rem), 1fr))',
  gap: 'var(--s4)',
  listStyle: 'none',
  margin: 0,
  padding: 0,
};

const wideListStyle: CSSProperties = {
  display: 'grid',
  gap: 'var(--s4)',
  listStyle: 'none',
  margin: 0,
  padding: 0,
};

const cardStyle: CSSProperties = {
  overflow: 'hidden',
  border: '1px solid var(--border)',
  borderRadius: 'var(--r-lg)',
  background: 'var(--bg-raised)',
};

const bodyStyle: CSSProperties = {
  display: 'grid',
  gap: 'var(--s2)',
  padding: 'var(--s4)',
};

const wideImageStyle: CSSProperties = {
  display: 'block',
  width: '100%',
  maxHeight: '28rem',
  objectFit: 'cover',
};

const cardImageStyle: CSSProperties = {
  display: 'block',
  width: '100%',
  aspectRatio: '4 / 3',
  objectFit: 'cover',
};

const titleStyle: CSSProperties = { margin: 0 };
const subtitleStyle: CSSProperties = { margin: 0, fontWeight: 600 };
const textStyle: CSSProperties = { margin: 0, color: 'var(--text-muted)' };
const eyebrowStyle: CSSProperties = {
  margin: 0,
  color: 'var(--text-muted)',
  fontSize: '13px',
};
const priceStyle: CSSProperties = { margin: 0, fontWeight: 700 };
