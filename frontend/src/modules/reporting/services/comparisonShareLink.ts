/** D20 — Enlace A+B de FILTROS; reutiliza el codec D12, nunca incluye resultados. */
import { encodeReportLink } from './reportLinks';
import { asInstant, type ReportZone } from './period';
export interface BaselinePeriod { readonly startInclusive: string; readonly endExclusive: string; }
export type ComparisonShareLink =
  | { readonly kind: 'ready'; readonly url: string }
  | { readonly kind: 'unavailable'; readonly reason: string };
const pad = (n: number) => String(n).padStart(2, '0');
/** No redondear subminutos ni interpretar zona del navegador. */
function localMinute(instant: string, zone: ReportZone): string | null {
  const m = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?(Z|[+-]\d{2}:\d{2})$/.exec(instant);
  if (m === null || /[1-9]/.test(m[2] ?? '')) return null;
  const epochMs = Date.parse(instant);
  if (!Number.isFinite(epochMs) || epochMs % 60_000 !== 0) return null;
  const fmt = new Intl.DateTimeFormat('en-US', { timeZone: zone, hourCycle: 'h23',
    year:'numeric', month:'2-digit', day:'2-digit', hour:'2-digit', minute:'2-digit' });
  const p = Object.fromEntries(fmt.formatToParts(new Date(epochMs)).map(({ type, value }) => [type, value]));
  const year = Number(p.year), month=Number(p.month), day=Number(p.day), hour=Number(p.hour), minute=Number(p.minute);
  if (![year,month,day,hour,minute].every(Number.isInteger) || year < 1 || year > 9999) return null;
  const local = `${String(year).padStart(4,'0')}-${pad(month)}-${pad(day)}T${pad(hour)}:${pad(minute)}`;
  try {
    const rebuilt = asInstant(local, zone);
    return Date.parse(rebuilt) === epochMs ? local : null;
  } catch {
    return null;
  }
}
export function prepareComparisonShareLink(baseline: BaselinePeriod, zone: ReportZone,
    comparisonStartLocal: string, comparisonEndLocal: string): ComparisonShareLink {
  if (zone !== 'America/Lima' && zone !== 'UTC')
    return { kind:'unavailable', reason:'Zona de consulta no admitida.' };
  const a = localMinute(baseline.startInclusive, zone);
  const b = localMinute(baseline.endExclusive, zone);
  if (a === null || b === null)
    return { kind:'unavailable', reason:'Fotografía A no representable exactamente en el enlace.' };
  if (!comparisonStartLocal || !comparisonEndLocal)
    return { kind:'unavailable', reason:'Selecciona inicio y fin de la cohorte B.' };
  const url=encodeReportLink({ zone, primary:{startLocal:a,endLocal:b},
    comparison:{startLocal:comparisonStartLocal,endLocal:comparisonEndLocal} });
  return url === null ? {kind:'unavailable',reason:'Filtros o calendario no válidos para un enlace exacto.'} :
    {kind:'ready',url};
}
