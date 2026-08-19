# SILLAR UI — conventions

No provider or root wrapper is required. Every component is self-contained (no
context, no theme injection) — just import from the bundle and render.

## Dark mode

Set `data-theme="dark"` on any ancestor element (commonly `<html>` or the
outermost container) to switch the whole page into dark mode; omit it (or use
`data-theme="light"`) for light mode. Every token flips automatically —
never toggle colors manually per component.

## Styling idiom: role tokens, never raw colors

There is **no utility-class system** (no `bg-*`/`gap-*` Tailwind-style
classes) and components take **no color/spacing props** — each component
ships its own BEM-style class (`ui-button`, `ui-field__label`, `gal__item`…)
already wired to the token set below. You never write or override those
classes.

What you DO write: your own layout glue (the `<div>`s that space components
out on a screen) using **role tokens**, via inline `style` or a small class,
e.g. `style={{ display: 'flex', gap: 'var(--s4)' }}`.

Role tokens (defined in `tokens/tokens.css`, all with dark-mode variants —
these are the only color vars a design may reference; raw palette vars like
`--stone-500` / `--accent-500` exist only to *build* the roles and must never
be used directly):

| Purpose | Token |
|---|---|
| Page / surface background | `--bg`, `--bg-raised`, `--bg-sunken` |
| Text | `--text`, `--text-muted`, `--text-subtle` |
| Borders | `--border` (decorative), `--border-strong` (control outlines) |
| Brand / action | `--primary`, `--primary-hover`, `--on-primary` |
| Links | `--link` (may differ from `--primary` — always use this for `<a>`/link-styled text, never `--primary`) |
| Semantic | `--success` / `--success-bg`, `--warning` / `--warning-bg`, `--danger` / `--danger-bg`, text-on-danger `--on-danger` |
| Radius | `--r-sm`, `--r-md`, `--r-lg` |
| Spacing (4px scale) | `--s1` (4px) … `--s8` (64px) |
| Shadow | `--shadow`, `--shadow-lg` |
| Type | `--font` (UI sans stack), `--mono` |

## Where the truth lives

- `tokens/tokens.css` — the full role + palette token set (read before any
  custom layout styling).
- `tokens/base.css` — global element resets (body, headings, focus ring,
  `.sr-only`). Focus is always visible (`:focus-visible`); never suppress it.
- `styles.css` — the root stylesheet; its `@import` chain (tokens, then
  `_ds_bundle.css`) is everything a rendered design receives.
- `components/general/<Name>/<Name>.prompt.md` — per-component usage notes
  and props, generated from the real JSDoc + prop types.

## Composition idiom

Content is **always Spanish** (this is a Peruvian retail/ops product —
SILLAR). Prices read `S/ 12.50`; dates `18/08/2026` or `es-PE` long form.
Destructive actions **never** say "¿Estás seguro?" — `ConfirmDialog`'s
`confirmLabel` names the actual action ("Desactivar usuario", "Activar
Catálogo"). Errors state what's blocking and what to do, not "Ha ocurrido un
error".

```tsx
import { Field, Input, Button, Card } from 'sillar-frontend';

<div style={{ maxWidth: 380 }}>
  <Card title="Nuevo producto">
    <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--s4)' }}>
      <Field label="Nombre" required>
        {(props) => <Input placeholder="Cuaderno universitario cuadriculado A4" {...props} />}
      </Field>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--s2)' }}>
        <Button variant="secondary">Cancelar</Button>
        <Button>Guardar cambios</Button>
      </div>
    </div>
  </Card>
</div>
```
