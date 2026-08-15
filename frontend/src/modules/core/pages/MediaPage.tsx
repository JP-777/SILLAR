import { useCallback, useMemo, useState } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { useCapability } from '../../../capabilities/useCapability';
import { describe } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Badge, Button, Card, EmptyState, Field, Input } from '../../../shared/ui';
import { Gallery } from '../../../shared/ui/Gallery';
import { ConfirmDialog, Pagination, Toasts, useToasts } from '../../../shared/ui/patterns';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { useSession } from '../../../session';
import { MediaUploader } from '../components/MediaUploader';
import {
  formatSize,
  mediaService,
  type MediaAsset,
  type MediaQuery,
} from '../services/media';
import '../../../shared/ui/gallery.css';

/**
 * Galería de medios.
 *
 * Galería y no tabla: los medios son visuales, y una lista de nombres de
 * archivo obliga a abrir cada uno para saber cuál es.
 */
export function MediaPage() {
  const { modules } = useCapability();
  const { hasRole } = useSession();
  const { toasts, show } = useToasts();

  const canDelete = hasRole('admin');
  const moduleCodes = useMemo(() => modules.map((module) => module.code), [modules]);

  const [filters, setFilters] = useState<MediaQuery>({});
  const [page, setPage] = useState(1);
  const [owner, setOwner] = useState('core');
  const [uploading, setUploading] = useState(false);
  const [notices, setNotices] = useState<{ tone: 'warning' | 'danger'; text: string }[]>([]);
  const [duplicate, setDuplicate] = useState<{ id: number; of: number } | null>(null);
  const [pendingDeletion, setPendingDeletion] = useState<MediaAsset | null>(null);
  const [busy, setBusy] = useState(false);

  const query = useMemo<MediaQuery>(() => ({ ...filters, page }), [filters, page]);
  const load = useCallback(() => mediaService.list(query), [query]);
  const { state, reload } = useResource(load, 'cargar los archivos');

  const result = state.status === 'ready' ? state.data : null;

  function apply(change: Partial<MediaQuery>) {
    setFilters((current) => ({ ...current, ...change }));
    setPage(1);
  }

  async function upload(files: File[], rejected: string[]) {
    // Los rechazados por la comprobación de cortesía se avisan sin haber salido
    // a la red.
    setNotices(rejected.map((text) => ({ tone: 'danger' as const, text })));
    setDuplicate(null);

    if (files.length === 0) {
      return;
    }

    setUploading(true);

    for (const file of files) {
      try {
        const uploaded = await mediaService.upload(file, owner);

        if (uploaded.duplicateOf !== null) {
          // No es un error: el archivo se subió. La entrega 3b decidió detectar
          // duplicados sin fusionarlos, y presentarlo como fallo contradiría esa
          // decisión y confundiría a quien acaba de hacer algo correcto.
          setDuplicate({ id: uploaded.mediaAssetId, of: uploaded.duplicateOf });
        } else {
          show(`Se subió «${uploaded.originalName ?? file.name}».`);
        }
      } catch (error) {
        // 413 y 415 llegan con la frase concreta que redactó el servidor: uno
        // dice cuánto pesa de más, el otro qué formatos se aceptan.
        const failure = describe(error, 'subir el archivo');

        if (failure.kind !== 'silent') {
          setNotices((current) => [...current, { tone: 'danger', text: failure.message }]);
        }
      }
    }

    setUploading(false);
    await reload();
  }

  async function remove() {
    if (!pendingDeletion) {
      return;
    }

    setBusy(true);

    try {
      await mediaService.deactivate(pendingDeletion.mediaAssetId);
      show(`«${pendingDeletion.originalName ?? 'El archivo'}» dejó de servirse.`);
      setPendingDeletion(null);
      await reload();
    } catch (error) {
      const failure = describe(error, 'dar de baja el archivo');

      if (failure.kind !== 'silent') {
        setNotices([{ tone: 'danger', text: failure.message }]);
      }

      setPendingDeletion(null);
    } finally {
      setBusy(false);
    }
  }

  if (state.status === 'forbidden') {
    return <ForbiddenPage minimum="editor" />;
  }

  const orphans = result?.items.filter((asset) => asset.isOrphan).length ?? 0;

  return (
    <PageContainer
      title="Archivos"
      description="Las imágenes que usan tus módulos. Se guardan una vez y se referencian desde donde haga falta."
    >
      <Card title="Subir imágenes">
        <MediaUploader
          moduleCodes={moduleCodes}
          ownerModuleCode={owner}
          onOwnerChange={setOwner}
          busy={uploading}
          onFiles={(files, rejected) => void upload(files, rejected)}
        />
      </Card>

      {notices.map((notice, index) => (
        <Alert key={index} tone={notice.tone}>
          {notice.text}
        </Alert>
      ))}

      {duplicate && (
        <Alert tone="warning" title="Ya tenías este archivo">
          Se subió igualmente, pero el contenido coincide con otro que ya estaba. Puedes usar
          cualquiera de los dos, o dar de baja el nuevo.
        </Alert>
      )}

      {/* Los huérfanos se destacan Y se explican: «huérfano» no significa nada
          para quien administra una librería. */}
      {orphans > 0 && (
        <Alert tone="warning" title={`${orphans} archivo${orphans === 1 ? '' : 's'} sin dueño`}>
          El módulo que los subió ya no está instalado. No se borran: quedan aquí por si los
          necesitas o por si el módulo vuelve.
        </Alert>
      )}

      <Card title="Filtros">
        <div style={filterGrid}>
          <Field label="Módulo">
            {(props) => (
              <select
                {...props}
                className="ui-input"
                value={filters.ownerModuleCode ?? ''}
                onChange={(event) => apply({ ownerModuleCode: event.target.value || undefined })}
              >
                <option value="">Todos</option>
                {moduleCodes.map((code) => (
                  <option key={code} value={code}>
                    {code}
                  </option>
                ))}
              </select>
            )}
          </Field>

          <Field label="Tipo">
            {(props) => (
              <select
                {...props}
                className="ui-input"
                value={filters.mimeType ?? ''}
                onChange={(event) => apply({ mimeType: event.target.value || undefined })}
              >
                <option value="">Todos</option>
                <option value="image/jpeg">JPEG</option>
                <option value="image/png">PNG</option>
                <option value="image/webp">WebP</option>
              </select>
            )}
          </Field>

          <Field label="Desde">
            {(props) => (
              <Input
                {...props}
                type="date"
                value={filters.from?.slice(0, 10) ?? ''}
                onChange={(event) =>
                  apply({ from: event.target.value ? `${event.target.value}T00:00:00Z` : undefined })
                }
              />
            )}
          </Field>

          <Field label="Sin dueño">
            {(props) => (
              <select
                {...props}
                className="ui-input"
                value={filters.isOrphan === undefined ? '' : String(filters.isOrphan)}
                onChange={(event) =>
                  apply({ isOrphan: event.target.value === '' ? undefined : event.target.value === 'true' })
                }
              >
                <option value="">Todos</option>
                <option value="true">Solo sin dueño</option>
                <option value="false">Solo con dueño</option>
              </select>
            )}
          </Field>
        </div>
      </Card>

      {state.status === 'error' && <Alert tone="danger">{state.failure.message}</Alert>}

      <Gallery
        items={result?.items ?? []}
        itemKey={(asset) => asset.mediaAssetId}
        loading={state.status === 'loading'}
        empty={
          <EmptyState
            title="Todavía no hay imágenes"
            description="Arrastra una arriba para empezar."
          />
        }
        render={(asset) => (
          <MediaItem
            asset={asset}
            canDelete={canDelete}
            onCopy={() => {
              void navigator.clipboard?.writeText(new URL(asset.url, window.location.origin).href);
              show('Enlace copiado.');
            }}
            onDelete={() => setPendingDeletion(asset)}
          />
        )}
      />

      {result && (
        <Pagination
          page={result.page}
          totalPages={result.totalPages}
          totalItems={result.totalItems}
          onChange={setPage}
        />
      )}

      <ConfirmDialog
        open={pendingDeletion !== null}
        title="Dar de baja este archivo"
        confirmLabel="Dar de baja"
        danger
        busy={busy}
        onConfirm={() => void remove()}
        onCancel={() => setPendingDeletion(null)}
      >
        {/* La consecuencia real, no que se marque una columna. */}
        <p>
          <strong>Dejará de verse en la web</strong> allí donde esté puesto: en un producto, en un
          banner, donde sea.
        </p>
        <p>El archivo no se borra del disco, así que se puede recuperar.</p>
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}

function MediaItem({
  asset,
  canDelete,
  onCopy,
  onDelete,
}: {
  asset: MediaAsset;
  canDelete: boolean;
  onCopy: () => void;
  onDelete: () => void;
}) {
  return (
    <article className="gal__item" data-orphan={asset.isOrphan} data-dimmed={!asset.isActive}>
      <div className="gal__thumb">
        <img src={asset.url} alt={asset.altText ?? asset.originalName ?? ''} loading="lazy" />
      </div>

      <div className="gal__body">
        <p className="gal__name">{asset.originalName ?? 'Sin nombre'}</p>

        <p className="gal__meta">
          {formatSize(asset.sizeBytes)}
          {asset.width && asset.height && ` · ${asset.width}×${asset.height}`}
        </p>

        <p className="gal__meta">
          {asset.ownerModuleCode ?? 'sin módulo'} ·{' '}
          {new Date(asset.createdAt).toLocaleDateString('es-PE')}
        </p>

        {asset.isOrphan && <Badge tone="warning">Su módulo ya no está</Badge>}
        {!asset.isActive && <Badge tone="neutral">De baja</Badge>}

        <div className="gal__foot">
          {/* Lo que alguien va a querer hacer el 90% de las veces. */}
          <Button size="sm" variant="ghost" onClick={onCopy}>
            Copiar enlace
          </Button>

          {canDelete && asset.isActive && (
            <Button size="sm" variant="ghost" onClick={onDelete}>
              Dar de baja
            </Button>
          )}
        </div>
      </div>
    </article>
  );
}

const filterGrid = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
  gap: 'var(--s4)',
};
