import { InconsistentIndicatorsError } from './verifyOrderIndicators.ts';

/**
 * M10 — I02 (valor de pedidos) e I05–I07 (cobros registrados), tal como los
 * entrega el servidor sobre los lectores de M03, y su verificación antes de
 * publicar nada (D8).
 *
 * Reglas que esta capa garantiza a la pantalla:
 * - Un 0 solo existe en `empty`, y `empty` solo con todos los orígenes medidos:
 *   «no medible» y «fuente no disponible» no traen ninguna cifra.
 * - Una fila por moneda ISO 4217 y nunca un total entre monedas; en I02, activos
 *   y dados de baja por separado. Lo que no tiene moneda se cuenta aparte.
 * - Un origen no medible nunca deja afirmar cobertura completa.
 * - Los importes son texto decimal exacto. No se convierten a número.
 */

export type IndicatorStatus = 'source_unavailable' | 'not_measurable' | 'empty' | 'currency_not_accredited' | 'with_data';
export type CurrencyCoverage = 'complete' | 'partial' | 'none';
export type NotMeasurableReason =
  | 'reader_absent' | 'staff_writer_not_accredited' | 'source_not_recorded' | 'provider_writer_absent' | 'currency_not_recorded';

export interface CurrencyAmount { currencyCode: string; count: number; amount: string }

export interface OrderValueView {
  status: IndicatorStatus;
  startInclusive: string;
  endExclusive: string;
  observedAtUtc: string | null;
  createdCount: number | null;
  byCurrency: { currencyCode: string; activeCount: number; activeAmount: string; inactiveCount: number; inactiveAmount: string }[] | null;
  withoutCurrency: { active: number; inactive: number } | null;
  coverage: CurrencyCoverage | null;
}

export interface SourceMeasure {
  source: 'staff' | 'provider';
  status: 'measured' | 'not_measurable';
  count: number | null;
  reason: NotMeasurableReason | null;
}

export interface PaymentIndicatorsView {
  startInclusive: string;
  endExclusive: string;
  observedAtUtc: string | null;
  notMeasurableReason: NotMeasurableReason | null;
  i05: { status: IndicatorStatus; count: number | null; coverage: CurrencyCoverage | null; bySource: SourceMeasure[] | null };
  i06: {
    status: IndicatorStatus;
    byCurrency: CurrencyAmount[] | null;
    withoutCurrencyCount: number | null;
    coverage: CurrencyCoverage | null;
    methodBreakdown: 'not_published';
  };
  i07: { status: IndicatorStatus; count: number | null; coverage: CurrencyCoverage | null; amountStatus: IndicatorStatus };
}

const STATUSES: ReadonlySet<string> = new Set(['source_unavailable', 'not_measurable', 'empty', 'currency_not_accredited', 'with_data']);
const REASONS: ReadonlySet<string> = new Set([
  'reader_absent', 'staff_writer_not_accredited', 'source_not_recorded', 'provider_writer_absent', 'currency_not_recorded',
]);
const SOURCES = ['staff', 'provider'] as const;
const CURRENCY = /^[A-Z]{3}$/;
const AMOUNT = /^\d{1,29}(\.\d{1,28})?$/;
const ZERO = /^0(\.0+)?$/;

function fail(): never {
  throw new InconsistentIndicatorsError();
}

function count(value: unknown): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) fail();
  return value;
}

function sum(values: number[]): number {
  const total = values.reduce((a, b) => a + b, 0);
  if (!Number.isSafeInteger(total)) fail();
  return total;
}

function instant(value: unknown): number {
  if (typeof value !== 'string' || value.trim() === '') fail();
  const result = Date.parse(value);
  if (!Number.isFinite(result)) fail();
  return result;
}

function status(value: unknown): IndicatorStatus {
  if (typeof value !== 'string' || !STATUSES.has(value)) fail();
  return value as IndicatorStatus;
}

function amount(value: unknown, of: number): string {
  if (typeof value !== 'string' || !AMOUNT.test(value) || (of === 0 && !ZERO.test(value))) fail();
  return value;
}

function samePeriod(data: { startInclusive: unknown; endExclusive: unknown }, requested: { start: string; end: string }) {
  if (instant(data.startInclusive) !== instant(requested.start) || instant(data.endExclusive) !== instant(requested.end)) fail();
}

/** Sin medición, ni una cifra: ningún campo trae dato. */
function nothing(...values: unknown[]) {
  if (values.some((value) => value !== null)) fail();
}

/** Solo con todos los orígenes medidos un cero dice «no hubo». */
function countStatus(n: number, allSources: boolean): IndicatorStatus {
  return allSources && n === 0 ? 'empty' : 'with_data';
}

/** I02: comprueba la respuesta contra el período pedido. Coherente o nada. */
export function verifyOrderValue(data: OrderValueView, requested: { start: string; end: string }): OrderValueView {
  if (data === null || typeof data !== 'object') fail();
  samePeriod(data, requested);
  const state = status(data.status);
  if (state === 'source_unavailable' || state === 'not_measurable') {
    nothing(data.observedAtUtc, data.createdCount, data.byCurrency, data.withoutCurrency, data.coverage);
    return data;
  }
  instant(data.observedAtUtc);
  const created = count(data.createdCount);
  if (!data.withoutCurrency) fail();
  const without = sum([count(data.withoutCurrency.active), count(data.withoutCurrency.inactive)]);

  if (state === 'currency_not_accredited') {
    if (data.byCurrency !== null || data.coverage !== 'none' || created === 0 || without !== created) fail();
    return data;
  }
  if (!Array.isArray(data.byCurrency)) fail();
  const seen = new Set<string>();
  const counts: number[] = [];
  for (const row of data.byCurrency) {
    if (!row || typeof row.currencyCode !== 'string' || !CURRENCY.test(row.currencyCode) || seen.has(row.currencyCode)) fail();
    seen.add(row.currencyCode);
    const active = count(row.activeCount);
    const inactive = count(row.inactiveCount);
    amount(row.activeAmount, active);
    amount(row.inactiveAmount, inactive);
    if (active + inactive === 0) fail();
    counts.push(active, inactive);
  }
  if (sum([...counts, without]) !== created) fail();
  if (state === 'empty') {
    if (created !== 0 || data.byCurrency.length !== 0) fail();
    return data;
  }
  if (state !== 'with_data' || data.byCurrency.length === 0 || data.coverage !== (without === 0 ? 'complete' : 'partial')) fail();
  return data;
}

/** I05–I07: cada indicador con su estado, y entre ellos los totales y la cobertura cuadran. */
export function verifyPaymentIndicators(
  data: PaymentIndicatorsView,
  requested: { start: string; end: string },
): PaymentIndicatorsView {
  if (data === null || typeof data !== 'object' || !data.i05 || !data.i06 || !data.i07) fail();
  samePeriod(data, requested);
  const { i05, i06, i07 } = data;
  const s05 = status(i05.status);
  if (i06.methodBreakdown !== 'not_published' || status(i07.amountStatus) === 'with_data') fail();

  // Sin medición, los tres a la vez y sin cifras: la capacidad es una sola.
  if (s05 === 'source_unavailable' || s05 === 'not_measurable') {
    if (status(i06.status) !== s05 || status(i07.status) !== s05 || i07.amountStatus !== s05) fail();
    const reason = data.notMeasurableReason;
    if (s05 === 'source_unavailable' ? reason !== null : (typeof reason !== 'string' || !REASONS.has(reason))) fail();
    nothing(data.observedAtUtc, i05.count, i05.coverage, i05.bySource, i06.byCurrency, i06.withoutCurrencyCount,
      i06.coverage, i07.count, i07.coverage);
    return data;
  }
  if (data.notMeasurableReason !== null) fail();
  instant(data.observedAtUtc);

  // I05: personal y proveedor, en ese orden; el personal siempre medido.
  if (!Array.isArray(i05.bySource) || i05.bySource.length !== SOURCES.length) fail();
  const measured: number[] = [];
  i05.bySource.forEach((item, index) => {
    if (!item || item.source !== SOURCES[index]) fail();
    if (item.status === 'measured') {
      if (item.reason !== null) fail();
      measured.push(count(item.count));
    } else if (item.status !== 'not_measurable' || item.count !== null
      || typeof item.reason !== 'string' || !REASONS.has(item.reason) || index === 0) {
      fail();
    }
  });
  const allSources = measured.length === SOURCES.length;
  const registered = count(i05.count);
  if (registered !== sum(measured) || i05.coverage !== (allSources ? 'complete' : 'partial')
    || s05 !== countStatus(registered, allSources)) fail();

  // I06: lo que tiene moneda y lo que no suman los pagos medidos.
  const without = count(i06.withoutCurrencyCount);
  const s06 = status(i06.status);
  if (s06 === 'currency_not_accredited') {
    if (i06.byCurrency !== null || i06.coverage !== 'none' || without === 0 || without !== registered) fail();
  } else {
    if (!Array.isArray(i06.byCurrency) || s06 !== countStatus(registered, allSources)) fail();
    const seen = new Set<string>();
    for (const row of i06.byCurrency) {
      if (!row || typeof row.currencyCode !== 'string' || !CURRENCY.test(row.currencyCode) || seen.has(row.currencyCode)) fail();
      seen.add(row.currencyCode);
      if (count(row.count) === 0) fail();
      amount(row.amount, row.count);
    }
    if (sum([...i06.byCurrency.map((row) => row.count), without]) !== registered
      || i06.coverage !== (allSources && without === 0 ? 'complete' : 'partial')) fail();
  }

  // I07: los tardíos son parte de los registrados; su importe M03 no lo publica.
  const s07 = status(i07.status);
  if (s07 === 'not_measurable') {
    nothing(i07.count, i07.coverage);
  } else {
    const late = count(i07.count);
    if (late > registered || i07.coverage !== i05.coverage || s07 !== countStatus(late, allSources)) fail();
  }
  return data;
}

/**
 * Importe con su moneda, sin pasar por coma flotante: la parte entera se agrupa
 * como en es-PE y los decimales se dejan tal como llegaron.
 */
export function showAmount(currencyCode: string, amount: string): string {
  const [whole, fraction] = amount.split('.');
  const grouped = BigInt(whole).toLocaleString('es-PE');
  return `${currencyCode} ${fraction === undefined ? grouped : `${grouped}.${fraction}`}`;
}

export const SOURCE_LABELS: Record<SourceMeasure['source'], string> = {
  staff: 'Registrados por el personal',
  provider: 'Registrados por el proveedor de pagos',
};

/** Por qué no hay cifra, dicho para quien administra la tienda. */
export const REASON_LABELS: Record<NotMeasurableReason, string> = {
  reader_absent: 'Ventas todavía no publica la lectura de cobros.',
  staff_writer_not_accredited: 'Ventas todavía no acredita que el personal pueda registrar cobros.',
  source_not_recorded: 'Ventas no guarda quién registró cada cobro.',
  provider_writer_absent: 'Todavía no hay registro de cobros del proveedor de pagos.',
  currency_not_recorded: 'Hay cobros sin moneda registrada.',
};
