export type DateFilterBoundary = 'start' | 'end';

export type DateFilterResult =
  | { readonly kind: 'empty' }
  | { readonly kind: 'invalid' }
  | { readonly kind: 'valid'; readonly apiValue: string };

export function parseDateFilter(
  input: string,
  boundary: DateFilterBoundary,
): DateFilterResult {
  const value = input.trim();

  if (value === '') {
    return { kind: 'empty' };
  }

  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);

  if (!match) {
    return { kind: 'invalid' };
  }

  const day = Number(match[1]);
  const month = Number(match[2]);
  const year = Number(match[3]);

  const date = new Date(Date.UTC(year, month - 1, day));

  if (
    year < 1000 ||
    date.getUTCFullYear() !== year ||
    date.getUTCMonth() !== month - 1 ||
    date.getUTCDate() !== day
  ) {
    return { kind: 'invalid' };
  }

  const isoDay =
    `${String(year).padStart(4, '0')}-` +
    `${String(month).padStart(2, '0')}-` +
    `${String(day).padStart(2, '0')}`;

  return {
    kind: 'valid',
    apiValue:
      boundary === 'start'
        ? `${isoDay}T00:00:00Z`
        : `${isoDay}T23:59:59Z`,
  };
}

export function formatDateFilter(
  apiValue: string | undefined,
): string {
  if (!apiValue) {
    return '';
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(apiValue);

  if (!match) {
    return '';
  }

  return `${match[3]}/${match[2]}/${match[1]}`;
}
