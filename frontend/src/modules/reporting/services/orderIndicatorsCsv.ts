import { orderStates, type OrderIndicatorsView } from './orderIndicators.ts';
import { isReportZone, type ReportZone } from './period.ts';
import { assessReportComparison, type ReportComparisonSnapshot } from './reportComparisonGate.ts';
import { InconsistentIndicatorsError, verifyOrderIndicators } from './verifyOrderIndicators.ts';

/**
 * M10 — generador CSV puro de los indicadores de pedidos.
 *
 * Solo transforma datos ya consultados; no llama a la red ni descarga nada.
 * **No se expone al usuario**: D-M10-05 exige que toda exportación quede
 * auditada con una acción propia, y esa capacidad es de CORE. Cuando exista,
 * la pantalla podrá ofrecer el archivo; este módulo ya fija su contenido.
 *
 * Reglas:
 * - Las filas son las mismas que muestra la vista: pedidos creados, actualmente
 *   vencidos, activos, dados de baja y los siete estados actuales.
 * - Antes de escribir nada se vuelve a verificar la coherencia (D8). Con fuente
 *   ausente o datos incoherentes no hay archivo, nunca uno con ceros.
 * - La resta descriptiva B − A solo entra si la compuerta de comparación la
 *   autoriza. La compuerta se calcula aquí, sobre las mismas A y B que se
 *   escriben: una decisión recibida de fuera podría ser de otra pareja.
 * - UTF-8 con BOM (Excel en español lo abre bien), separador coma, CRLF y
 *   comillas según RFC 4180. Toda celda de texto que empiece por = + - @,
 *   tabulador o retorno se neutraliza con un apóstrofo; los números se escriben
 *   como números.
 * - Cada fila dice cuándo se observó y de dónde sale la cifra: frescura y
 *   procedencia viajan con el archivo, no se quedan en la pantalla.
 * - Sin identificadores, códigos de pedido, clientes ni importes.
 */

export interface OrderIndicatorsCsvInput {
  readonly zone: ReportZone;
  readonly baseline: OrderIndicatorsView;
  /** Cohorte B consultada en la comparación, si la hay. */
  readonly comparison?: OrderIndicatorsView | null;
}

export type OrderIndicatorsCsv =
  | { readonly kind: 'ready'; readonly fileName: string; readonly mimeType: string; readonly content: string }
  | { readonly kind: 'unavailable'; readonly reason: string };

export const CSV_MIME_TYPE = 'text/csv;charset=utf-8';
const BOM = '﻿';
const EOL = '\r\n';
const HEADER = ['Cohorte', 'Inicio (incluido)', 'Fin (excluido)', 'Zona horaria', 'Observado', 'Fuente', 'Indicador', 'Pedidos'];
/** Procedencia de cada cifra: lo leído de ventas y lo que calcula este módulo. */
const SOURCE_SALES = 'Módulo de ventas';
const SOURCE_REPORTS = 'Cálculo de reportes';

type Cell = string | number;

/** Neutraliza la inyección de fórmulas y aplica las comillas de RFC 4180. */
export function csvCell(value: Cell): string {
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value)) throw new RangeError('Solo se exportan recuentos enteros exactos.');
    return String(value);
  }
  const safe = /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
  return /[",\r\n]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe;
}

function row(cells: readonly Cell[]): string {
  return cells.map(csvCell).join(',');
}

/** Fecha y hora en la zona elegida, sin depender de la del navegador. */
function localStamp(instant: string, zone: ReportZone): string {
  const parts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', {
    timeZone: zone, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  }).formatToParts(new Date(instant)).map((part) => [part.type, part.value]));
  return `${parts.year}-${parts.month}-${parts.day} ${parts.hour}:${parts.minute}`;
}

interface Measures {
  readonly label: string;
  readonly value: number | null;
}

/** Las filas de la vista, en su orden. Un estado ausente queda null, no cero. */
function measures(view: OrderIndicatorsView): Measures[] {
  return [
    { label: 'Pedidos creados', value: view.i01CreatedOrders },
    { label: 'Actualmente vencidos', value: view.i04CurrentlyExpiredOrders },
    { label: 'Activos', value: view.activeOrders },
    { label: 'Dados de baja', value: view.inactiveOrders },
    ...orderStates.map((state) => ({
      label: `Estado actual: ${state.label}`,
      value: view.i03CurrentStatusCounts?.find((item) => item.status === state.code)?.count ?? null,
    })),
  ];
}

/**
 * La fotografía que entiende la compuerta. Las claves son las etiquetas de la
 * vista, en su orden, para que la resta salga con el mismo nombre de fila.
 */
function snapshot(view: OrderIndicatorsView): ReportComparisonSnapshot {
  return {
    window: view,
    state: view.status,
    validated: true, // solo se llama tras verified()
    metrics: Object.fromEntries(measures(view).map((measure) => [measure.label, measure.value])),
  };
}

/** Revalida una respuesta contra su propio período: coherente o nada. */
function verified(view: OrderIndicatorsView): OrderIndicatorsView | string {
  try {
    verifyOrderIndicators(view, { start: view.startInclusive, end: view.endExclusive });
  } catch (error) {
    if (error instanceof InconsistentIndicatorsError) return 'Los indicadores no cuadran entre sí; no se exportan.';
    throw error;
  }
  if (view.status === 'source_unavailable') return 'El módulo de ventas no está activo; no hay indicadores que exportar.';
  return view;
}

function cohortRows(name: string, view: OrderIndicatorsView, zone: ReportZone): string[] {
  const start = localStamp(view.startInclusive, zone);
  const end = localStamp(view.endExclusive, zone);
  const observed = view.observedAtUtc ? localStamp(view.observedAtUtc, zone) : '';
  return measures(view)
    .filter((measure) => measure.value !== null)
    .map((measure) => row([name, start, end, zone, observed, SOURCE_SALES, measure.label, measure.value as number]));
}

export function buildOrderIndicatorsCsv(input: OrderIndicatorsCsvInput): OrderIndicatorsCsv {
  if (!isReportZone(input.zone)) {
    return { kind: 'unavailable', reason: 'Zona horaria no admitida.' };
  }
  const baseline = verified(input.baseline);
  if (typeof baseline === 'string') return { kind: 'unavailable', reason: baseline };

  const lines = [row(HEADER), ...cohortRows('A', baseline, input.zone)];

  if (input.comparison) {
    const compared = verified(input.comparison);
    if (typeof compared === 'string') return { kind: 'unavailable', reason: `Cohorte B: ${compared}` };
    lines.push(...cohortRows('B', compared, input.zone));

    const decision = assessReportComparison(snapshot(baseline), snapshot(compared));
    if (decision.decision === 'comparable' && decision.differenceBasis === 'independent_snapshots') {
      // La compuerta ordena sus métricas por clave; el CSV sigue el orden de la vista.
      const byLabel = new Map(decision.metrics.map((metric) => [metric.code, metric.absoluteDifference]));
      for (const { label } of measures(baseline)) {
        const difference = byLabel.get(label);
        if (difference === undefined || difference === null) continue;
        lines.push(row(['B − A (resta descriptiva)', '', '', input.zone, '', SOURCE_REPORTS, label, difference]));
      }
    }
  }

  const stamp = (instant: string) => localStamp(instant, input.zone).replace(/[-: ]/g, '');
  return {
    kind: 'ready',
    fileName: `reportes-pedidos-${stamp(baseline.startInclusive)}-${stamp(baseline.endExclusive)}.csv`,
    mimeType: CSV_MIME_TYPE,
    content: BOM + lines.join(EOL) + EOL,
  };
}
