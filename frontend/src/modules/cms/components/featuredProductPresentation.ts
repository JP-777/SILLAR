/** Presenta los tres estados de precio sin perder la marca de variación aparte. */
export function formatFeaturedProductPrice(price: number | null): string {
  if (price === null) {
    return 'A consultar';
  }

  if (price === 0) {
    return 'Gratis';
  }

  return price.toLocaleString('es-PE', { style: 'currency', currency: 'PEN' });
}
