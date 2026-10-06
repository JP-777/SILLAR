/**
 * Importes con la moneda de la instalación (`currency_code`, ajuste público de
 * CORE). Si no hay código válido, el número a secas: ningún símbolo escrito aquí.
 */
export function formatearImporte(valor: number, moneda: string | null): string {
  if (moneda && /^[A-Z]{3}$/.test(moneda)) {
    try {
      return valor.toLocaleString('es-PE', { style: 'currency', currency: moneda });
    } catch {
      // Código que Intl no reconoce: se cae al número.
    }
  }
  return valor.toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** El precio de catálogo de referencia: nulo es «A consultar», nunca gratis. */
export function precioDeCatalogo(valor: number | null, moneda: string | null): string {
  return valor === null ? 'A consultar' : formatearImporte(valor, moneda);
}

/** Fecha corta legible, en es-PE y hora de Lima. */
export function formatearFecha(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('es-PE', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Lima' });
}
