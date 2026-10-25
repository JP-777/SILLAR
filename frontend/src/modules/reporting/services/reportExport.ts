import type { OrderIndicatorsView } from './orderIndicators.ts';
import { buildOrderIndicatorsCsv } from './orderIndicatorsCsv.ts';
import { verifiedInstallationZone, type InstallationZone } from './installationZone.ts';
import { INSTALLATION, reportZonesFor, type ReportZone } from './period.ts';

/**
 * M10 — punto de ejecución de la exportación CSV, condicionado a la auditoría.
 *
 * D-M10-05: toda exportación queda auditada con una acción PROPIA de CORE. Esa
 * acción no existe todavía (`AuditAction.All` no la tiene y el CHECK de
 * `core.audit_log` se construye desde esa lista), así que aquí:
 *
 * - sin puerto de auditoría no hay archivo: la exportación queda bloqueada;
 * - sin zona de la instalación verificada tampoco: ni el respaldo histórico de
 *   Lima ni una zona ausente o sin verificar sirven para un archivo nuevo;
 * - la zona del archivo es la de esa misma instalación verificada, o UTC; no
 *   la de una lista calculada antes, que podría ser de otra instalación;
 * - con puerto, primero se registra la exportación y solo después se entrega
 *   el archivo; si el registro falla, se niega o responde otra cosa que la
 *   confirmación, tampoco hay archivo (cerrado por defecto);
 * - lo que se audita es el hecho, nunca el contenido: informe, los dos
 *   períodos que van en el archivo, zona y cuántas filas. Sin datos personales;
 * - una auditoría que no contesta a tiempo es un fallo: no hay archivo. Si el
 *   registro llega tarde queda un registro sin archivo, nunca al revés.
 *
 * Esta barrera vive en el navegador, que ya tiene los datos: sirve para que la
 * pantalla no entregue nada sin confirmación, pero no es la garantía. La
 * garantía es un endpoint de M10 que escriba con `IAuditWriter.WriteAsync` y
 * solo después devuelva el archivo; falta la acción de exportación en
 * `AuditAction` y en el CHECK de `core.audit_log`.
 *
 * Esta función no descarga nada ni la importa ninguna pantalla. Entregar el
 * archivo al navegador y mostrar el botón son pasos posteriores a la costura.
 */

/** Lo que se registra de una exportación. Agregado y sin datos personales. */
export interface OrderIndicatorsExportAudit {
  readonly report: 'pedidos';
  readonly startInclusive: string;
  readonly endExclusive: string;
  readonly zone: ReportZone;
  readonly includesComparison: boolean;
  /** Período de la comparación que también va en el archivo; null si no hay. */
  readonly comparisonStartInclusive: string | null;
  readonly comparisonEndExclusive: string | null;
  readonly dataRows: number;
  readonly fileName: string;
}

/**
 * Lo que M10 necesita para poder exportar. Lo implementará una llamada al
 * endpoint de M10 que escriba la auditoría con la acción de CORE.
 *
 * Responde `'recorded'` solo cuando el registro quedó escrito, y `'denied'`
 * cuando la auditoría se niega a registrarlo. Cualquier otra respuesta —un
 * `undefined`, un objeto, un texto parecido— se trata como fallo: el archivo
 * solo sale con la confirmación exacta (cerrado por defecto).
 */
export type ExportAuditDecision = 'recorded' | 'denied';

export interface ExportAuditPort {
  recordExport(entry: OrderIndicatorsExportAudit): Promise<ExportAuditDecision>;
}

/**
 * ÚNICO punto de conexión. Hoy no existe la capacidad de auditoría, así que
 * vale null y toda exportación queda bloqueada. Integración lo sustituye por la
 * implementación real cuando CORE publique la acción de exportación.
 */
export const exportAuditPort: ExportAuditPort | null = null;

/** Lo que se espera a la auditoría antes de darla por fallida. */
export const EXPORT_AUDIT_TIMEOUT_MS = 15_000;

export type OrderIndicatorsExport =
  | { readonly kind: 'ready'; readonly fileName: string; readonly mimeType: string; readonly content: string }
  | { readonly kind: 'blocked'; readonly reason: string }
  | { readonly kind: 'unavailable'; readonly reason: string }
  | { readonly kind: 'audit_denied'; readonly reason: string }
  | { readonly kind: 'audit_failed'; readonly reason: string };

export interface OrderIndicatorsExportRequest {
  readonly zone: ReportZone;
  readonly baseline: OrderIndicatorsView;
  readonly comparison?: OrderIndicatorsView | null;
}

export async function prepareOrderIndicatorsExport(
  request: OrderIndicatorsExportRequest,
  audit: ExportAuditPort | null = exportAuditPort,
  timeoutMs: number = EXPORT_AUDIT_TIMEOUT_MS,
  installation: InstallationZone = INSTALLATION,
): Promise<OrderIndicatorsExport> {
  if (audit === null) {
    return {
      kind: 'blocked',
      reason: 'La exportación tiene que quedar registrada en la auditoría y esa capacidad todavía no está disponible.',
    };
  }
  if (verifiedInstallationZone(installation) === null) {
    return {
      kind: 'blocked',
      reason: 'Para exportar, la zona horaria de la instalación tiene que estar verificada, y todavía no lo está.',
    };
  }
  const zones = reportZonesFor(installation);
  if (!zones.includes(request.zone)) {
    return {
      kind: 'blocked',
      reason: 'La zona horaria pedida no es la de la instalación ni UTC, así que no se generó el archivo.',
    };
  }

  const csv = buildOrderIndicatorsCsv(request, zones);
  if (csv.kind !== 'ready') return csv;

  const dataRows = csv.content.split('\r\n').filter((line) => line !== '').length - 1;
  // Congelada: el puerto no puede cambiar lo que se registra antes de responder.
  const entry: OrderIndicatorsExportAudit = Object.freeze({
    report: 'pedidos',
    startInclusive: request.baseline.startInclusive,
    endExclusive: request.baseline.endExclusive,
    zone: request.zone,
    includesComparison: Boolean(request.comparison),
    comparisonStartInclusive: request.comparison?.startInclusive ?? null,
    comparisonEndExclusive: request.comparison?.endExclusive ?? null,
    dataRows,
    fileName: csv.fileName,
  });
  let decision: unknown;
  let timer: ReturnType<typeof setTimeout> | undefined;
  try {
    decision = await Promise.race([
      audit.recordExport(entry),
      new Promise((resolve) => { timer = setTimeout(resolve, timeoutMs, undefined); }),
    ]);
  } catch {
    decision = undefined;
  } finally {
    clearTimeout(timer);
  }
  if (decision === 'denied') {
    return {
      kind: 'audit_denied',
      reason: 'La auditoría no autorizó registrar esta exportación, así que no se generó el archivo.',
    };
  }
  if (decision !== 'recorded') {
    return {
      kind: 'audit_failed',
      reason: 'No se pudo registrar la exportación en la auditoría, así que no se generó el archivo. Vuelve a intentarlo.',
    };
  }
  return csv;
}
