import { useEffect, useState } from 'react';
import {
  formatDateFilter,
  parseDateFilter,
  type DateFilterBoundary,
} from '../dateFilter';
import { Field, Input } from './index';

interface DateFilterFieldProps {
  readonly label: string;
  readonly value: string | undefined;
  readonly boundary: DateFilterBoundary;
  readonly onChange: (value: string | undefined) => void;
}

export function DateFilterField({
  label,
  value,
  boundary,
  onChange,
}: DateFilterFieldProps) {
  const [text, setText] = useState(() => formatDateFilter(value));
  const [error, setError] = useState<string | undefined>();

  useEffect(() => {
    setText(formatDateFilter(value));
  }, [value]);

  function change(next: string) {
    setText(next);

    const parsed = parseDateFilter(next, boundary);

    if (parsed.kind === 'invalid') {
      setError('Usa una fecha válida con formato dd/mm/aaaa.');
      return;
    }

    setError(undefined);

    if (parsed.kind === 'empty') {
      onChange(undefined);
      return;
    }

    onChange(parsed.apiValue);
  }

  return (
    <Field label={label} error={error}>
      {(props) => (
        <Input
          {...props}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          placeholder="dd/mm/aaaa"
          maxLength={10}
          value={text}
          invalid={error !== undefined}
          onChange={(event) => change(event.target.value)}
        />
      )}
    </Field>
  );
}
