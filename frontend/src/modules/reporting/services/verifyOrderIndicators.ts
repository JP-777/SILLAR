import { orderStates, type OrderIndicatorsView } from './orderIndicators.ts';

/**
 * Los indicadores llegaron, pero no cuadran entre sí o no son del período pedido.
 *
 * Es un error real y distinto de «fuente no disponible»: la fuente respondió, y
 * lo que respondió no se puede presentar como dato comercial. La pantalla lo
 * muestra como error y no dibuja ninguna cifra.
 */
export class InconsistentIndicatorsError extends Error {
  constructor() {
    super('Los indicadores recibidos no cuadran entre sí, así que no se muestran. '
      + 'Vuelve a consultar el período; si se repite, avisa a quien administre el sistema.');
    this.name = 'InconsistentIndicatorsError';
  }
}

const VALID_STATUS: ReadonlySet<string> = new Set(orderStates.map((state) => state.code));

function fail(): never {
  throw new InconsistentIndicatorsError();
}

function count(value: unknown): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) fail();
  return value;
}

function instant(value: unknown): number {
  if (typeof value !== 'string' || value.trim() === '') fail();
  const result = Date.parse(value);
  if (!Number.isFinite(result)) fail();
  return result;
}

/**
 * M10 D8: comprueba la respuesta antes de publicarla. No consulta ni agrega nada.
 *
 * Invariantes, sobre una misma cohorte:
 * - la respuesta es del período pedido `[inicio, fin)`;
 * - fuente ausente ⇒ ninguna métrica (nunca un cero inventado);
 * - suma por estado = pedidos creados = activos + dados de baja;
 * - vencidos = recuento del estado `expired`;
 * - estados conocidos y sin repetir; instante de observación presente;
 * - «vacío» si y solo si no se creó ningún pedido.
 */
export function verifyOrderIndicators(
  data: OrderIndicatorsView,
  requested: { start: string; end: string },
): OrderIndicatorsView {
  if (data === null || typeof data !== 'object') fail();
  const from = instant(data.startInclusive);
  const until = instant(data.endExclusive);
  if (until <= from) fail();
  if (from !== instant(requested.start) || until !== instant(requested.end)) fail();

  if (data.status === 'source_unavailable') {
    if ([data.i01CreatedOrders, data.i03CurrentStatusCounts,
      data.i04CurrentlyExpiredOrders, data.activeOrders,
      data.inactiveOrders].some((value) => value !== null)) fail();
    return data;
  }

  if (data.status !== 'empty' && data.status !== 'with_data') fail();
  instant(data.observedAtUtc);
  const created = count(data.i01CreatedOrders);
  const expired = count(data.i04CurrentlyExpiredOrders);
  const active = count(data.activeOrders);
  const inactive = count(data.inactiveOrders);
  if (!Array.isArray(data.i03CurrentStatusCounts)) fail();

  const seen = new Set<string>();
  let total = 0;
  let expiredByStatus = 0;
  for (const entry of data.i03CurrentStatusCounts) {
    if (!entry || typeof entry.status !== 'string' || !VALID_STATUS.has(entry.status)
        || seen.has(entry.status)) fail();
    seen.add(entry.status);
    const n = count(entry.count);
    total += n;
    if (!Number.isSafeInteger(total)) fail();
    if (entry.status === 'expired') expiredByStatus = n;
  }
  if (!Number.isSafeInteger(active + inactive)) fail();
  if (created !== active + inactive || created !== total || expired !== expiredByStatus) fail();
  if ((data.status === 'empty') !== (created === 0)) fail();
  return data;
}
