import { existsSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { E2E_DIR } from './env.js';

const SCREENSHOTS_DIR = path.join(E2E_DIR, 'screenshots');

/**
 * Un `index.html` en `e2e/screenshots/` que enseña todo de un vistazo: una
 * fila por paso, claro y oscuro lado a lado. Sin esto, revisar la carpeta es
 * abrir archivos de uno en uno adivinando el orden por el nombre.
 */
export function buildGallery(): void {
  if (!existsSync(SCREENSHOTS_DIR)) {
    return;
  }

  const specs = readdirSync(SCREENSHOTS_DIR).filter((name) =>
    statSync(path.join(SCREENSHOTS_DIR, name)).isDirectory(),
  );

  const sections = specs
    .sort()
    .map((spec) => {
      const dir = path.join(SCREENSHOTS_DIR, spec);
      const files = readdirSync(dir).filter((name) => name.endsWith('.png'));

      const pairs = new Map<string, { light?: string; dark?: string }>();
      for (const file of files) {
        const isLight = file.endsWith('--claro.png');
        const isDark = file.endsWith('--oscuro.png');
        if (!isLight && !isDark) continue;

        const stem = file.replace(/--(claro|oscuro)\.png$/, '');
        const entry = pairs.get(stem) ?? {};
        if (isLight) entry.light = file;
        if (isDark) entry.dark = file;
        pairs.set(stem, entry);
      }

      const rows = [...pairs.entries()]
        .sort(([a], [b]) => a.localeCompare(b))
        .map(
          ([stem, { light, dark }]) => `
        <section class="step">
          <h3>${escapeHtml(stem)}</h3>
          <div class="pair">
            ${light ? `<figure><img src="${spec}/${light}" loading="lazy" alt="${escapeHtml(stem)}, tema claro"><figcaption>claro</figcaption></figure>` : ''}
            ${dark ? `<figure><img src="${spec}/${dark}" loading="lazy" alt="${escapeHtml(stem)}, tema oscuro"><figcaption>oscuro</figcaption></figure>` : ''}
          </div>
        </section>`,
        )
        .join('\n');

      return `<article class="spec"><h2>${escapeHtml(spec)}</h2>${rows}</article>`;
    })
    .join('\n');

  const html = `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>Capturas e2e</title>
<style>
  body { font: 14px/1.5 -apple-system, "Segoe UI", sans-serif; margin: 0; padding: 24px; background: #FAF8F5; color: #2C2822; }
  h1 { font-size: 20px; }
  .spec { margin-bottom: 40px; }
  .spec h2 { border-bottom: 2px solid #E3DCD1; padding-bottom: 6px; }
  .step h3 { font-size: 14px; font-weight: 600; margin: 20px 0 8px; }
  .pair { display: flex; gap: 16px; flex-wrap: wrap; }
  figure { margin: 0; background: #fff; border: 1px solid #E3DCD1; border-radius: 6px; padding: 8px; }
  figure img { max-width: 420px; display: block; border-radius: 3px; }
  figcaption { text-align: center; font-size: 12px; color: #5C5447; margin-top: 4px; }
</style>
</head>
<body>
<h1>Capturas del arnés e2e</h1>
<p>Generado al final de cada corrida. Una fila por paso, claro y oscuro lado a lado.</p>
${sections || '<p>Sin capturas todavía.</p>'}
</body>
</html>`;

  writeFileSync(path.join(SCREENSHOTS_DIR, 'index.html'), html, 'utf8');
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
