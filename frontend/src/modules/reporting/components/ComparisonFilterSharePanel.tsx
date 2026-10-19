import { useState } from 'react';
import { type OrderIndicatorsView } from '../services/reporting';
import { type ReportZone as ReportLinkZone } from '../services/period';
import { prepareComparisonShareLink } from '../services/comparisonShareLink';
interface Props { readonly baseline: OrderIndicatorsView; readonly zone: ReportLinkZone;
  readonly comparisonStartLocal: string; readonly comparisonEndLocal: string; }
/** Enlace visible solo tras clic; se invalida inmediatamente si cambian filtros. */
export function ComparisonFilterSharePanel({baseline,zone,comparisonStartLocal,comparisonEndLocal}:Props) {
  const [selection, setSelection] = useState<{url:string; copied:'none'|'yes'|'manual'}|null>(null);
  const candidate=prepareComparisonShareLink(baseline,zone,comparisonStartLocal,comparisonEndLocal);
  const visible=candidate.kind==='ready' && selection?.url===candidate.url ? selection : null;
  async function copyLink() {
    if (visible === null) return;
    try {
      if (typeof window==='undefined' || typeof navigator==='undefined' || !navigator.clipboard?.writeText)
        throw new Error('Portapapeles no disponible');
      await navigator.clipboard.writeText(new URL(visible.url,window.location.origin).href);
      setSelection(old=>old?.url===visible.url ? {...old,copied:'yes'} : old);
    } catch { setSelection(old=>old?.url===visible.url ? {...old,copied:'manual'} : old); }
  }
  return <section className="rp-compare__share" aria-label="Compartir filtros de comparación">
    <p className="rp-compare__note">El enlace contiene exclusivamente los filtros de A y B y la zona horaria;
      no contiene resultados, datos personales ni permisos. A corresponde a la última fotografía consultada.
      Abrir el enlace no consulta datos automáticamente.</p>
    <button type="button" className="ui-button" disabled={candidate.kind!=='ready'}
      onClick={()=>{if(candidate.kind==='ready') setSelection({url:candidate.url,copied:'none'});}}>
      Preparar enlace con ambas cohortes
    </button>
    {candidate.kind==='unavailable' && <p className="rp-compare__note">{candidate.reason}</p>}
    {visible !== null && <div><label>Enlace relativo (solo filtros)
      <input className="ui-input" readOnly value={visible.url} aria-label="Enlace de filtros A y B" />
    </label><button type="button" className="ui-button" onClick={()=>{void copyLink();}}>Copiar enlace</button>
    <p role="status" className="rp-compare__note">{visible.copied==='yes' ? 'Enlace copiado.' :
      visible.copied==='manual' ? 'Copia el enlace manualmente; no se pudo usar el portapapeles.' :
      'Solo se reponen filtros al abrirlo; consultar requiere una acción explícita.'}</p></div>}
  </section>;
}
