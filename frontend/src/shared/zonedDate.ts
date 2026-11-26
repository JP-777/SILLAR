/** Conversión IANA explícita, independiente de la zona del navegador. */
const formatters = new Map<string, Intl.DateTimeFormat>();

/** Fecha y hora de pared de un instante en la zona, `YYYY-MM-DDTHH:mm:ss`. */
export function wallClock(instantMs: number, zone: string): string {
  let formatter = formatters.get(zone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat('en-US', {
      timeZone: zone, hourCycle: 'h23', era: 'short', year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
    });
    formatters.set(zone, formatter);
  }
  const p = Object.fromEntries(formatter.formatToParts(new Date(instantMs)).map((part) => [part.type, part.value]));
  if (p.era !== 'AD') return '';
  return `${p.year.padStart(4, '0')}-${p.month}-${p.day}T${p.hour}:${p.minute}:${p.second}`;
}

/** El mismo texto leído como si fuera UTC; NaN si no es una fecha de calendario. */
function wallAsUtc(wall: string): number {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})$/.exec(wall);
  if (!m) return NaN;
  const d = new Date(0);
  d.setUTCFullYear(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  d.setUTCHours(Number(m[4]), Number(m[5]), Number(m[6]), 0);
  return d.getTime();
}

/**
 * Período explícito: jamás se deduce de la zona del navegador.
 * `datetime-local` entrega `HH:mm` o, según el navegador, segundos y milisegundos.
 * Lanza `RangeError` si el valor no forma una fecha válida.
 *
 * D11: además exige ida y vuelta. Algunos motores normalizan fechas imposibles
 * (`2026-02-30` → `2026-03-02`) en vez de rechazarlas; una fecha así consultaría
 * en silencio otro día del que el usuario escribió. La misma ida y vuelta
 * rechaza la hora que no existe en la zona (salto de horario) y la que existe
 * dos veces (retroceso): ninguna se resuelve adivinando.
 */
export function asZonedInstant(value: string, zone: string): string {
  const parts = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})(?::(\d{2})(?:\.(\d{1,3}))?)?$/.exec(value);
  if (!parts) throw new RangeError('Fecha y hora con formato inesperado.');
  const [, minutes, seconds = '00', fraction = '0'] = parts;
  const milliseconds = Number(fraction.padEnd(3, '0'));
  // Mismo rango que DateTimeOffset de .NET: año 1..9999.
  if (minutes.startsWith('0000')) throw new RangeError('El año 0000 no existe en el calendario de la consulta.');
  const wall = `${minutes}:${seconds}`;
  const naive = wallAsUtc(wall);
  if (!Number.isFinite(naive) || new Date(naive).toISOString().slice(0, 19) !== wall) throw new RangeError('Fecha inválida.');
  // Candidatos: el texto menos cada desplazamiento que la zona usa alrededor de él.
  // En los bordes del año 1 y del 9999 el día vecino cae fuera del calendario.
  const offsets = new Set([-1, 0, 1].map((days) => {
    const at = naive + days * 86_400_000;
    return wallAsUtc(wallClock(at, zone)) - at;
  }).filter(Number.isFinite));
  const matches = [...offsets].map((offset) => naive - offset).filter((at) => wallClock(at, zone) === wall);
  if (matches.length === 0) throw new RangeError('La fecha no existe en la zona seleccionada.');
  if (matches.length > 1) throw new RangeError('Esa hora se repite en la zona seleccionada; elige otra.');
  const instant = new Date(matches[0] + milliseconds).toISOString();
  // El borde también vale en UTC: 9999-12-31 23:59 en Lima ya es el año 10000.
  if (!/^\d{4}-/.test(instant) || instant.startsWith('0000')) {
    throw new RangeError('La fecha queda fuera del calendario de la consulta.');
  }
  return instant;
}

