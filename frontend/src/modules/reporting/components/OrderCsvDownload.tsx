import { useEffect, useRef, useState } from 'react';
import { downloadOrderIndicatorsCsv } from '../services/reporting';
import { useModulePermissions } from '../../../session';
import { describe } from '../../../shared/errors/messages';

/** El servidor consulta, valida, cuenta y audita el archivo antes de responder. */
export function OrderCsvDownload({ start, end, zone }: { start: string; end: string; zone: string }) {
  const { hasOperation } = useModulePermissions();
  const active = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  useEffect(() => () => active.current?.abort(), []);
  async function download() {
    const controller = new AbortController(); active.current = controller;
    setBusy(true); setMessage(null);
    try {
      const content = await downloadOrderIndicatorsCsv(start, end, zone, controller.signal);
      if (controller.signal.aborted) return;
      const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }));
      const link = document.createElement('a'); link.href = url;
      link.download = `pedidos-${start.slice(0, 10)}-${end.slice(0, 10)}.csv`;
      document.body.append(link); link.click(); link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
      setMessage('Archivo preparado y envío al navegador iniciado.');
    } catch (error) {
      if (controller.signal.aborted) return;
      const result = describe(error, 'exportar los indicadores');
      if (result.kind !== 'silent') setMessage(result.message);
    } finally { if (!controller.signal.aborted) setBusy(false); }
  }
  if (!hasOperation('reporting', 'export')) return null;
  return <div>
    <button type="button" className="ui-button" disabled={busy} onClick={() => void download()}>
      {busy ? 'Preparando CSV…' : 'Exportar recuentos del período (CSV)'}
    </button>
    <p className="rp-note">El archivo consulta de nuevo los recuentos del período principal; conserva su propio instante de observación. No incluye importes ni la comparación.</p>
    <p className="rp-note" role="status">{message}</p>
  </div>;
}
