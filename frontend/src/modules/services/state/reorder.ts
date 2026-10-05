export function reorderedIds<T extends { readonly id: number }>(items: readonly T[], from: number, to: number): number[] {
  if (!Number.isInteger(from) || !Number.isInteger(to) || from < 0 || to < 0 || from >= items.length || to >= items.length) {
    throw new RangeError('Las posiciones deben pertenecer al listado completo.');
  }
  const next = [...items];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next.map((item) => item.id);
}
