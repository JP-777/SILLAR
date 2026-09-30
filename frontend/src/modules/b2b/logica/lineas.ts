import type { LineaDeCotizacionAdmin, LineaRequest } from '../services/contratos';

/**
 * Las líneas de un borrador tal como se editan (A8). El orden de la lista es el
 * orden del documento: al guardar se sustituye el conjunto entero.
 */
export interface LineaEditable {
  /** Clave estable para React; no viaja al backend. */
  clave: string;
  /** `null` = línea libre. */
  itemId: string | null;
  /** Nombre de la presentación, para la etiqueta; `null` en una línea libre. */
  presentacion: string | null;
  /** Referencia de catálogo: `null` con presentación = «a consultar». */
  precioCatalogo: number | null;
  descripcion: string;
  cantidad: string;
  precioUnitario: string;
}

let secuencia = 0;
const nuevaClave = () => `l${++secuencia}`;

export function desdeContrato(lineas: readonly LineaDeCotizacionAdmin[]): LineaEditable[] {
  return [...lineas]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((l) => ({
      clave: nuevaClave(),
      itemId: l.itemId,
      presentacion: l.itemId ? [l.productName, l.variantValue].filter(Boolean).join(' — ') : null,
      precioCatalogo: l.catalogPriceAtQuote,
      descripcion: l.description,
      cantidad: String(l.quantity),
      precioUnitario: String(l.unitPrice),
    }));
}

export function lineaLibre(): LineaEditable {
  return { clave: nuevaClave(), itemId: null, presentacion: null, precioCatalogo: null, descripcion: '', cantidad: '1', precioUnitario: '0' };
}

export function lineaDeCatalogo(p: { itemId: string; productName: string; variantValue: string | null; price: number | null }): LineaEditable {
  const presentacion = [p.productName, p.variantValue].filter(Boolean).join(' — ');
  return {
    clave: nuevaClave(),
    itemId: p.itemId,
    presentacion,
    precioCatalogo: p.price,
    descripcion: presentacion,
    cantidad: '1',
    // El precio cobrado lo pone el personal. Se propone el de catálogo si lo
    // hay; si es «a consultar», no se inventa: queda en blanco para rellenar.
    precioUnitario: p.price === null ? '' : String(p.price),
  };
}

/** Subir (`-1`) o bajar (`+1`) una línea. Fuera de rango, la lista no cambia. */
export function moverLinea(lineas: readonly LineaEditable[], indice: number, direccion: -1 | 1): LineaEditable[] {
  const destino = indice + direccion;
  if (indice < 0 || indice >= lineas.length || destino < 0 || destino >= lineas.length) return [...lineas];
  const copia = [...lineas];
  [copia[indice], copia[destino]] = [copia[destino], copia[indice]];
  return copia;
}

function numero(texto: string): number | null {
  const limpio = texto.trim().replace(',', '.');
  if (limpio === '') return null;
  const valor = Number(limpio);
  return Number.isFinite(valor) ? valor : null;
}

/** Errores por línea, con la frase para la persona. Vacío = se puede guardar. Cero líneas es válido. */
export function erroresDeLineas(lineas: readonly LineaEditable[]): Map<string, string> {
  const errores = new Map<string, string>();
  lineas.forEach((l, i) => {
    const n = i + 1;
    const cantidad = numero(l.cantidad);
    const precio = numero(l.precioUnitario);
    if (l.itemId === null && l.descripcion.trim() === '') errores.set(l.clave, `La línea ${n} es libre: escribe qué es.`);
    else if (cantidad === null || !Number.isInteger(cantidad) || cantidad <= 0) errores.set(l.clave, `La línea ${n} necesita una cantidad entera mayor que cero.`);
    else if (precio === null || precio < 0) errores.set(l.clave, `La línea ${n} necesita un precio unitario de cero o más.`);
  });
  return errores;
}

/** Lo que se envía a `PUT /api/admin/b2b/quotes/{id}`: el orden de la lista es el del documento. */
export function aPeticion(lineas: readonly LineaEditable[]): LineaRequest[] {
  return lineas.map((l) => ({
    itemId: l.itemId,
    description: l.descripcion.trim() === '' ? null : l.descripcion.trim(),
    quantity: numero(l.cantidad) ?? 0,
    unitPrice: numero(l.precioUnitario) ?? 0,
  }));
}

/** Total a cobrar: cantidad × precio unitario, tal cual. Ningún descuento automático. */
export function totalCobrado(lineas: readonly LineaEditable[]): number {
  return lineas.reduce((suma, l) => suma + (numero(l.cantidad) ?? 0) * (numero(l.precioUnitario) ?? 0), 0);
}
