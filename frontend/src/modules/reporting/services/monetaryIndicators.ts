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
  | 'reader_absent' | 'writer_not_accredited' | 'window_starts_before_coverage' | 'currency_not_recorded';

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

/** Un origen con su medida para un indicador: cuenta si se mide; motivo si no. */
export interface SourceMeasure {
  source: 'staff' | 'provider';
  status: 'measured' | 'not_measurable';
  count: number | null;
  reason: NotMeasurableReason | null;
  coveredSinceUtc: string | null;
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
    byMethod: { method: string; source: SourceMeasure['source']; currencyCode: string; count: number; amount: string }[] | null;
    withoutCurrencyCount: number | null;
    coverage: CurrencyCoverage | null;
    bySource: SourceMeasure[] | null;
  };
  i07: {
    status: IndicatorStatus;
    count: number | null;
    coverage: CurrencyCoverage | null;
    bySource: SourceMeasure[] | null;
    amountStatus: IndicatorStatus;
    amountByCurrency: CurrencyAmount[] | null;
    amountCoverage: CurrencyCoverage | null;
  };
}

const STATUSES: ReadonlySet<string> = new Set(['source_unavailable', 'not_measurable', 'empty', 'currency_not_accredited', 'with_data']);
const REASONS: ReadonlySet<string> = new Set([
  'reader_absent', 'writer_not_accredited', 'window_starts_before_coverage', 'currency_not_recorded',
]);
const METHOD = /^[a-z][a-z0-9_]*$/;
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

/**
 * Las medidas por origen de un indicador: personal y proveedor, en ese orden.
 * Devuelve las cuentas de los medidos; ninguno medido, lista vacía.
 */
function sources(list: unknown): Map<string, number> {
  if (!Array.isArray(list) || list.length !== SOURCES.length) fail();
  const measured = new Map<string, number>();
  list.forEach((item: SourceMeasure, index) => {
    if (!item || item.source !== SOURCES[index]) fail();
    if (item.status === 'measured') {
      if (item.reason !== null) fail();
      instant(item.coveredSinceUtc);
      measured.set(item.source, count(item.count));
    } else if (item.status !== 'not_measurable' || item.count !== null
      || typeof item.reason !== 'string' || !REASONS.has(item.reason) || item.reason === 'reader_absent') {
      fail();
    }
  });
  return measured;
}

function coverageOf(measured: number): CurrencyCoverage {
  return measured === SOURCES.length ? 'complete' : 'partial';
}

function amounts(list: unknown): CurrencyAmount[] {
  if (!Array.isArray(list)) fail();
  const seen = new Set<string>();
  for (const row of list) {
    if (!row || typeof row.currencyCode !== 'string' || !CURRENCY.test(row.currencyCode) || seen.has(row.currencyCode)) fail();
    seen.add(row.currencyCode);
    if (count(row.count) === 0) fail();
    amount(row.amount, row.count);
  }
  return list as CurrencyAmount[];
}

/**
 * I05–I07: solo cuentan los orígenes que la cobertura declara medidos, por
 * indicador; con alguno sin medir, la cobertura es parcial y nunca hay un vacío.
 */
export function verifyPaymentIndicators(
  data: PaymentIndicatorsView,
  requested: { start: string; end: string },
): PaymentIndicatorsView {
  if (data === null || typeof data !== 'object' || !data.i05 || !data.i06 || !data.i07) fail();
  samePeriod(data, requested);
  const { i05, i06, i07 } = data;
  const s05 = status(i05.status);

  // Sin medición, los tres a la vez y sin cifras.
  if (s05 === 'source_unavailable' || s05 === 'not_measurable') {
    if (status(i06.status) !== s05 || status(i07.status) !== s05 || i07.amountStatus !== s05) fail();
    const reason = data.notMeasurableReason;
    if (s05 === 'source_unavailable' ? reason !== null : (typeof reason !== 'string' || !REASONS.has(reason))) fail();
    nothing(data.observedAtUtc, i05.count, i05.coverage, i05.bySource, i06.byCurrency, i06.byMethod, i06.withoutCurrencyCount,
      i06.coverage, i06.bySource, i07.count, i07.coverage, i07.bySource, i07.amountByCurrency, i07.amountCoverage);
    return data;
  }
  if (data.notMeasurableReason !== null) fail();
  instant(data.observedAtUtc);

  // I05: al menos un origen medido; la cifra es la suma de los medidos.
  const counted = sources(i05.bySource);
  const registered = count(i05.count);
  if (counted.size === 0 || registered !== sum([...counted.values()]) || i05.coverage !== coverageOf(counted.size)
    || s05 !== countStatus(registered, counted.size === SOURCES.length)) fail();

  // I06: importe de los orígenes con importe medido, por moneda y por medio.
  const priced = sources(i06.bySource);
  count(i06.withoutCurrencyCount);
  const s06 = status(i06.status);
  if (s06 === 'currency_not_accredited') {
    if (priced.size !== 0 || i06.byCurrency !== null || i06.byMethod !== null || i06.coverage !== 'none'
      || i06.withoutCurrencyCount === 0) fail();
  } else {
    const byCurrency = amounts(i06.byCurrency);
    if (!Array.isArray(i06.byMethod) || priced.size === 0) fail();
    const rows = new Set<string>();
    const perCurrency = new Map<string, number>();
    for (const row of i06.byMethod) {
      const key = `${row?.method}|${row?.source}|${row?.currencyCode}`;
      if (!row || typeof row.method !== 'string' || !METHOD.test(row.method) || !priced.has(row.source)
        || !CURRENCY.test(row.currencyCode) || rows.has(key) || count(row.count) === 0) fail();
      amount(row.amount, row.count);
      rows.add(key);
      perCurrency.set(row.currencyCode, (perCurrency.get(row.currencyCode) ?? 0) + row.count);
    }
    const total = sum([...priced.values()]);
    if (byCurrency.length !== perCurrency.size || byCurrency.some((c) => perCurrency.get(c.currencyCode) !== c.count)
      || sum(byCurrency.map((c) => c.count)) !== total || i06.coverage !== coverageOf(priced.size)
      || s06 !== countStatus(total, priced.size === SOURCES.length)) fail();
  }

  // I07: tardíos de los orígenes con tardanza medida; su importe, aparte.
  const late = sources(i07.bySource);
  const s07 = status(i07.status);
  if (s07 === 'not_measurable') {
    if (late.size !== 0 || i07.amountStatus !== 'not_measurable') fail();
    nothing(i07.count, i07.coverage, i07.amountByCurrency, i07.amountCoverage);
    return data;
  }
  const lateCount = count(i07.count);
  if (late.size === 0 || lateCount !== sum([...late.values()]) || lateCount > registered
    || i07.coverage !== coverageOf(late.size) || s07 !== countStatus(lateCount, late.size === SOURCES.length)) fail();
  // El importe de los tardíos, solo de los orígenes con tardanza e importe medidos.
  const lateAndPriced = [...late.keys()].filter((source) => priced.has(source)).length;
  const a07 = status(i07.amountStatus);
  if (a07 === 'currency_not_accredited') {
    if (lateAndPriced !== 0 || i07.amountByCurrency !== null || i07.amountCoverage !== 'none') fail();
  } else {
    const lateAmounts = sum(amounts(i07.amountByCurrency).map((c) => c.count));
    if (lateAndPriced === 0 || lateAmounts > lateCount || i07.amountCoverage !== coverageOf(lateAndPriced)
      || a07 !== countStatus(lateAmounts, lateAndPriced === SOURCES.length)) fail();
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
  writer_not_accredited: 'Ventas todavía no acredita que se registren todos los cobros de este origen.',
  window_starts_before_coverage: 'El registro de este origen empezó después del inicio del período.',
  currency_not_recorded: 'Hay cobros sin moneda registrada.',
};

const METHOD_LABELS: Record<string, string> = { yape: 'Yape', efectivo: 'Efectivo' };

/** Un medio que M03 añada después se muestra por su código, no se oculta. */
export function methodLabel(code: string): string {
  return METHOD_LABELS[code] ?? code;
}
