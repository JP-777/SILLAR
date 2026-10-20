import type { OrderIndicatorsView } from './orderIndicators.ts';
import { buildOrderIndicatorsCsv } from './orderIndicatorsCsv.ts';
import type { ReportZone } from './period.ts';

/**
 * M10 — punto de ejecución de la exportación CSV, condicionado a la auditoría.
 *
 * D-M10-05: toda exportación queda auditada con una acción PROPIA de CORE. Esa
 * acción no existe todavía (`AuditAction.All` no la tiene y el CHECK de
 * `core.audit_log` se construye desde esa lista), así que aquí:
 *
 * - sin puerto de auditoría no hay archivo: la exportación queda bloqueada;
 * - con puerto, primero se registra la exportación y solo después se entrega
 *   el archivo; si el registro falla, tampoco hay archivo (cerrado por defecto);
 * - lo que se audita es el hecho, nunca el contenido: informe, período, zona,
 *   si incluye comparación y cuántas filas. Sin datos personales.
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
  readonly dataRows: number;
  readonly fileName: string;
}

/**
 * Lo que M10 necesita para poder exportar. Lo implementará una llamada al
 * endpoint de M10 que escriba la auditoría con la acción de CORE. Debe
 * resolverse solo cuando el registro quedó escrito.
 */
export interface ExportAuditPort {
  recordExport(entry: OrderIndicatorsExportAudit): Promise<void>;
}

/**
 * ÚNICO punto de conexión. Hoy no existe la capacidad de auditoría, así que
 * vale null y toda exportación queda bloqueada. Integración lo sustituye por la
 * implementación real cuando CORE publique la acción de exportación.
 */
export const exportAuditPort: ExportAuditPort | null = null;

export type OrderIndicatorsExport =
  | { readonly kind: 'ready'; readonly fileName: string; readonly mimeType: string; readonly content: string }
  | { readonly kind: 'blocked'; readonly reason: string }
  | { readonly kind: 'unavailable'; readonly reason: string }
  | { readonly kind: 'audit_failed'; readonly reason: string };

export interface OrderIndicatorsExportRequest {
  readonly zone: ReportZone;
  readonly baseline: OrderIndicatorsView;
  readonly comparison?: OrderIndicatorsView | null;
}

export async function prepareOrderIndicatorsExport(
  request: OrderIndicatorsExportRequest,
  audit: ExportAuditPort | null = exportAuditPort,
): Promise<OrderIndicatorsExport> {
  if (audit === null) {
    return {
      kind: 'blocked',
      reason: 'La exportación tiene que quedar registrada en la auditoría y esa capacidad todavía no está disponible.',
    };
  }

  const csv = buildOrderIndicatorsCsv(request);
  if (csv.kind !== 'ready') return csv;

  const dataRows = csv.content.split('\r\n').filter((line) => line !== '').length - 1;
  try {
    await audit.recordExport({
      report: 'pedidos',
      startInclusive: request.baseline.startInclusive,
      endExclusive: request.baseline.endExclusive,
      zone: request.zone,
      includesComparison: Boolean(request.comparison),
      dataRows,
      fileName: csv.fileName,
    });
  } catch {
    return {
      kind: 'audit_failed',
      reason: 'No se pudo registrar la exportación en la auditoría, así que no se generó el archivo. Vuelve a intentarlo.',
    };
  }
  return csv;
}
