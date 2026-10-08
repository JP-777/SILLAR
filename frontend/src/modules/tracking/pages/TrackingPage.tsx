import { useCallback, useEffect, useMemo, useState, type DragEvent } from 'react';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { isApiError } from '../../../shared/http/errors';
import { Alert, Badge, Button, EmptyState, Field, Input, Switch } from '../../../shared/ui';
import {
  ConfirmDialog,
  Drawer,
  FailureAlert,
  Pagination,
  Toasts,
  useToasts,
} from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import {
  cardCount,
  formatDateTime,
  fromDateTimeLocal,
  moveId,
  peerIds,
  shiftId,
  statusName,
  toDateTimeLocal,
} from '../logica/board';
import { trackingService } from '../services/tracking';
import type {
  TrackingBoard,
  TrackingBoardCard,
  TrackingBoardColumn,
  TrackingOrderDetail,
  TrackingScope,
} from '../services/contracts';
import '../tracking.css';

const CLOSED_PAGE_SIZE = 20;

interface DraggedCard {
  card: TrackingBoardCard;
  sourceStatus: string;
}

interface PendingTransition {
  card: TrackingBoardCard;
  targetStatus: string;
}

/** A1–A5 de M06: tablero, reordenamiento, detalle, notas y transición autoritativa. */
export function TrackingPage() {
  const [scope, setScope] = useState<TrackingScope>('open');
  const [page, setPage] = useState(1);
  const loadBoard = useCallback(
    () => trackingService.board(scope, page, CLOSED_PAGE_SIZE),
    [scope, page],
  );
  const board = useResource(loadBoard, 'cargar el tablero de seguimiento');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [dragged, setDragged] = useState<DraggedCard | null>(null);
  const [transition, setTransition] = useState<PendingTransition | null>(null);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [busy, setBusy] = useState(false);
  const { toasts, show } = useToasts();

  useEffect(() => setPage(1), [scope]);

  if (board.state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;

  const data = board.state.status === 'ready' ? board.state.data : null;
  const empty = data !== null && cardCount(data.columns) === 0;

  async function refreshOpenBoard(): Promise<TrackingBoard> {
    const fresh = await trackingService.board('open', 1, CLOSED_PAGE_SIZE);
    if (scope === 'open') board.replace(fresh);
    return fresh;
  }

  async function reorder(
    card: TrackingBoardCard,
    orderedPeerIds: string[],
  ) {
    setBusy(true);
    setFailure(null);
    try {
      await trackingService.setPriority(card.serviceOrderId, {
        boardPriority: null,
        pinned: card.pinned,
        orderedPeerIds,
      });
      await board.reload();
      show(`Orden de trabajo de ${card.visibleCode} actualizada.`);
    } catch (error) {
      setFailure(describe(error, 'reordenar el tablero'));
      await board.reload();
    } finally {
      setBusy(false);
      setDragged(null);
    }
  }

  async function dropInside(column: TrackingBoardColumn, beforeId: string | null) {
    if (!dragged || scope !== 'open' || busy) return;

    if (dragged.sourceStatus !== column.status) {
      const source = data?.columns.find((candidate) => candidate.status === dragged.sourceStatus);
      if (!source?.legalTargetStatuses.includes(column.status)) {
        setFailure({
          kind: 'inline',
          message: 'Ese cambio de estado no está disponible desde la columna actual.',
          fieldErrors: null,
          blockedBy: null,
        });
        setDragged(null);
        return;
      }

      setTransition({ card: dragged.card, targetStatus: column.status });
      setDragged(null);
      return;
    }

    const target = beforeId ? column.cards.find((card) => card.serviceOrderId === beforeId) : null;
    if (target && target.pinned !== dragged.card.pinned) {
      setFailure({
        kind: 'inline',
        message: 'Para pasar entre fijadas y no fijadas, usa primero la acción de fijar o desfijar.',
        fieldErrors: null,
        blockedBy: null,
      });
      setDragged(null);
      return;
    }

    const current = peerIds(column, dragged.card);
    const ordered = moveId(current, dragged.card.serviceOrderId, beforeId);
    if (ordered.join('|') === current.join('|')) {
      setDragged(null);
      return;
    }

    await reorder(dragged.card, ordered);
  }

  async function shift(column: TrackingBoardColumn, card: TrackingBoardCard, delta: -1 | 1) {
    const current = peerIds(column, card);
    const ordered = shiftId(current, card.serviceOrderId, delta);
    if (ordered.join('|') !== current.join('|')) await reorder(card, ordered);
  }

  async function togglePinned(card: TrackingBoardCard) {
    setBusy(true);
    setFailure(null);
    try {
      await trackingService.setPriority(card.serviceOrderId, {
        boardPriority: null,
        pinned: !card.pinned,
        orderedPeerIds: null,
      });
      await board.reload();
      show(card.pinned ? `${card.visibleCode} ya no está fijada.` : `${card.visibleCode} quedó fijada.`);
    } catch (error) {
      setFailure(describe(error, card.pinned ? 'desfijar la orden' : 'fijar la orden'));
    } finally {
      setBusy(false);
    }
  }

  async function confirmTransition() {
    if (!transition) return;
    const pending = transition;
    setBusy(true);
    setFailure(null);

    try {
      await trackingService.transition(pending.card.serviceOrderId, {
        expectedStatus: pending.card.currentStatus,
        targetStatus: pending.targetStatus,
      });
      setTransition(null);
      await board.reload();
      show(`${pending.card.visibleCode} cambió de estado.`);
    } catch (error) {
      setTransition(null);

      if (isApiError(error, 'Conflict')) {
        try {
          const fresh = await refreshOpenBoard();
          const actual = fresh.columns.flatMap((column) => column.cards)
            .find((card) => card.serviceOrderId === pending.card.serviceOrderId)?.currentStatus ?? null;
          setFailure({
            kind: 'inline',
            message: `Esta orden cambió mientras la mirabas: ahora está ${statusName(fresh.columns, actual)}. Vuelve a intentarlo si todavía quieres moverla.`,
            fieldErrors: null,
            blockedBy: null,
          });
        } catch {
          setFailure({
            kind: 'inline',
            message: 'Esta orden cambió mientras la mirabas. Actualiza el tablero antes de volver a moverla.',
            fieldErrors: null,
            blockedBy: null,
          });
        }
      } else if (isApiError(error, 'NotFound')) {
        setFailure({
          kind: 'inline',
          message: 'Esa orden ya no está disponible. Vuelve al tablero para ver las actuales.',
          fieldErrors: null,
          blockedBy: null,
        });
        await board.reload();
      } else {
        setFailure(describe(error, 'cambiar el estado de la orden'));
      }
    } finally {
      setBusy(false);
    }
  }

  const transitionSource = transition && data
    ? data.columns.find((column) => column.status === transition.card.currentStatus)
    : null;
  const transitionTarget = transition && data
    ? data.columns.find((column) => column.status === transition.targetStatus)
    : null;

  return (
    <PageContainer
      title="Seguimiento"
      description="Ordena el trabajo interno sin duplicar el estado autoritativo de las órdenes de servicio."
      actions={(
        <div className="tracking-tabs" role="group" aria-label="Vista del seguimiento">
          <Button variant={scope === 'open' ? 'primary' : 'secondary'} onClick={() => setScope('open')}>
            En trabajo
          </Button>
          <Button variant={scope === 'closed' ? 'primary' : 'secondary'} onClick={() => setScope('closed')}>
            Terminadas
          </Button>
        </div>
      )}
    >
      <div className="tracking-stack">
        <FailureAlert failure={failure} />
        {board.state.status === 'error' && <FailureAlert failure={board.state.failure} />}

        {board.state.status === 'loading' && (
          <Alert>Preparando el tablero de seguimiento…</Alert>
        )}

        {data && empty && (
          <EmptyState
            title={scope === 'open' ? 'Todavía no hay órdenes que seguir' : 'Todavía no hay órdenes terminadas'}
            description={scope === 'open' ? 'Las órdenes abiertas aparecerán aquí cuando M05b las publique.' : 'Las órdenes cerradas aparecerán aquí y se conservarán paginadas.'}
          />
        )}

        {data && !empty && (
          <div className="tracking-board" aria-busy={busy}>
            {data.columns.map((column) => (
              <section
                key={column.status}
                className="tracking-column"
                aria-labelledby={`tracking-column-${column.status}`}
                onDragOver={(event) => {
                  if (dragged && scope === 'open') event.preventDefault();
                }}
                onDrop={(event) => {
                  event.preventDefault();
                  void dropInside(column, null);
                }}
              >
                <header className="tracking-column__header">
                  <h2 id={`tracking-column-${column.status}`}>{column.displayName}</h2>
                  <Badge>{column.cards.length}</Badge>
                </header>

                <div className="tracking-column__cards">
                  {column.cards.map((card) => {
                    const peers = peerIds(column, card);
                    const peerIndex = peers.indexOf(card.serviceOrderId);
                    return (
                      <article
                        key={card.serviceOrderId}
                        className="tracking-card"
                        draggable={scope === 'open' && !column.isTerminal && !busy}
                        onDragStart={() => setDragged({ card, sourceStatus: column.status })}
                        onDragEnd={() => setDragged(null)}
                        onDragOver={(event) => {
                          if (dragged && scope === 'open') event.preventDefault();
                        }}
                        onDrop={(event: DragEvent<HTMLElement>) => {
                          event.preventDefault();
                          event.stopPropagation();
                          void dropInside(column, card.serviceOrderId);
                        }}
                      >
                        <div className="tracking-card__topline">
                          <strong>{card.visibleCode}</strong>
                          {card.pinned && <Badge tone="warning">Fijada</Badge>}
                        </div>
                        <span>{card.customerName}</span>
                        <span className="tracking-muted">
                          {card.currentAssignee?.displayName ?? 'Sin responsable'}
                        </span>
                        {card.internalDueAt && (
                          <span className="tracking-muted">Plazo interno: {formatDateTime(card.internalDueAt)}</span>
                        )}

                        <div className="tracking-card__actions">
                          <Button size="sm" variant="ghost" onClick={() => setSelectedId(card.serviceOrderId)}>
                            Abrir detalle
                          </Button>
                          {scope === 'open' && !column.isTerminal && (
                            <>
                              <Button size="sm" variant="ghost" disabled={busy} onClick={() => void togglePinned(card)}>
                                {card.pinned ? 'Desfijar' : 'Fijar'}
                              </Button>
                              <Button size="sm" variant="ghost" disabled={busy || peerIndex <= 0} onClick={() => void shift(column, card, -1)}>
                                Subir
                              </Button>
                              <Button size="sm" variant="ghost" disabled={busy || peerIndex < 0 || peerIndex >= peers.length - 1} onClick={() => void shift(column, card, 1)}>
                                Bajar
                              </Button>
                            </>
                          )}
                        </div>
                      </article>
                    );
                  })}
                </div>
              </section>
            ))}
          </div>
        )}

        {scope === 'closed' && data?.pagination && (
          <Pagination
            page={data.pagination.page}
            totalPages={data.pagination.totalPages}
            totalItems={data.pagination.totalItems}
            onChange={setPage}
          />
        )}
      </div>

      {selectedId && (
        <TrackingDetail
          serviceOrderId={selectedId}
          columns={data?.columns ?? []}
          onClose={() => setSelectedId(null)}
          onChanged={async () => {
            await board.reload();
          }}
          showToast={show}
        />
      )}

      <ConfirmDialog
        open={transition !== null}
        title="Confirmar cambio de estado"
        confirmLabel="Cambiar estado"
        busy={busy}
        onCancel={() => setTransition(null)}
        onConfirm={() => void confirmTransition()}
      >
        {transition && (
          <p>
            {transition.card.visibleCode} pasará de <strong>{transitionSource?.displayName ?? transition.card.currentStatus}</strong> a <strong>{transitionTarget?.displayName ?? transition.targetStatus}</strong>. M05b registrará el cambio y su historial autoritativo.
          </p>
        )}
      </ConfirmDialog>

      <Toasts toasts={toasts} />
    </PageContainer>
  );
}

function TrackingDetail({
  serviceOrderId,
  columns,
  onClose,
  onChanged,
  showToast,
}: {
  serviceOrderId: string;
  columns: readonly TrackingBoardColumn[];
  onClose: () => void;
  onChanged: () => Promise<void>;
  showToast: (message: string, tone?: 'success' | 'danger') => void;
}) {
  const load = useCallback(() => trackingService.detail(serviceOrderId), [serviceOrderId]);
  const detail = useResource(load, 'cargar el detalle de seguimiento');
  const { hasRole } = useSession();
  const [priority, setPriority] = useState('');
  const [pinned, setPinned] = useState(false);
  const [due, setDue] = useState('');
  const [note, setNote] = useState('');
  const [failure, setFailure] = useState<Failure | null>(null);
  const [busy, setBusy] = useState(false);
  const [noteToDeactivate, setNoteToDeactivate] = useState<string | null>(null);

  const current = detail.state.status === 'ready' ? detail.state.data : null;

  useEffect(() => {
    if (!current) return;
    setPriority(current.tracking.boardPriority?.toString() ?? '');
    setPinned(current.tracking.pinned);
    setDue(toDateTimeLocal(current.tracking.internalDueAt));
  }, [current]);

  async function execute(action: () => Promise<unknown>, context: string, message: string) {
    setBusy(true);
    setFailure(null);
    try {
      await action();
      await detail.reload();
      await onChanged();
      showToast(message);
      return true;
    } catch (error) {
      if (isApiError(error, 'NotFound')) {
        setFailure({
          kind: 'inline',
          message: 'Esa orden ya no está disponible. Vuelve al tablero para ver las actuales.',
          fieldErrors: null,
          blockedBy: null,
        });
      } else {
        setFailure(describe(error, context));
      }
      return false;
    } finally {
      setBusy(false);
    }
  }

  async function savePriority() {
    const parsed = priority.trim() === '' ? null : Number(priority);
    if (parsed !== null && (!Number.isInteger(parsed) || parsed < 0)) {
      setFailure({
        kind: 'validation',
        message: 'La prioridad manual debe ser un entero de cero o más.',
        fieldErrors: { boardPriority: 'Usa un entero de cero o más.' },
        blockedBy: null,
      });
      return;
    }
    await execute(
      () => trackingService.setPriority(serviceOrderId, { boardPriority: parsed, pinned, orderedPeerIds: null }),
      'guardar la prioridad',
      'Prioridad de seguimiento guardada.',
    );
  }

  async function saveDue() {
    await execute(
      () => trackingService.setDue(serviceOrderId, { internalDueAt: fromDateTimeLocal(due) }),
      'guardar el plazo interno',
      'Plazo interno guardado.',
    );
  }

  async function addNote() {
    const ok = await execute(
      () => trackingService.addNote(serviceOrderId, { body: note }),
      'guardar la nota de seguimiento',
      'Nota de seguimiento añadida.',
    );
    if (ok) setNote('');
  }

  async function deactivateNote() {
    if (!noteToDeactivate) return;
    const id = noteToDeactivate;
    setNoteToDeactivate(null);
    await execute(
      () => trackingService.deactivateNote(id),
      'dar de baja la nota',
      'Nota de seguimiento dada de baja.',
    );
  }

  return (
    <>
      <Drawer open title={current?.order.visibleCode ?? 'Seguimiento de la orden'} onClose={onClose}>
        <div className="tracking-stack">
          {detail.state.status === 'loading' && <Alert>Cargando el detalle de seguimiento…</Alert>}
          {detail.state.status === 'error' && <FailureAlert failure={detail.state.failure} />}
          <FailureAlert failure={failure} />

          {current && (
            <>
              <ReadOnlyOrder detail={current} columns={columns} />

              <section className="tracking-detail-section" aria-labelledby="tracking-own-data">
                <h3 id="tracking-own-data">Datos editables de seguimiento</h3>
                <p className="tracking-muted">Estos datos pertenecen a M06 y no cambian la orden de servicio.</p>

                <div className="tracking-form-grid">
                  <Field label="Prioridad manual" hint="Vacío mantiene el orden implícito por recepción." error={failure?.fieldErrors?.boardPriority}>
                    {(props) => (
                      <Input
                        {...props}
                        type="number"
                        min="0"
                        step="1"
                        value={priority}
                        onChange={(event) => setPriority(event.target.value)}
                      />
                    )}
                  </Field>
                  <div className="tracking-switch-row">
                    <Switch checked={pinned} onChange={setPinned} label="Fijar arriba" disabled={busy} />
                  </div>
                </div>
                <Button variant="secondary" disabled={busy} onClick={() => void savePriority()}>
                  Guardar prioridad
                </Button>

                <Field label="Plazo interno" hint={`Recibida: ${formatDateTime(current.order.receivedAt)} · Prometida al cliente: ${formatDateTime(current.order.promisedAt)}`} error={failure?.fieldErrors?.internalDueAt}>
                  {(props) => (
                    <Input {...props} type="datetime-local" value={due} onChange={(event) => setDue(event.target.value)} />
                  )}
                </Field>
                <Button variant="secondary" disabled={busy} onClick={() => void saveDue()}>
                  Guardar plazo interno
                </Button>
              </section>

              <section className="tracking-detail-section" aria-labelledby="tracking-notes">
                <h3 id="tracking-notes">Notas de seguimiento</h3>
                <p className="tracking-muted">Son internas de M06; no son las notas internas de la orden.</p>

                {current.tracking.notes.length === 0 ? (
                  <EmptyState title="Todavía no hay notas de seguimiento" />
                ) : (
                  <ol className="tracking-notes">
                    {current.tracking.notes.map((item) => (
                      <li key={item.trackingNoteId} className="tracking-note">
                        <p>{item.body}</p>
                        <span className="tracking-muted">
                          {item.author?.displayName ?? 'Registro del sistema'} · {formatDateTime(item.createdAt)}
                        </span>
                        {hasRole('admin') && (
                          <Button size="sm" variant="ghost" disabled={busy} onClick={() => setNoteToDeactivate(item.trackingNoteId)}>
                            Dar de baja nota
                          </Button>
                        )}
                      </li>
                    ))}
                  </ol>
                )}

                <Field label="Nueva nota" hint="Solo la ve el personal en Seguimiento." error={failure?.fieldErrors?.body}>
                  {(props) => (
                    <textarea
                      className="ui-input tracking-textarea"
                      rows={4}
                      {...props}
                      value={note}
                      onChange={(event) => setNote(event.target.value)}
                    />
                  )}
                </Field>
                <Button disabled={busy} onClick={() => void addNote()}>
                  Añadir nota
                </Button>
              </section>
            </>
          )}
        </div>
      </Drawer>

      <ConfirmDialog
        open={noteToDeactivate !== null}
        title="Dar de baja nota de seguimiento"
        confirmLabel="Dar de baja nota"
        danger
        busy={busy}
        onCancel={() => setNoteToDeactivate(null)}
        onConfirm={() => void deactivateNote()}
      >
        <p>La nota dejará de aparecer en el seguimiento. La baja es lógica y queda auditada.</p>
      </ConfirmDialog>
    </>
  );
}

function ReadOnlyOrder({ detail, columns }: { detail: TrackingOrderDetail; columns: readonly TrackingBoardColumn[] }) {
  const order = detail.order;
  const status = statusName(columns, order.currentStatus);
  const history = useMemo(
    () => [...order.statusHistory].sort((a, b) => a.occurredAt.localeCompare(b.occurredAt)),
    [order.statusHistory],
  );

  return (
    <section className="tracking-detail-section tracking-readonly" aria-labelledby="tracking-order-data">
      <h3 id="tracking-order-data">Orden de servicio · solo lectura</h3>
      <dl className="tracking-definition-grid">
        <div><dt>Cliente</dt><dd>{order.customerName}</dd></div>
        <div><dt>Estado</dt><dd>{status}</dd></div>
        <div><dt>Responsable</dt><dd>{order.currentAssignee?.displayName ?? 'Sin responsable'}</dd></div>
        <div><dt>Recibida</dt><dd>{formatDateTime(order.receivedAt)}</dd></div>
        <div><dt>Prometida al cliente</dt><dd>{formatDateTime(order.promisedAt)}</dd></div>
      </dl>

      <div>
        <h4>Trabajo solicitado</h4>
        {order.items.length === 0 ? (
          <p className="tracking-muted">La orden no contiene líneas de trabajo.</p>
        ) : (
          <ul className="tracking-work-items">
            {order.items.map((item) => (
              <li key={item.serviceOrderItemId}>
                <strong>{item.serviceName}</strong> · {item.quantity}{item.saleUnit ? ` ${item.saleUnit}` : ''}
                {item.requestedDetails && <span className="tracking-muted"> · {item.requestedDetails}</span>}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h4>Historial autoritativo de M05b</h4>
        <ol className="tracking-history">
          {history.map((entry) => (
            <li key={entry.statusHistoryId}>
              <span>{statusName(columns, entry.fromStatus)} → {statusName(columns, entry.toStatus)}</span>
              <span className="tracking-muted">
                {formatDateTime(entry.occurredAt)} · {entry.performedBy?.displayName ?? 'Registro del sistema'}
              </span>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
