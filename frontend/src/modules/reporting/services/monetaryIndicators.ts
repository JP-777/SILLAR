import { InconsistentIndicatorsError } from './verifyOrderIndicators.ts';

/**
 * M10 — I02 (valor de pedidos) e I05–I07 (cobros registrados), tal como los
 * entrega el servidor, y su verificación antes de publicar nada (D8).
 *
 * Reglas que esta capa garantiza a la pantalla:
 * - Un 0 solo existe en `empty`: «no medible» y «fuente no disponible» no
 *   traen ninguna cifra.
 * - Una fila por moneda ISO 4217 y nunca un total entre monedas. Los hechos
 *   sin moneda se cuentan aparte; su importe no existe.
 * - Los importes son texto decimal exacto. No se convierten a número: no se
 *   suman aquí ni pierden céntimos.
 */

export type IndicatorStatus = 'source_unavailable' | 'not_measurable' | 'empty' | 'currency_not_accredited' | 'with_data';
export type CurrencyCoverage = 'complete' | 'partial' | 'none';
export type PaymentSource = 'staff' | 'provider' | 'unrecorded';

export interface CurrencyAmount { currencyCode: string; count: number; amount: string }

export interface OrderValueView {
  status: IndicatorStatus;
  startInclusive: string;
  endExclusive: string;
  observedAtUtc: string | null;
  createdCount: number | null;
  byCurrency: CurrencyAmount[] | null;
  withoutCurrencyCount: number | null;
  coverage: CurrencyCoverage | null;
}

export interface PaymentIndicatorsView {
  startInclusive: string;
  endExclusive: string;
  observedAtUtc: string | null;
  i05: { status: IndicatorStatus; count: number | null; bySource: { source: PaymentSource; count: number }[] | null };
  i06: {
    status: IndicatorStatus;
    byMethod: { method: string; source: PaymentSource; currencyCode: string; count: number; amount: string }[] | null;
    withoutCurrency: { method: string; source: PaymentSource; count: number }[] | null;
    coverage: CurrencyCoverage | null;
  };
  i07: {
    status: IndicatorStatus;
    count: number | null;
    amountStatus: IndicatorStatus;
    amountByCurrency: CurrencyAmount[] | null;
    withoutCurrencyCount: number | null;
  };
}

const STATUSES: ReadonlySet<string> = new Set(['source_unavailable', 'not_measurable', 'empty', 'currency_not_accredited', 'with_data']);
const SOURCES: ReadonlySet<string> = new Set(['staff', 'provider', 'unrecorded']);
const CURRENCY = /^[A-Z]{3}$/;
const AMOUNT = /^\d{1,29}(\.\d{1,28})?$/;
const METHOD = /^[a-z][a-z0-9_]*$/;

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

function samePeriod(data: { startInclusive: unknown; endExclusive: unknown }, requested: { start: string; end: string }) {
  if (instant(data.startInclusive) !== instant(requested.start) || instant(data.endExclusive) !== instant(requested.end)) fail();
}

/** Sin medición, ni una cifra: ningún campo trae dato. */
function nothing(...values: unknown[]) {
  if (values.some((value) => value !== null)) fail();
}

function amounts(list: unknown): CurrencyAmount[] {
  if (!Array.isArray(list) || list.length === 0) fail();
  const seen = new Set<string>();
  for (const item of list) {
    if (!item || typeof item.currencyCode !== 'string' || !CURRENCY.test(item.currencyCode) || seen.has(item.currencyCode)) fail();
    seen.add(item.currencyCode);
    if (count(item.count) === 0 || typeof item.amount !== 'string' || !AMOUNT.test(item.amount)) fail();
  }
  return list as CurrencyAmount[];
}

function coverageOf(without: number): CurrencyCoverage {
  return without === 0 ? 'complete' : 'partial';
}

/** I02: comprueba la respuesta contra el período pedido. Coherente o nada. */
export function verifyOrderValue(data: OrderValueView, requested: { start: string; end: string }): OrderValueView {
  if (data === null || typeof data !== 'object') fail();
  samePeriod(data, requested);
  switch (status(data.status)) {
    case 'source_unavailable':
    case 'not_measurable':
      nothing(data.observedAtUtc, data.createdCount, data.byCurrency, data.withoutCurrencyCount, data.coverage);
      return data;
    case 'empty':
      instant(data.observedAtUtc);
      if (count(data.createdCount) !== 0 || !Array.isArray(data.byCurrency) || data.byCurrency.length !== 0
        || count(data.withoutCurrencyCount) !== 0) fail();
      return data;
    case 'currency_not_accredited':
      instant(data.observedAtUtc);
      if (data.byCurrency !== null || data.coverage !== 'none' || count(data.createdCount) === 0
        || count(data.withoutCurrencyCount) !== data.createdCount) fail();
      return data;
    case 'with_data': {
      instant(data.observedAtUtc);
      const without = count(data.withoutCurrencyCount);
      const listed = amounts(data.byCurrency);
      if (sum([...listed.map((a) => a.count), without]) !== count(data.createdCount) || data.coverage !== coverageOf(without)) fail();
      return data;
    }
  }
}

function sourceOf(value: unknown): PaymentSource {
  if (typeof value !== 'string' || !SOURCES.has(value)) fail();
  return value as PaymentSource;
}

function method(value: unknown): string {
  if (typeof value !== 'string' || !METHOD.test(value)) fail();
  return value;
}

/** I05–I07: cada indicador con su propio estado, y entre ellos sus totales cuadran. */
export function verifyPaymentIndicators(
  data: PaymentIndicatorsView,
  requested: { start: string; end: string },
): PaymentIndicatorsView {
  if (data === null || typeof data !== 'object' || !data.i05 || !data.i06 || !data.i07) fail();
  samePeriod(data, requested);
  const { i05, i06, i07 } = data;
  const s05 = status(i05.status);
  const s06 = status(i06.status);
  const s07 = status(i07.status);
  const a07 = status(i07.amountStatus);

  // Sin medición, los tres a la vez y sin cifras: la capacidad es una sola.
  if (s05 === 'source_unavailable' || s05 === 'not_measurable') {
    if (s06 !== s05 || s07 !== s05 || a07 !== s05) fail();
    nothing(data.observedAtUtc, i05.count, i05.bySource, i06.byMethod, i06.withoutCurrency, i06.coverage,
      i07.count, i07.amountByCurrency, i07.withoutCurrencyCount);
    return data;
  }
  instant(data.observedAtUtc);

  const registered = count(i05.count);
  if (s05 === 'empty') {
    if (registered !== 0 || s06 !== 'empty' || s07 !== 'empty' || a07 !== 'empty') fail();
    return data;
  }
  if (s05 !== 'with_data' || registered === 0 || !Array.isArray(i05.bySource)) fail();
  const sources = new Set<string>();
  for (const item of i05.bySource) {
    if (!item || sources.has(sourceOf(item.source)) || count(item.count) === 0) fail();
    sources.add(item.source);
  }
  if (sum(i05.bySource.map((s) => s.count)) !== registered) fail();

  // I06: lo que tiene moneda y lo que no suman los mismos pagos que I05.
  if (!Array.isArray(i06.withoutCurrency)) fail();
  const keys = new Set<string>();
  for (const item of i06.withoutCurrency) {
    const key = `${method(item?.method)}|${sourceOf(item.source)}`;
    if (keys.has(key) || count(item.count) === 0) fail();
    keys.add(key);
  }
  const without06 = sum(i06.withoutCurrency.map((m) => m.count));
  let with06 = 0;
  if (s06 === 'with_data') {
    if (!Array.isArray(i06.byMethod) || i06.byMethod.length === 0) fail();
    const rows = new Set<string>();
    for (const item of i06.byMethod) {
      const key = `${method(item?.method)}|${sourceOf(item.source)}|${item.currencyCode}`;
      if (!CURRENCY.test(item.currencyCode) || rows.has(key) || count(item.count) === 0
        || typeof item.amount !== 'string' || !AMOUNT.test(item.amount)) fail();
      rows.add(key);
    }
    with06 = sum(i06.byMethod.map((m) => m.count));
    if (i06.coverage !== coverageOf(without06)) fail();
  } else if (s06 !== 'currency_not_accredited' || i06.byMethod !== null || i06.coverage !== 'none') {
    fail();
  }
  if (with06 + without06 !== registered) fail();

  // I07: los tardíos son parte de los registrados; su importe, solo con moneda.
  const late = count(i07.count);
  if (s07 === 'empty') {
    if (late !== 0 || a07 !== 'empty') fail();
    return data;
  }
  if (s07 !== 'with_data' || late === 0 || late > registered) fail();
  const without07 = count(i07.withoutCurrencyCount);
  if (a07 === 'with_data') {
    if (sum([...amounts(i07.amountByCurrency).map((a) => a.count), without07]) !== late) fail();
  } else if (a07 !== 'currency_not_accredited' || i07.amountByCurrency !== null || without07 !== late) {
    fail();
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

export const SOURCE_LABELS: Record<PaymentSource, string> = {
  staff: 'Registrado por el personal',
  provider: 'Registrado por el proveedor de pagos',
  unrecorded: 'Sin origen registrado',
};

const METHOD_LABELS: Record<string, string> = { yape: 'Yape', efectivo: 'Efectivo' };

/** Un medio que M03 añada después se muestra por su código, no se oculta. */
export function methodLabel(code: string): string {
  return METHOD_LABELS[code] ?? code;
}
