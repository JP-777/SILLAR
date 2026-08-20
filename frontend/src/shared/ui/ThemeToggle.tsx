import { useTheme } from '../hooks/useTheme';
import { Button } from './index';

/**
 * Interruptor de tema claro/oscuro.
 *
 * El texto nombra el tema al que se pasa, no el que hay: "Tema oscuro" mientras
 * está en claro, "Tema claro" mientras está en oscuro. Mismo criterio que
 * `docs/sillar-design-system.html`.
 */
export function ThemeToggle() {
  const { theme, toggle } = useTheme();
  const next = theme === 'dark' ? 'claro' : 'oscuro';

  return (
    <Button variant="ghost" size="sm" onClick={toggle} aria-label={`Cambiar a tema ${next}`}>
      Tema {next}
    </Button>
  );
}
