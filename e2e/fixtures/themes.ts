import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { E2E_DIR } from '../setup/env.js';

const SCREENSHOTS_DIR = path.join(E2E_DIR, 'screenshots');

type Theme = 'light' | 'dark';

const LABEL: Record<Theme, string> = { light: 'claro', dark: 'oscuro' };

/**
 * Para cada paso relevante de una prueba: axe-core (contraste y
 * accesibilidad) y una captura, en los dos temas. Dos temas por paso, no la
 * prueba entera duplicada — la lógica de negocio no cambia con el tema, lo
 * que cambia es lo que se ve.
 *
 * El tema se fuerza escribiendo `data-theme` directamente, no pulsando el
 * interruptor real: este helper se usa en pasos donde puede que ni siquiera
 * esté montado (antes de iniciar sesión, por ejemplo). El interruptor en sí
 * — que existe y hace exactamente esto — lo prueba `tema.spec.ts`.
 *
 * Termina siempre en claro, para que la prueba que llama pueda seguir su
 * flujo sin que el tema se le quede cambiado a mitad de camino.
 */
export function themeRecorder(page: Page, spec: string) {
  const dir = path.join(SCREENSHOTS_DIR, spec);
  mkdirSync(dir, { recursive: true });
  let step = 0;

  async function setTheme(theme: Theme): Promise<void> {
    await page.evaluate((value) => {
      // Sin esto, axe puede fotografiar un color a mitad de una transición
      // (.12s/.15s ease de varios componentes, para el cambio de tema de
      // verdad) y ver un tono que no es ni el claro ni el oscuro —y que
      // puede no cumplir contraste— sin que exista ningún problema real.
      const style = document.createElement('style');
      style.textContent = '*, *::before, *::after { transition: none !important; }';
      document.head.appendChild(style);

      document.documentElement.setAttribute('data-theme', value);
      void document.documentElement.offsetHeight; // fuerza el repintado antes de quitarlo

      style.remove();
    }, theme);
  }

  async function checkAndShoot(fileStem: string, theme: Theme): Promise<void> {
    await setTheme(theme);

    const results = await new AxeBuilder({ page }).analyze();
    expect(results.violations, describeViolations(results.violations)).toEqual([]);

    await page.screenshot({
      path: path.join(dir, `${fileStem}--${LABEL[theme]}.png`),
      fullPage: true,
    });
  }

  return async function capture(stepName: string): Promise<void> {
    step += 1;
    const fileStem = `${String(step).padStart(2, '0')}-${slug(stepName)}`;

    await checkAndShoot(fileStem, 'light');
    await checkAndShoot(fileStem, 'dark');
    await setTheme('light');
  };
}

function slug(text: string): string {
  const withoutAccents = text
    .toLowerCase()
    .normalize('NFD')
    .split('')
    .filter((ch) => {
      const code = ch.codePointAt(0) ?? 0;
      // Marcas diacríticas combinantes (U+0300–U+036F), lo que deja la
      // descomposición NFD de una tilde o una diéresis.
      return code < 0x0300 || code > 0x036f;
    })
    .join('');

  return withoutAccents.replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
}

function describeViolations(violations: readonly { id: string; description: string; nodes: readonly unknown[] }[]): string {
  if (violations.length === 0) {
    return '';
  }

  return violations
    .map((v) => `· ${v.id}: ${v.description} (${v.nodes.length} nodo(s))`)
    .join('\n');
}
