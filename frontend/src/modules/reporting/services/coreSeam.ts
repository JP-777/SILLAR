import type { ExportAuditDecision, ExportAuditPort } from './reportExport.ts';

/**
 * M10 — adaptadores candidatos de la costura con CORE, sin red: reciben la
 * llamada ya hecha. La unión con el cliente HTTP vive en `reporting.ts`.
 *
 * Ninguno de los dos está conectado. Los puntos de conexión siguen en null
 * (`installationZone.ts`, `reportExport.ts`) hasta que el host registre los
 * productores de CORE y la zona llegue antes de montar la pantalla.
 */

/**
 * De la respuesta de `GET /admin/reporting/installation-zone` a lo que entra
 * por el punto de conexión de la zona. «Sin conexión» vuelve a ser null, el
 * mismo estado que hoy; el resto lo lee y lo valida `zoneObservation.ts`.
 */
export function installationZoneFromResponse(body: unknown): unknown {
  if (typeof body === 'object' && body !== null && (body as { kind?: unknown }).kind === 'unconnected') return null;
  return body;
}

/** Una lectura que falla bloquea los reportes: no se supone ninguna zona. */
export async function readInstallationZone(get: () => Promise<unknown>): Promise<unknown> {
  try {
    return installationZoneFromResponse(await get());
  } catch {
    return { kind: 'invalid', original: null };
  }
}

/** Lo único que viaja a CORE: la zona del archivo y sus filas. Ni períodos ni nombre de archivo. */
export interface ExportAuditCall {
  readonly zone: string;
  readonly dataRows: number;
}

/**
 * Puerto de auditoría sobre `POST /admin/reporting/orders/export-audit`.
 * `recorded` solo con la confirmación y el identificador de CORE; `denied`
 * cuando CORE se niega. Cualquier otra respuesta o un error del servidor
 * (zona sin verificar, auditoría no disponible) rechaza: no hay archivo.
 */
export function exportAuditPortOver(post: (call: ExportAuditCall) => Promise<unknown>): ExportAuditPort {
  return {
    async recordExport(entry): Promise<ExportAuditDecision> {
      const answer = await post({ zone: entry.zone, dataRows: entry.dataRows });
      if (typeof answer === 'object' && answer !== null) {
        const { decision, exportId } = answer as { decision?: unknown; exportId?: unknown };
        if (decision === 'recorded' && typeof exportId === 'string' && exportId !== '') return 'recorded';
        if (decision === 'denied') return 'denied';
      }
      throw new Error('La auditoría respondió algo que no es una confirmación.');
    },
  };
}
